using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Opportunistic shore fishing. Native bobber AI and native reel/bait/loot logic;
// only choosing a nearby pond and deciding when to reel are companion policy.
public sealed class CompanionFishing {
    int Slot=>Main.netMode==NetmodeID.SinglePlayer?253:255;
    static readonly Func<Player,bool,bool> Reel=typeof(Player).GetMethod("ItemCheck_CheckFishingBobbers",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate<Func<Player,bool,bool>>();
    static readonly Action<Player,Item> HoldStyle=typeof(Player).GetMethod("ItemCheck_HackHoldStyles",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate<Action<Player,Item>>();
    static readonly Action<Player,float,Item,Rectangle> HoldPose=typeof(Player).GetMethod("ItemCheck_ApplyHoldStyle",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate<Action<Player,float,Item,Rectangle>>();
    delegate void PondQuery(int x,int y,out bool lava,out bool honey,out int water,out int chum);
    static readonly PondQuery Pond=typeof(Projectile).GetMethod("GetFishingPondState",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<PondQuery>();
    public readonly Player Actor=new();
    public static CompanionFishing Current {get;private set;}
    public Projectile Cat {get;private set;}
    public CompanionPlayer State {get;private set;}
    public Projectile Bobber {get;private set;}
    public Item Rod {get;private set;}
    public Vector2 Aim {get;private set;}
    internal Point? RequestedWater;
    // Native fishing counts any non-solid liquid cell, including a partial
    // surface row. Accept a click just above the rendered waves as well.
    internal static bool TrySurface(Point cell,out Point surface){
        surface=cell;if(!WorldGen.InWorld(cell.X,cell.Y,10))return false;
        for(int down=0;down<=2;down++){
            int y=cell.Y+down;if(!WorldGen.InWorld(cell.X,y,10)||WorldGen.SolidTile(cell.X,y))return false;
            var tile=Main.tile[cell.X,y];
            if(tile.LiquidAmount==0)continue;
            if(tile.LiquidType==LiquidID.Shimmer)return false;
            while(y>10&&Main.tile[cell.X,y-1].LiquidAmount>0&&!WorldGen.SolidTile(cell.X,y-1))y--;
            surface=new(cell.X,y);return true;
        }
        return false;
    }
    internal bool CanRequest(CompanionPlayer s,Point cell){
        var old=RequestedWater;var oldState=State;var oldCat=Cat;var oldRod=Rod;var oldBait=bait;var oldGoal=MoveTarget;bool approaching=Approaching;
        try {RequestedWater=cell;State=s;Cat=s.Cat;Rod=BestRod(s.Player);bait=s.Bag.Where(SafeBait).OrderByDescending(i=>i.bait).FirstOrDefault();return bait!=null&&(FindPond(CompanionSprite.Hand(Cat),s.Player,out _)||FindShore(Cat,s.Player));}
        finally {RequestedWater=old;State=oldState;Cat=oldCat;Rod=oldRod;bait=oldBait;MoveTarget=oldGoal;Approaching=approaching;}
    }
    int idle,age,recast,biteDelay,gesture,gestureMax;Vector2 visitAnchor;
    public bool Approaching {get;private set;}
    public Vector2 MoveTarget {get;private set;}
    internal static bool OwnerFishing(Player p)=>Main.projectile.Any(q=>q.active&&q.owner==p.whoAmI&&q.bobber&&CompanionFishingBobber.Of(q)==null);
    internal static bool LiquidAllowed(int liquid,Item rod,Item bait,Player p)=>liquid==LiquidID.Water||liquid==LiquidID.Honey||liquid==LiquidID.Lava&&(ItemID.Sets.CanFishInLava[rod.type]||ItemID.Sets.IsLavaBait[bait.type]||p.accLavaFishing);
    public bool Gesture=>Active&&gesture>0;
    public float GestureProgress=>gestureMax>0?1-gesture/(float)gestureMax:0;
    Item bait;
    public int Casts {get;private set;}
    public int Catches {get;internal set;}
    public int BaitConsumed {get;private set;}
    public int Draws {get;private set;}
    internal int LineDraws;
    internal readonly System.Collections.Generic.List<Item> OverflowDrops=new();
    readonly List<Item> waitingOverflow=new();
    readonly Queue<int> catchAnnouncements=new();
    internal void Caught(Item item)=>catchAnnouncements.Enqueue(item.type);
    public bool Active=>Bobber is {active:true}&&CompanionFishingBobber.Of(Bobber)==this;
    public bool Available=>Cat is {active:true}&&State is {Enabled:true,Paused:false}&&!State.Vitals.Downed&&!State.Player.dead&&(RequestedWater.HasValue||CompanionNetwork.Client||CompanionNetwork.Server&&State.NetFishing||!CompanionNetwork.Server&&ModContent.GetInstance<CompanionConfig>().Fishing);
    public static Item BestRod(Player p) {
        Item best=new(ItemID.WoodFishingPole);
        foreach(var i in p.inventory.Take(50))if(!i.IsAir&&i.fishingPole>best.fishingPole&&StandardRod(i))best=i;
        return best;
    }
    internal static bool StandardRod(Item item)=>Terraria.ID.ContentSamples.ProjectilesByType.TryGetValue(item.shoot,out var shot)&&shot.bobber;
    public static bool SafeBait(Item i)=>!i.IsAir&&!i.favorited&&i.bait>0&&i.type!=ItemID.TruffleWorm;
    internal bool WaitingForOverflow(CompanionPlayer s){
        waitingOverflow.RemoveAll(i=>!i.active||i.IsAir||!Main.item.Contains(i));
        return waitingOverflow.Any(i=>!InventoryTransfer.HasRoom(i,s.Bag));
    }
    public static int WaterCount(Point cell){Pond(cell.X,cell.Y,out bool lava,out bool honey,out int water,out _);return lava||honey?0:water;}
    public void Cancel(){if(Active){Bobber.ai[1]=0;if(CompanionNetwork.Server)NetMessage.SendData(MessageID.KillProjectile,-1,-1,null,Bobber.identity,Bobber.owner);Bobber.active=false;}Bobber=null;Approaching=false;idle=age=biteDelay=gesture=0;recast=30;}
    internal void BindReplica(Projectile cat,Projectile bobber,int rod){Cat=cat;State=Main.player[CompanionCat.OwnerOf(cat)].GetModPlayer<CompanionPlayer>();Bobber=bobber;if(Rod?.type!=rod)Rod=new Item(rod);Actor.inventory[0]=Rod;Prepare();}
    public void Maintain(Player p,CompanionPlayer s,Projectile cat){
        Cat=cat;State=s;
        // Native bobber callbacks temporarily replace Main.myPlayer with the
        // fishing actor. Announce completed catches on the normal owner tick.
        while(catchAnnouncements.TryDequeue(out int type)){
            var item=ContentSamples.ItemsByType[type];
            State.Expression.TrySay(State,Cat,"FishCaught",Terraria.GameContent.UI.EmoteID.EmoteHappiness,
                context:CompanionText.MessageFull(Lang.GetItemName(type).Key),
                variant:item.questItem?"Quest":ItemID.Sets.IsFishingCrate[type]?"Crate":null);
        }
        if(!Available||s.Digging.Active||s.Mining.Active||s.Order.Active||s.Construction.Active||s.Storage.Active||s.Trees.Active||s.Garden.Active||
            Vector2.DistanceSquared(p.Center,Active?Aim:cat.Center)>640*640||CompanionThreat.WorkDanger(p,cat.Center))Cancel();
    }
    public bool Tick(Player p,CompanionPlayer s,Projectile cat){
        if(gesture>0)gesture--;
        Cat=cat;State=s;
        if(!Available||(Main.netMode==NetmodeID.SinglePlayer&&Main.player[Slot].active)){Cancel();return false;}
        if(!Active){
            if(recast>0){recast--;return false;}
            if(idle==0||Vector2.DistanceSquared(visitAnchor,p.Center)>128*128){visitAnchor=p.Center;idle=0;}
            idle++;bool ownerFishing=OwnerFishing(p);
            if(!RequestedWater.HasValue&&!ownerFishing&&idle<120)return false;
            if(Approaching&&Vector2.DistanceSquared(MoveTarget,p.Center)>480*480)Approaching=false;
            if(idle%(ownerFishing?15:30)!=0){
                // A completed reel leaves a brief gap before the next cast.
                // Keep the shore position during that gap while the owner fishes.
                if(!Approaching&&ownerFishing&&Bobber is {active:false}&&bait is {IsAir:false}&&SafeBait(bait)&&s.Bag.Contains(bait)&&!WaitingForOverflow(s)){
                    MoveTarget=cat.Center;((CompanionCat)cat.ModProjectile).FaceWork(Aim);HoldPosition(cat);return true;
                }
                return Approaching;
            }
            bait=s.Bag.Where(SafeBait).OrderByDescending(i=>i.bait).FirstOrDefault();
            if(bait==null||WaitingForOverflow(s)){Approaching=false;return false;}
            Rod=BestRod(p).Clone();
            if(!FindPond(CompanionSprite.Hand(cat),p,out var target)){
                Approaching=FindShore(cat,p);if(Approaching)s.Status=CompanionText.Message("CompanionFishing.ApproachingShore");return Approaching;
            }
            if(cat.velocity.LengthSquared()>1){cat.velocity*=.5f;Approaching=true;MoveTarget=cat.Center;return true;}
            Approaching=false;
            Aim=target;cat.direction=target.X<cat.Center.X?-1:1;Prepare();
            Actor.inventory=InventoryTransfer.Empty(59);Actor.inventory[0]=Rod;Actor.inventory[54]=bait;
            // Predict the same free-flight drag/gravity used by AI_061.
            Vector2 from=CompanionSprite.Hand(cat),delta=target-from;
            const int flight=35;
            float horizontal=(float)(.98*(1-Math.Pow(.98,flight))/.02);
            Vector2 velocity=new(delta.X/horizontal,(delta.Y-.1f*flight*(flight+1))/flight);
            using(new Scope(this)){
                int id=Projectile.NewProjectile(cat.GetSource_FromAI(),from,velocity,Rod.shoot,0,0,Slot);
                if(id>=Main.maxProjectiles)return false;
                Bobber=Main.projectile[id];Bobber.GetGlobalProjectile<CompanionFishingBobber>().Context=this;
            }
            age=biteDelay=0;gesture=gestureMax=24;Casts++;CompanionItemVisual.BeginUse(Rod,from);
        }
        if(Bobber.ai[0]==0&&(!SafeBait(bait)||!s.Bag.Contains(bait))){Cancel();return false;}
        using(new Scope(this)){Actor.controlUseItem=false;Actor.ItemCheck();}
        if(++age>3600||age>150&&!Bobber.wet&&Bobber.ai[0]==0){Cancel();return false;}
        // Negative localAI is a caught NPC, not an item. Release instead of
        // automatically bringing Blood Moon enemies or bosses onto the owner.
        if(Bobber.ai[1]<0&&Bobber.localAI[1]<0){Cancel();return false;}
        if(Bobber.ai[0]==0&&Bobber.ai[1]<0&&Bobber.localAI[1]>0){
            if(++biteDelay>=12){
                int before=bait.stack;Prepare();
                using(new Scope(this))Reel(Actor,true);gesture=gestureMax=18;
                BaitConsumed+=Math.Max(0,before-(bait.IsAir?0:bait.stack));biteDelay=0;
            }
        }else biteDelay=0;
        ((CompanionCat)cat.ModProjectile).FaceWork(Aim);
        HoldPosition(cat);
        s.Status=Bobber.ai[0]>0?CompanionText.Message("CompanionFishing.ReelingIn"):CompanionText.Message("CompanionFishing.WaitingForBite");
        return true;
    }
    internal static void HoldPosition(Projectile cat){
        // A cast owns its position, including a hovering kite. Grounded casts
        // keep their normal vertical collision; flight brakes both axes.
        ((CompanionCat)cat.ModProjectile).Navigation.HoldFishingPosition(cat);
    }
    bool FindPond(Vector2 from,Player p,out Vector2 goal){
        goal=default;var origin=RequestedWater??from.ToTileCoordinates();float best=float.MaxValue;Item bestRod=Rod,bestBait=bait;
        for(int dx=-18;dx<=18;dx++)for(int dy=-2;dy<=20;dy++){
            if(!RequestedWater.HasValue&&Math.Abs(dx)<3)continue;
            if(RequestedWater.HasValue&&(Math.Abs(dx)>3||Math.Abs(dy)>1))continue;int x=origin.X+dx,y=origin.Y+dy;if(!WorldGen.InWorld(x,y,10))continue;
            var t=Main.tile[x,y];var above=Main.tile[x,y-1];
            if(WorldGen.SolidTile(x,y)||t.LiquidType==LiquidID.Shimmer||t.LiquidAmount==0||WorldGen.SolidTile(x,y-1)||above.LiquidAmount>0)continue;
            Vector2 at=new(x*16+8,y*16+16-t.LiquidAmount/255f*16+2);
            if(Vector2.DistanceSquared(at,p.Center)>320*320||Vector2.DistanceSquared(at,from)>320*320||!Reachability.Clear(from,at-new Vector2(0,8)))continue;
            float d=Vector2.DistanceSquared(at,from);if(d>=best)continue;
            Item candidateRod=Rod,candidateBait=bait;
            if(t.LiquidType==LiquidID.Lava&&!LiquidAllowed(t.LiquidType,candidateRod,candidateBait,p)){
                candidateBait=State.Bag.Where(SafeBait).Where(i=>ItemID.Sets.IsLavaBait[i.type]).OrderByDescending(i=>i.bait).FirstOrDefault()??bait;
                candidateRod=p.inventory.Take(50).Where(i=>!i.IsAir&&i.fishingPole>0&&StandardRod(i)&&ItemID.Sets.CanFishInLava[i.type]).OrderByDescending(i=>i.fishingPole).FirstOrDefault()??Rod;
            }
            if(!LiquidAllowed(t.LiquidType,candidateRod,candidateBait,p))continue;
            Pond(x,y,out _,out _,out int count,out _);if(count<75)continue;
            best=d;goal=at;bestRod=candidateRod;bestBait=candidateBait;
        }
        if(best==float.MaxValue)return false;Rod=bestRod.Clone();bait=bestBait;return true;
    }
    bool FindShore(Projectile cat,Player p){
        if(!FindPond(p.Center,p,out var water))return false;
        var tile=water.ToTileCoordinates();float best=float.MaxValue;Vector2 goal=default;
        for(int dx=-18;dx<=18;dx+=3)for(int dy=-8;dy<=0;dy+=2){
            Vector2 candidate=new((tile.X+dx)*16+8,(tile.Y+dy)*16-CompanionNavigation.Height/2f);
            if(!CompanionNavigation.TryStand(candidate,out candidate)||Vector2.DistanceSquared(candidate,p.Center)>480*480||Vector2.DistanceSquared(candidate,water)>288*288||!Reachability.Clear(candidate-new Vector2(0,6),water-new Vector2(0,8)))continue;
            float distance=Vector2.DistanceSquared(candidate,cat.Center);if(distance<best){best=distance;goal=candidate;}
        }
        if(best==float.MaxValue)return false;MoveTarget=goal;return true;
    }
    void Prepare(){
        Actor.whoAmI=Slot;Actor.itemLocation+=CompanionSprite.Hand(Cat)-Actor.Center;Actor.Center=CompanionSprite.Hand(Cat);Actor.direction=Cat.direction;Actor.gravDir=1;
        Actor.accLavaFishing=State.Player.accLavaFishing;
        Actor.dead=false;Actor.selectedItem=0;Actor.itemRotation=0;Actor.heldProj=-1;
        Actor.fishingSkill=State.Player.fishingSkill+(State.Training.Work(10)?5*(State.Training.Tier+1):0);
        var p=State.Player;Actor.name=p.name;Actor.ZoneCorrupt=p.ZoneCorrupt;Actor.ZoneCrimson=p.ZoneCrimson;
        Actor.ZoneJungle=p.ZoneJungle;Actor.ZoneSnow=p.ZoneSnow;Actor.ZoneHallow=p.ZoneHallow;Actor.ZoneDesert=p.ZoneDesert;
    }
    readonly List<DrawData> cache=new();
    public Vector2 DrawShift(Color light){
        cache.Clear();
        var info=new PlayerDrawSet{drawPlayer=Actor,heldItem=Rod,DrawDataCache=cache,Position=Actor.position,ItemLocation=Actor.itemLocation,
            itemColor=light,itemEffect=Cat.direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None};
        PlayerDrawLayers.DrawPlayer_27_HeldItem(ref info);
        return cache.Count==0?Vector2.Zero:CompanionSprite.Hand(Cat)-Main.screenPosition-CompanionGrip.DrawnGrip(cache[0]);
    }
    public void Draw(Color light){
        if(!Active||Rod==null)return;
        using var scope=new Scope(this);
        PrepareVisual();
        var c=(CompanionCat)Cat.ModProjectile;var hand=CompanionSprite.Hand(Cat);
        Vector2 shift=DrawShift(light);
        foreach(var entry in cache){var draw=entry;draw.position+=shift;draw.Draw(Main.spriteBatch);}
        CompanionItemVisual.DrawGrip(Main.spriteBatch,Cat.Bottom+new Vector2(0,Cat.gfxOffY)-Main.screenPosition,c.Animation.SpriteFrame,Cat.direction,light);
        Draws++;
    }
    internal void PrepareVisual(){
        // Remote replicas do not run ItemCheck, which can reel/consume bait.
        // These native visual-only steps restore holdStyle and the rod tip.
        if(CompanionNetwork.Client&&Rod!=null){HoldStyle(Actor,Rod);HoldPose(Actor,0,Rod,Actor.GetItemDrawFrame(Rod.type));Actor.lastVisualizedSelectedItem=Rod;}
    }
    public sealed class Scope:IDisposable{
        readonly CompanionFishing c,previous;readonly Player backup;readonly int mine;readonly CompanionProjectileScope projectiles;
        public Scope(CompanionFishing context){
            c=context;previous=Current;backup=Main.player[c.Slot];mine=Main.myPlayer;projectiles=new CompanionProjectileScope(c.Slot,p=>CompanionFishingBobber.Of(p)==c);c.Prepare();Main.player[c.Slot]=c.Actor;c.Actor.active=true;Main.myPlayer=CompanionNetwork.Client?mine:c.Slot;Current=c;
        }
        public void Dispose(){
            projectiles.Dispose();Main.player[c.Slot]=backup;Main.myPlayer=mine;c.Actor.active=false;Current=previous;
            // Native full-inventory drops are reserved to the scoped local actor.
            foreach(var item in c.OverflowDrops){
                int index=Array.IndexOf(Main.item,item);if(index<0||!item.active)continue;
                if(!c.waitingOverflow.Contains(item))c.waitingOverflow.Add(item);
                item.playerIndexTheItemIsReservedFor=c.State.Player.whoAmI;
                if(CompanionNetwork.Server)NetMessage.SendData(MessageID.ItemOwner,-1,-1,null,index);
            }
            c.OverflowDrops.Clear();
        }
    }
}
public sealed class CompanionFishingBobber:GlobalProjectile{
    public override bool InstancePerEntity=>true;
    public CompanionFishing Context;
    int identity=-1,rod,bondOwner,wait;ulong instance;
    public override void OnSpawn(Projectile p,IEntitySource source){if(CompanionFishing.Current is {} c&&p.bobber&&p.owner==c.Actor.whoAmI)Context=c;}
    public override void SendExtraAI(Projectile p,Terraria.ModLoader.IO.BitWriter bits,System.IO.BinaryWriter w){bits.WriteBit(Context!=null);if(Context!=null){w.Write(Context.State.Player.whoAmI);w.Write(((CompanionCat)Context.Cat.ModProjectile).NetworkInstance);w.Write(Context.Cat.identity);w.Write(Context.Rod.type);}}
    public override void ReceiveExtraAI(Projectile p,Terraria.ModLoader.IO.BitReader bits,System.IO.BinaryReader r){
        if(bits.ReadBit()){int owner=r.ReadInt32();ulong token=r.ReadUInt64();int cat=r.ReadInt32(),item=r.ReadInt32();if(CompanionNetwork.Client){bondOwner=owner;instance=token;identity=cat;rod=item;Resolve(p);}}
        else if(CompanionNetwork.Client){Context=null;identity=-1;instance=0;wait=0;}
    }
    internal bool Resolve(Projectile p){
        if(identity<0)return true;
        if(Context?.Cat is {active:true,ModProjectile:CompanionCat bound}&&bound.NetworkInstance==instance)return true;
        Context=null;
        foreach(var cat in Main.projectile)if(cat.active&&cat.ModProjectile is CompanionCat c&&c.OwnerIndex==bondOwner&&c.NetworkInstance==instance){Context=c.Fishing;Context.BindReplica(cat,p,rod);wait=0;return true;}
        if(CompanionRoster.Retired(bondOwner,instance)||++wait>180)p.active=false;
        return false;
    }

    public static CompanionFishing Of(Projectile p)=>p.TryGetGlobalProjectile<CompanionFishingBobber>(out var g)?g.Context:null;
}
public sealed class CompanionFishingHooks:ModSystem{
    public override void Load(){On_Projectile.Update+=Update;On_Main.DrawProjDirect+=Draw;On_Player.GetItem+=GetItem;On_Main.DrawProj_FishingLine+=Line;}
    public override void Unload(){On_Projectile.Update-=Update;On_Main.DrawProjDirect-=Draw;On_Player.GetItem-=GetItem;On_Main.DrawProj_FishingLine-=Line;}
    static void Line(On_Main.orig_DrawProj_FishingLine orig,Projectile p,ref float x,ref float y,Vector2 center){
        var c=CompanionFishingBobber.Of(p);
        if(c!=null&&c.Available){center+=c.DrawShift(Lighting.GetColor(c.Cat.Center.ToTileCoordinates()));c.LineDraws++;}
        orig(p,ref x,ref y,center);
    }
    static Item GetItem(On_Player.orig_GetItem orig,Player p,int who,Item item,GetItemSettings settings){
        var c=CompanionFishing.Current;
        if(c==null||p!=c.Actor)return orig(p,who,item,settings);

        var remainder=item.Clone();int moved=((CompanionCat)c.Cat.ModProjectile).Pickup.Receive(remainder,c.State,c.Cat);c.Catches++;
        TrainingWorkFeedback.Trigger(c.State,10);
        // GetItem was redirected to the cat bag; keep its successful pickup
        // cue on the same authoritative feedback path as ordinary collection.
        if(moved>0)CompanionFeedback.Show(5,c.Cat.Center,item.IsACoin?1:0);
        c.Caught(item);
        return remainder; // Native GiveItemToPlayer drops any remainder, never discard it.
    }
    static void Update(On_Projectile.orig_Update orig,Projectile p,int index){
        if(p.TryGetGlobalProjectile<CompanionFishingBobber>(out var tag)&&!tag.Resolve(p))return;
        var c=CompanionFishingBobber.Of(p);if(c==null){orig(p,index);return;}
        if(!c.Available||Main.netMode==NetmodeID.SinglePlayer&&Main.player[c.Actor.whoAmI].active){p.ai[1]=0;p.active=false;return;}
        using(new CompanionFishing.Scope(c))orig(p,index);
        if(CompanionNetwork.Server&&p.active&&Main.GameUpdateCount%6==0)NetMessage.SendData(MessageID.SyncProjectile,-1,-1,null,p.whoAmI);
    }
    static void Draw(On_Main.orig_DrawProjDirect orig,Main self,Projectile p){
        var c=CompanionFishingBobber.Of(p);if(c==null){orig(self,p);return;}
        if(!c.Available)return;using(new CompanionFishing.Scope(c)){c.PrepareVisual();orig(self,p);}
    }
}




// Track the actual native overflow source, not every unreserved item nearby.
public sealed class CompanionFishingOverflow:GlobalItem {
    public override void OnSpawn(Item item,IEntitySource source){
        if(!CompanionNetwork.Client&&source is EntitySource_OverfullInventory overflow&&CompanionFishing.Current is {} fishing&&ReferenceEquals(overflow.Player,fishing.Actor))
            fishing.OverflowDrops.Add(item);
    }
}
