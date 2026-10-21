using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
namespace PalicoCompanion.Systems;
public static class Logistics
{
    public static Vector2 Center(Chest chest)=>new(chest.x*16+16,chest.y*16+16);
    public static Chest At(int x,int y) {
        if(!WorldGen.InWorld(x,y,10))return null;
        int index=Chest.FindChest(x,y);
        var tile=Main.tile[x,y];
        if(index<0 || !tile.HasTile || (tile.TileType!=TileID.Containers&&tile.TileType!=TileID.Containers2) || Chest.IsLocked(x,y))return null;
        return Main.chest[index];
    }
    static Chest Authorized(Player p,CompanionPlayer s,Chest target) {
        if(Main.netMode==NetmodeID.MultiplayerClient || s.Paused || s.Cat is not {} cat||Vector2.DistanceSquared(s.WorkAnchor,cat.Center)>400*400)return null;
        bool Valid(Chest c)=>c!=null&&!Main.player.Any(other=>other.active&&other.chest>=0&&other.chest<Main.maxChests&&ReferenceEquals(Main.chest[other.chest],c))&&s.Chests.Allows(c.x,c.y)&&At(c.x,c.y)==c&&Vector2.DistanceSquared(cat.Center,Center(c))<=100*100&&Reachability.Clear(cat.Center,Center(c));
        // A running visit supplies exactly its selected chest, never whichever
        // chest happens to be nearest when the transfer completes.
        return target!=null?(Valid(target)?target:null):s.Chests.Available().Where(Valid).OrderBy(c=>Vector2.DistanceSquared(cat.Center,Center(c))).FirstOrDefault();
    }
    public static int Store(Player p,CompanionPlayer s,Chest target=null) {
        var chest=Authorized(p,s,target);if(chest==null){s.Status="Walk with cat to authorized chest; unpause";return 0;}
        int count=0;
        foreach(var item in s.Bag) {
            if(item.IsAir||item.favorited||CompanionRestock.Keep(item,s))continue;
            // Match existing types only: explicit chest authorization alone is not a sorting rule.
            if(chest.item.Any(i=>!i.IsAir&&i.type==item.type))count+=InventoryTransfer.Move(item,chest.item);
        }
        Sync(chest);s.Status=$"Stored {count} items matching chest contents";return count;
    }
    public static int Supply(Player p,CompanionPlayer s,Chest target=null) {
        var chest=Authorized(p,s,target);if(chest==null){s.Status="Walk with cat to authorized chest; unpause";return 0;}
        int count=0;
        foreach(var item in CompanionRestock.Candidates(s,chest)) {
            int wanted=CompanionRestock.Wanted(s,item);
            if(wanted>0){PickupCredit.Clear(item);count+=InventoryTransfer.Move(item,CompanionRestock.Destination(s,item),wanted);}
        }
        Sync(chest);s.Status=CompanionText.Message("Logistics.Restocked" ,count);return count;
    }
    static void Sync(Chest chest){if(!CompanionNetwork.Server)return;int index=Chest.FindChest(chest.x,chest.y);for(int i=0;i<chest.item.Length;i++)NetMessage.SendData(MessageID.SyncChestItem,-1,-1,null,index,i);}
    public static bool IsSupply(int type)=>type==ItemID.Torch||type==ItemID.WoodPlatform||type==ItemID.Rope;
}
