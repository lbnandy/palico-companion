using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Carry the existing world item, never bank it or synthesize healing. The real
// player's GrabItems/PickupItem owns consumption, healing, sound and combat text.
public sealed class CompanionSupplies {
    Item item; int slot=-1,type,age,scanClock;
    bool carrying; Vector2 lastCat;
    public bool Active=>item!=null;
    public bool Carrying=>carrying;
    public int Carried {get;private set;}
    public int Delivered {get;private set;}
    public static bool Needed(Item i,Player p)=>i.type is ItemID.Heart or ItemID.CandyApple or ItemID.CandyCane
        ?p.statLife<p.statLifeMax2:i.type is ItemID.Star or ItemID.SoulCake or ItemID.SugarPlum?p.statMana<p.statManaMax2:false;
    bool Same=>slot>=0&&ReferenceEquals(Main.item[slot],item)&&item.active&&item.type==type&&!item.IsAir;
    public void Cancel(){
        if(Same&&carrying){
            item.beingGrabbed=false;item.velocity=Vector2.Zero;
            item.GetGlobalItem<CompanionCarriedItem>().Release();
            if(CompanionNetwork.Server)NetMessage.SendData(MessageID.SyncItem,-1,-1,null,slot);
        }
        item=null;slot=-1;carrying=false;age=0;
    }
    public bool Tick(Player p,CompanionPlayer s,Projectile cat,ref Vector2 goal) {
        if(!(CompanionNetwork.Server?s.NetSupplies:ModContent.GetInstance<CompanionConfig>().DeliverRecoveryDrops)||s.Paused||s.Vitals.Downed||p.dead){Cancel();return false;}
        bool Eligible(Item i)=>!s.ExcludedPickup.Contains(i.type)&&i.active&&!i.IsAir&&i.ModItem==null&&Needed(i,p)&&i.noGrabDelay==0&&i.shimmerTime==0&&!i.shimmered&&!Main.projectile.Any(q=>q.active&&q.ModProjectile is CompanionCat other&&!ReferenceEquals(other.Supplies,this)&&ReferenceEquals(other.Supplies.item,i))&&
            (i.playerIndexTheItemIsReservedFor==255||i.playerIndexTheItemIsReservedFor==p.whoAmI)&&
            Vector2.DistanceSquared(i.Center,p.Center)<480*480&&p.CanAcceptItemIntoInventory(i)&&ItemLoader.CanPickup(i,p);
        if(item!=null&&(!Same||!Eligible(item)||++age>480))Cancel();
        if(item==null){
            if(++scanClock%15!=0)return false;
            float best=400*400;
            for(int n=0;n<Main.maxItems;n++){
                var drop=Main.item[n];float d=Vector2.DistanceSquared(drop.Center,cat.Center);
                if(d<best&&Eligible(drop)&&Reachability.Clear(cat.Center,drop.Center)&&CompanionPickup.FlightApproach(cat,drop,out _)){
                    best=d;item=drop;slot=n;type=drop.type;
                }
            }
        }
        if(item==null)return false;
        if(!carrying){
            if(!CompanionPickup.FlightApproach(cat,item,out goal)){Cancel();return false;}
            if(Vector2.DistanceSquared(cat.Center,item.Center)>32*32){s.Status=CompanionText.Message("CompanionSupplies.CollectingSupply");return true;}
            carrying=true;lastCat=cat.Center;Carried++;
            item.GetGlobalItem<CompanionCarriedItem>().Attach((CompanionCat)cat.ModProjectile);
            if(CompanionNetwork.Server)NetMessage.SendData(MessageID.SyncItem,-1,-1,null,slot);
        }
        goal=p.Center;s.Status=CompanionText.Message("CompanionSupplies.DeliveringSupply");
        if(cat.Hitbox.Intersects(p.Hitbox)&&Reachability.Clear(cat.Center,p.Center)){
            var deliveredItem=item;int deliveredSlot=slot;
            item.Center=p.Center;item.velocity=Vector2.Zero;
            if(CompanionNetwork.Server)item.playerIndexTheItemIsReservedFor=p.whoAmI;
            // Clear the attachment before the single hand-off packet. A second
            // live-item packet after pickup could make a consumed drop reappear.
            Cancel();
            if(CompanionNetwork.Server)NetMessage.SendData(MessageID.ItemOwner,-1,-1,null,deliveredSlot);
            if(cat.ModProjectile is CompanionCat visual)visual.ActionVisual.Begin(visual,CompanionGesture.RecoveryDeliver,type);
            Delivered++;
            bool life=deliveredItem.type is ItemID.Heart or ItemID.CandyApple or ItemID.CandyCane;
            s.Expression.TrySay(s,cat,"Delivered",life?Terraria.GameContent.UI.EmoteID.ItemLifePotion:Terraria.GameContent.UI.EmoteID.ItemManaPotion,context:CompanionText.MessageFull(Lang.GetItemName(deliveredItem.type).Key),variant:life?"Life":"Mana");
            return false;
        }
        return true;
    }
    public void UpdateCarried(Projectile cat){
        if(!carrying)return;
        if(!Same||Vector2.DistanceSquared(lastCat,cat.Center)>64*64||!Reachability.Clear(lastCat,cat.Center)){Cancel();return;}
        // The real drop follows the carrying paw; collision and pickup stay native.
        Vector2 at=CompanionCarriedItem.Position(cat);
        if(!Reachability.Clear(item.Center,at)||Collision.SolidCollision(at-new Vector2(item.width,item.height)*.5f,item.width,item.height)){Cancel();return;}
        item.Center=at;item.velocity=Vector2.Zero;item.beingGrabbed=true;lastCat=cat.Center;if(CompanionNetwork.Server&&Main.GameUpdateCount%6==0)NetMessage.SendData(MessageID.SyncItem,-1,-1,null,slot);
    }
}
