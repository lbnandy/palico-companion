using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public static class MiningTools
{
    // Private reusable tool; never transferred to inventories or dropped as loot.
    static Item basicPick;
    // Backpack and hotbar only. Never borrow a bank item or an item on the cursor.
    public static Item Best(Player player)
    {
        Item best=basicPick??=new Item(Terraria.ID.ItemID.CopperPickaxe);
        for(int i=0;i<Math.Min(50,player.inventory.Length);i++) {
            Item item=player.inventory[i];
            if(item==null||item.IsAir||item.pick<=0)continue;
            if(item.pick>best.pick||item.pick==best.pick&&
                (item.useTime<best.useTime||item.useTime==best.useTime&&item.tileBoost>best.tileBoost))best=item;
        }
        return best;
    }

    // Same calculation used by stable Player.ApplyItemTime(item, pickSpeed).
    // This reads timing hooks without using the item or changing player input.
    public static float ExactInterval(Player player,Item tool) => Math.Clamp(
        CombinedHooks.TotalUseTime(tool.useTime*player.pickSpeed,player,tool)/player.GetModPlayer<CompanionPlayer>().Training.WorkSpeed(9),1,600);
    public static int Interval(Player player,Item tool) => (int)Math.Ceiling(ExactInterval(player,tool));

    // Vanilla's player/tool reach rectangle, plus two tiles of companion assistance.
    // Bound exceptional modded boosts so the automatic scan has a fixed upper budget.
    public static Point Range(Player player,Item tool,bool autonomous=false) {
        // Apply equipment after the baseline bounds so both the minimum scan
        // range and unusually long tools retain the advertised +1/+2/+3 tiles.
        int bonus=player.GetModPlayer<CompanionPlayer>().Training.MiningReach;
        return new(
            Math.Clamp(Player.tileRangeX+tool.tileBoost+player.blockRange+2,autonomous?12:2,14)+bonus,
            Math.Clamp(Player.tileRangeY+tool.tileBoost+player.blockRange+2,autonomous?10:2,14)+bonus);
    }

    public static bool InRange(Player player,Item tool,Point tile,bool autonomous=false)
    {
        Point range=Range(player,tool,autonomous);
        return tile.X>=player.position.X/16f-range.X &&
            tile.X<=(player.position.X+player.width)/16f+range.X-1 &&
            tile.Y>=player.position.Y/16f-range.Y &&
            tile.Y<=(player.position.Y+player.height)/16f+range.Y-2;
    }
}

// Carry only fractions earned by completed strokes, including across ore blocks.
// Interrupted windups cannot accumulate a free hit; never do multiple hits/tick.
internal sealed class MiningCadence {
    float credit, pending, period;
    internal int Begin(float exact){
        exact=Math.Clamp(exact,1,600);
        if(Math.Abs(period-exact)>.0001f){credit=0;period=exact;}
        int ticks=Math.Max(1,(int)Math.Ceiling(exact-credit));
        pending=Math.Clamp(credit+ticks-exact,0,.999999f);return ticks;
    }
    internal void Commit()=>credit=pending;
}
