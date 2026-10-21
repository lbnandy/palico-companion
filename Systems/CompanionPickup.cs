using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Same local attraction idea and feedback as Player.GrabItems/GetItem. Storage
// remains transactional to the cat bag; never invoke player pickup side effects.
public sealed class CompanionPickup {
    // Stable exposes this as private; cache its actual implementation once.
    // This isolated anchor never enters Main.player and owns no inventory.
    static readonly Action<Player,Item,float> PullNative=typeof(Player).GetMethod("PullItem_Common",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate<Action<Player,Item,float>>();
    readonly Player anchor=new();
    static readonly Dictionary<Type,bool> ordinaryTypes=new();
    internal static bool Ordinary(Item item) {
        if(item.ModItem==null)return true;
        var type=item.ModItem.GetType();
        if(!ordinaryTypes.TryGetValue(type,out bool ordinary)) {
            // Local-player pickup effects (hearts, experience, custom currencies)
            // must not be converted into bag items or executed on a server proxy.
            ordinary=type.GetMethod(nameof(ModItem.OnPickup),[typeof(Player)])?.DeclaringType==typeof(ModItem)&&
                type.GetMethod(nameof(ModItem.ItemSpace),[typeof(Player)])?.DeclaringType==typeof(ModItem);
            ordinaryTypes[type]=ordinary;
        }
        return ordinary;
    }
    // A short working-radius bonus, not a larger roaming/search radius.
    public static int GrabRange=>Player.defaultItemGrabRange+24;
    internal int Range(Item item){int range=GrabRange;ItemLoader.GrabRange(item,anchor,ref range);return Math.Max(0,range);}
    public int Collected {get;private set;}
    public int Popups {get;private set;}
    public int SoundBatches {get;private set;}
    public int PulledThisTick {get;private set;}
    public int DeferredTargets {get;private set;}
    Item tracked;float bestDistance;int stalled;
    bool fullReported;
    readonly Dictionary<int,(Item Item,int Type,Vector2 Position,ulong Until)> deferred=new();
    readonly Dictionary<int,(Item Item,int Type,ulong Until)> pulling=new();
    bool Attached(Item item,Rectangle tail,int slot) => tail.Intersects(item.Hitbox)&&
        pulling.TryGetValue(slot,out var link)&&ReferenceEquals(link.Item,item)&&
        link.Type==item.type&&Main.GameUpdateCount<link.Until;
    bool Waiting(Item item,int slot) {
        if(!deferred.TryGetValue(slot,out var saved))return false;
        if(!ReferenceEquals(saved.Item,item)||saved.Type!=item.type||Main.GameUpdateCount>=saved.Until||
            Vector2.DistanceSquared(saved.Position,item.Center)>32*32) {deferred.Remove(slot);return false;}
        return true;
    }
    // A dropped coin's center can fit six pixels above a floor; the cat is 28
    // pixels tall. Approach a free body position, then use native local pulling.
    public static bool FlightApproach(Projectile cat,Item item,out Vector2 goal) {
        goal=item.Center;float best=float.MaxValue;
        foreach(int dy in new[]{0,-8,-16,-24})foreach(int dx in new[]{0,-16,16}) {
            var candidate=item.Center+new Vector2(dx,dy);
            var reach=new Rectangle((int)(candidate.X-cat.width/2f),(int)(candidate.Y-cat.height/2f),cat.width,cat.height);
            reach.Inflate(GrabRange,GrabRange);
            if(!reach.Intersects(item.Hitbox)||!Reachability.Clear(candidate,item.Center)||!CompanionNavigation.Fits(candidate))continue;
            float score=Vector2.DistanceSquared(cat.Center,candidate)+(CompanionNavigation.ClearFlight(cat.Center,candidate)?0:160*160);
            if(score<best){best=score;goal=candidate;}
        }
        return best<float.MaxValue;
    }
    // Selection is not pickup: a fresh throw or an obstacle may delay taking
    // an item without making it disappear from a direct work request.
    internal static bool CanCollect(Item i,Player owner,CompanionPlayer state,bool requireRoom=true)=>
        i.active&&!i.IsAir&&Ordinary(i)&&!ItemID.Sets.IsAPickup[i.type]&&i.type!=ItemID.Heart&&i.type!=ItemID.Star&&
        !state.ExcludedPickup.Contains(i.type)&&!i.favorited&&
        (i.playerIndexTheItemIsReservedFor==255||i.playerIndexTheItemIsReservedFor==owner.whoAmI)&&
        Vector2.DistanceSquared(i.Center,state.WorkAnchor)<(480*480)&&(!requireRoom||InventoryTransfer.HasRoom(i,state.Bag))&&
        ItemLoader.CanPickup(i,owner);
    public static bool Eligible(Item i,Player owner,CompanionPlayer state,Projectile cat,bool requireRoom=true)=>
        CanCollect(i,owner,state,requireRoom)&&i.noGrabDelay==0&&i.shimmerTime==0&&(!i.shimmered||i.velocity.Length()<.2f)&&Reachability.Clear(cat.Center,i.Center);
    internal void BeginSelection(){tracked=null;stalled=0;deferred.Clear();}
    // A caught fish and a ground pickup enter the same bag and get the same
    // provenance/receipt. Sounds stay at the caller so a sweep can batch them.
    internal int Receive(Item item,CompanionPlayer state,Projectile cat){
        var display=item.Clone();PickupCredit.Set(item,item.stack*PickupCredit.Unit(item));
        var courtesy=item.GetGlobalItem<CompanionDropCourtesy>();ulong until=courtesy.until;courtesy.until=0;
        int moved;
        try{moved=InventoryTransfer.Move(item,state.Bag);}finally{PickupCredit.Clear(item);if(!item.IsAir)courtesy.until=until;}
        if(moved>0){
            display.Center=cat.Center;display.stack=moved;
            if(CompanionNetwork.Server)CompanionNetwork.PickupEffect(state,display,cat.Center);
            else if(PopupText.NewText(PopupTextContext.RegularItemPickup,display,moved)>=0)Popups++;
        }
        return moved;
    }
    public int Tick(Player owner,CompanionPlayer state,Projectile cat,ref Vector2 goal,bool pursue=true,Func<Item,bool> selection=null) {
        // Explicit selection is still bounded by Eligible's owner work radius.
        // The cat may start on the opposite side of that radius.
        float pursuitRange=selection==null?400:960;
        int target=-1,trailing=-1;float nearest=pursuitRange*pursuitRange,nearestTrailing=nearest;bool took=false,coin=false,valuable=false,blocked=false;PulledThisTick=0;
        anchor.position=cat.position;anchor.width=cat.width;anchor.height=cat.height;anchor.velocity=cat.velocity;
        // Fresh NewItem results can precede the update that assigns whoAmI.
        // Use the authoritative array slot even during that first frame.
        for(int slot=0;slot<Main.maxItems;slot++) {
            var item=Main.item[slot];
            if(!item.active||item.IsAir||selection!=null&&!selection(item)||selection==null&&CompanionDropCourtesy.Protected(item))continue;
            // Item-specific native hooks cover QoT and other mods without
            // copying the owner's accessories or running pickup side effects.
            int range=Range(item);Rectangle grab=cat.Hitbox;grab.Inflate(range,range);
            Rectangle tail=grab;tail.Inflate(32,32);
            bool inGrab=grab.Intersects(item.Hitbox),attached=Attached(item,tail,slot);
            if(inGrab&&item.active&&!item.IsAir&&!InventoryTransfer.HasRoom(item,state.Bag)&&Eligible(item,owner,state,cat,false))blocked=true;
            if(!pursue&&!inGrab&&!attached)continue;
            if(!inGrab&&!attached&&Vector2.DistanceSquared(item.Center,cat.Center)>pursuitRange*pursuitRange)continue;
            bool eligible=Eligible(item,owner,state,cat);
            if(!eligible){
                if(selection!=null&&CanCollect(item,owner,state)){
                    float d=Vector2.DistanceSquared(item.Center,cat.Center);
                    if(d<nearest){nearest=d;target=slot;}
                }
                continue;
            }
            // Contact can collect even a deferred item; only pursuing/pulling waits.
            if(!cat.Hitbox.Intersects(item.Hitbox)&&Waiting(item,slot))continue;
            if(cat.Hitbox.Intersects(item.Hitbox)) {
                bool isCoin=item.IsACoin,isValuable=item.rare>ItemRarityID.White;int moved=Receive(item,state,cat);
                if(item.IsAir)item.active=false;
                if(moved>0) {
                    if(CompanionNetwork.Server)NetMessage.SendData(MessageID.SyncItem,-1,-1,null,slot);
                    Collected+=moved;took=true;coin|=isCoin;valuable|=isValuable;
                    state.Experience++;cat.ai[1]=25;
                }
                if(!item.active||!InventoryTransfer.HasRoom(item,state.Bag))continue;
            }
            bool attracting=inGrab||attached;
            if(attracting) {
                // A small, bounded retention band lets a moving collector keep
                // pulling its own trailing items; never grants new distant grabs.
                if(inGrab)pulling[slot]=(item,item.type,Main.GameUpdateCount+120);
                item.beingGrabbed=true;item.shimmered=false;PulledThisTick++;
                if(!ItemLoader.GrabStyle(item,anchor))PullNative(anchor,item,.75f);
                if(CompanionNetwork.Server&&Main.GameUpdateCount%10==0)NetMessage.SendData(MessageID.SyncItem,-1,-1,null,slot);
            }
            float distance=Vector2.DistanceSquared(item.Center,cat.Center);
            if(pursue&&!attracting&&distance<nearest){nearest=distance;target=slot;}
            if(pursue&&attracting&&distance<nearestTrailing){nearestTrailing=distance;trailing=slot;}
        }
        if(!blocked)fullReported=false;
        else if(!fullReported)fullReported=state.Expression.TrySay(state,cat,"Working",Terraria.GameContent.UI.EmoteID.EmoteConfused,variant:"BagFull");
        if(took){CompanionFeedback.Show(5,cat.Center,coin?1:0);SoundBatches++;if(valuable)state.Expression.Say(state,cat,"Loot",Terraria.GameContent.UI.EmoteID.EmoteHappiness);}
        // Finish the trailing items only after there is no next unswept pile.
        bool finishing=target<0;
        if(finishing)target=trailing;
        if(target>=0) {
            var item=Main.item[target];float distance=Vector2.Distance(cat.Center,item.Center);
            if(!ReferenceEquals(tracked,item)){tracked=item;bestDistance=distance;stalled=0;}
            if(distance<bestDistance-4){bestDistance=distance;stalled=0;}
            if(item.noGrabDelay>0)stalled=0;
            if(++stalled>=120) {
                deferred[target]=(item,item.type,item.Center,Main.GameUpdateCount+600);
                DeferredTargets++;tracked=null;stalled=0;state.Status=CompanionText.Message("CompanionPickup.LootBlocked");
                return -1;
            }
            goal=finishing?cat.Center:item.Center;
            if((selection!=null||((CompanionCat)cat.ModProjectile).Navigation.Riding)&&goal!=cat.Center&&FlightApproach(cat,item,out var approach))goal=approach;
            state.Status=CompanionText.Message("CompanionPickup.PickingUp")+CompanionText.MessageFull(Lang.GetItemName(item.type).Key);
        }
        else {tracked=null;stalled=0;}
        return target;
    }
}
