using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace PalicoCompanion.Systems;

// Attribute actual native harvest drops to one owner. Multiplayer uses the
// sender of the original tile-break packet; no nearest-player guessing.
internal sealed class HarvestBasket : IDisposable {
    static CompanionPlayer current;
    readonly CompanionPlayer previous;
    internal HarvestBasket(CompanionPlayer owner){previous=current;current=owner;}
    public void Dispose()=>current=previous;
    internal static void NotifyBenefit(){if(current!=null)TrainingWorkFeedback.Trigger(current,10);}
    internal static int Extra(int type,int count) {
        if(current==null||!current.Training.Work(10))return 0;
        float rate=IsWood(type)?new[]{.08f,.12f,.16f}[current.Training.Tier]:IsHerb(type)?new[]{.20f,.30f,.40f}[current.Training.Tier]:0;
        return rate>0?TrainingLoot.Bonus(count,rate):0;
    }
    internal static bool IsWood(int type)=>type is ItemID.Wood or ItemID.BorealWood or ItemID.PalmWood or ItemID.RichMahogany or ItemID.Ebonwood or ItemID.Shadewood or ItemID.Pearlwood or ItemID.AshWood;
    internal static bool IsHerb(int type)=>type is ItemID.Daybloom or ItemID.Moonglow or ItemID.Blinkroot or ItemID.Deathweed or ItemID.Waterleaf or ItemID.Fireblossom or ItemID.Shiverthorn;
}

public sealed class HarvestBasketOwners:ModSystem {
    public override void Load(){On_Player.ItemCheck+=Use;On_MessageBuffer.GetData+=Message;}
    public override void Unload(){On_Player.ItemCheck-=Use;On_MessageBuffer.GetData-=Message;}
    internal static bool Eligible(CompanionPlayer s)=>s.Training.Active(s)&&s.Training.Work(10);
    static void Use(On_Player.orig_ItemCheck orig,Player p){
        var s=p.GetModPlayer<CompanionPlayer>();
        if(Main.netMode==NetmodeID.SinglePlayer&&p.whoAmI==Main.myPlayer&&CompanionNativeUse.Current==null&&Eligible(s)){using(new HarvestBasket(s))orig(p);}else orig(p);
    }
    static void Message(On_MessageBuffer.orig_GetData orig,MessageBuffer buffer,int start,int length,out int messageType){
        // Merely establish attribution around Terraria's unchanged validation,
        // tile destruction and drop generation. Failed/effect-only hits drop nothing.
        if(CompanionNetwork.Server&&length>=9&&buffer.whoAmI>=0&&buffer.whoAmI<Main.maxPlayers&&buffer.readBuffer[start]==MessageID.TileManipulation&&buffer.readBuffer[start+1]==0){
            var p=Main.player[buffer.whoAmI];var s=p.GetModPlayer<CompanionPlayer>();
            // The original handler decides whether the harvest succeeds, including
            // modded tool reach. Passive equipment adds no second distance gate.
            if(p.active&&s.NetReady&&!s.NetLeaving&&Eligible(s)){using(new HarvestBasket(s))orig(buffer,start,length,out messageType);return;}
        }
        orig(buffer,start,length,out messageType);
    }
}

public sealed class HarvestBasketDrops : GlobalItem {
    public override void OnSpawn(Item item,IEntitySource source) {
        if(CompanionNetwork.Client||source is not EntitySource_TileBreak tile||!WorldGen.InWorld(tile.TileCoords.X,tile.TileCoords.Y))return;
        int original=Main.tile[tile.TileCoords].TileType;
        // Cutting a tree is different from breaking a placed wood block. Without
        // this distinction, place/break would produce unlimited extra wood.
        if(HarvestBasket.IsWood(item.type)&&TreeSpecies.SeedType(original)==0||HarvestBasket.IsHerb(item.type)&&original is not (TileID.MatureHerbs or TileID.BloomingHerbs))return;
        int extra=HarvestBasket.Extra(item.type,item.stack);
        if(extra>0&&Item.NewItem(new EntitySource_Misc("PalicoHarvestBasket"),item.Hitbox,item.type,extra)<Main.maxItems)HarvestBasket.NotifyBenefit();
    }
}
