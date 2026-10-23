using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// An isolated Player executes the game's actual use pipeline. The unused player
// index is visible ONLY in synchronous scopes; never replace the human player.
// Architecture reference: TerraGuardians' Player-based companion / masked update.
// Independently implemented; no third-party source copied.
public sealed partial class CompanionNativeUse {
    public readonly Player Actor=new();
    public Projectile Cat {get;private set;}
    public CompanionPlayer State {get;private set;}
    public Vector2 Aim {get;private set;}
    internal void PlacementAim(Vector2 aim)=>Aim=aim;
    internal Vector2 ReplicaItemOffset;
    CompanionPose replicaPose;
    internal void ReceivePose(CompanionPose pose){replicaPose=pose;ApplyReplicaPose();}
    void ApplyReplicaPose(){
        Actor.direction=replicaPose.ItemDirection==0?Cat?.direction??1:replicaPose.ItemDirection;
        Actor.itemAnimation=replicaPose.ItemAnimation;Actor.itemAnimationMax=replicaPose.ItemMax;
        Actor.itemRotation=replicaPose.ItemRotation;Actor.channel=replicaPose.Channel;
        ReplicaItemOffset=replicaPose.ItemOffset;Actor.itemLocation=Actor.Center+ReplicaItemOffset;
    }
    // Native projectile AI may temporarily change its owner's arm. Rendering
    // uses the buffered server pose, then returns that actor to simulation.
    internal readonly struct ReplicaDrawScope:IDisposable {
        readonly CompanionNativeUse use;
        readonly int direction,animation,maximum;
        readonly float rotation;readonly bool channel;readonly Vector2 location;
        internal ReplicaDrawScope(CompanionNativeUse context){
            use=CompanionNetwork.Client?context:null;
            var actor=context.Actor;direction=actor.direction;animation=actor.itemAnimation;maximum=actor.itemAnimationMax;
            rotation=actor.itemRotation;channel=actor.channel;location=actor.itemLocation;
            use?.ApplyReplicaPose();
        }
        public void Dispose(){if(use==null)return;var actor=use.Actor;actor.direction=direction;actor.itemAnimation=animation;actor.itemAnimationMax=maximum;actor.itemRotation=rotation;actor.channel=channel;actor.itemLocation=location;}
    }
    public int Uses {get;private set;}
    public int Shots {get;set;}
    public int Hits {get;set;}
    public int ManaSpent {get;private set;}
    public int ThrowablesSpent {get;private set;}
    public int ThrownHits {get;set;}
    public int ManaPotions {get;private set;}
    public int ManaRestored {get;private set;}
    internal float ReachFactor {get;private set;}=1;
    internal float FlailRange=100;
    int reachTarget=-1,missTicks,lastReachHits;
    CompanionNpcEpoch reachEpoch;
    internal bool PressedThisTick {get;private set;}
    void ObserveReach(Item weapon,NPC target,bool attacking) {
        int id=target?.whoAmI??-1;
        var epoch=target?.GetGlobalNPC<CompanionNpcEpoch>();
        if(id!=reachTarget||!ReferenceEquals(epoch,reachEpoch)){reachTarget=id;reachEpoch=epoch;ReachFactor=1;missTicks=0;lastReachHits=Hits;}
        if(Hits!=lastReachHits){lastReachHits=Hits;missTicks=0;}
        // Unknown/modded attacks can have shorter reach than their metadata.
        // Retry closer after complete unsuccessful cycles, preserving native
        // AI, damage and collision instead of enlarging an attack to fit.
        if(!attacking||target==null||!Busy||Summoning(weapon)||CompanionWeaponPolicy.Sentry(weapon)||CompanionWeaponPolicy.Rain(weapon)||
            weapon.channel&&CompanionCombatReach.Sample(weapon)?.aiStyle==ProjAIStyleID.Spear)return;
        if(++missTicks<Math.Max(180,weapon.useAnimation*3))return;
        missTicks=0;ReachFactor=Math.Max(Math.Min(1,64/CompanionCombatReach.Range(weapon,Cat)),ReachFactor*.75f);
    }
    internal bool WaitingForMana {get;private set;}
    internal void WaitForMana(){
        WaitingForMana=true;
        State.Expression.Say(State,Cat,"NoMana",Terraria.GameContent.UI.EmoteID.EmoteConfused);
    }
    bool depletedThrow;
    // Read the same tagged native projectiles on every peer. A client does not
    // run Tick/ItemCheck, so a Tick-only cached value never described its minions.
    public float SummonSlots {
        get {float slots=0;foreach(var p in Main.projectile)if(p.active&&p.minion&&CompanionOwnedShot.Of(p)==this)slots+=p.minionSlots;return slots;}
    }
    public int SentryCount {
        get {int count=0;foreach(var p in Main.projectile)if(p.active&&p.sentry&&CompanionOwnedShot.Of(p)==this)count++;return count;}
    }
    static readonly Action<Player> ResetProjectileCache=typeof(Player).GetMethod("ResetProjectileCaches",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate<Action<Player>>();
    static readonly Action<Player,int> UpdateProjectileCache=typeof(Player).GetMethod("UpdateProjectileCaches",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate<Action<Player,int>>();
    void CountOwnedProjectiles(){
        // Native caches also carry summon damage (e.g. Tiger and Abigail).
        // Counting types alone silently leaves those native damage fields zero.
        using(new Scope(this)){ResetProjectileCache(Actor);UpdateProjectileCache(Actor,Actor.whoAmI);}
    }
    public bool Busy=>Actor.itemAnimation>0;
    public static CompanionNativeUse Current {get;private set;}
    internal readonly CompanionHitCooldowns HitCooldowns=new();
    int type=-1,prefix=-1,slot=-1;
    internal bool EnsureActorSlot() {
        if(Main.netMode!=NetmodeID.SinglePlayer&&State!=null){slot=255;Actor.whoAmI=slot;return true;}
        if(CompanionActorSlots.Owns(this,slot))return true;
        // A joining player may take an inactive slot between ticks. Stop the
        // old native projectiles before changing owner indices; never hijack it.
        if(slot>=0){Cancel(preserveSkills:false);CompanionActorSlots.Release(this,slot);}
        slot=CompanionActorSlots.Acquire(this);
        if(slot<0)return false;
        Actor.whoAmI=slot;return true;
    }
    internal void ReleaseActorSlot(){Cancel(preserveSkills:false);CompanionActorSlots.Release(this,slot);slot=-1;}
    bool initialized;
    bool actorLocated;
    uint actorLocationTick;
    Vector2 lastActorPosition;
    void LocateActor(Projectile cat){
        Actor.Center=CompanionSprite.Hand(cat);Actor.velocity=cat.velocity;
        // BindReplica is also called by each projectile and draw pass. Keep the
        // same previous-frame position for every native trail in this update.
        if(!actorLocated||actorLocationTick!=Main.GameUpdateCount){
            Actor.oldPosition=actorLocated?lastActorPosition:Actor.position;
            actorLocationTick=Main.GameUpdateCount;actorLocated=true;
        }
        lastActorPosition=Actor.position;
    }
    public static int MinionLimit(Player owner)=>Math.Max(1,owner.maxMinions);
    public static int SentryLimit(Player owner)=>Math.Max(1,owner.maxTurrets);
    public static bool Summoning(Item item)=>item.shoot>ProjectileID.None&&ContentSamples.ProjectilesByType.TryGetValue(item.shoot,out var p)&&p.minion;
    public static bool DistanceWeapon(Item item)=>CompanionCombatReach.Distance(item);
    // Range chooses tactics, not arm animation. Projectile-emitting swords
    // still use vanilla's overhead swing; guns, staffs, yoyos and rapiers do not.
    internal static bool SwingArm(Item item)=>item.useStyle==ItemUseStyleID.Swing;
    internal bool Present=>Cat is {active:true}&&Cat.ModProjectile is CompanionCat c&&ReferenceEquals(c.Weapons,this)&&State is {Enabled:true,Paused:false}&&!State.Vitals.Downed&&!State.Player.dead;
    public bool Available=>Present&&State.Combat;
    internal bool CanMaintainShot(Projectile p)=>Present&&(State.Combat||p.ModProjectile is TrainingDart or TrainingBomb);
    internal void BindReplica(Player owner,CompanionPlayer state,Projectile cat){
        Cat=cat;State=state;EnsureActorSlot();
        if(!initialized){Actor.inventory=InventoryTransfer.Empty(59);initialized=true;}
        var item=CompanionWeapons.Active(state.Gear);if(Actor.inventory[0].type!=item.type||Actor.inventory[0].prefix!=item.prefix)Actor.inventory[0]=item.Clone();
        // ResetEffects clears the projectile caches too. Match Player.Update's
        // order: reset, rebuild caches, then update buffs which maintain minions.
        Actor.ResetEffects();CountOwnedProjectiles();
        using(new Scope(this))Actor.UpdateBuffs(Actor.whoAmI);Actor.maxMinions=MinionLimit(owner);Actor.maxTurrets=SentryLimit(owner);
        Actor.slotsMinions=0;Actor.numMinions=0;
        Actor.selectedItem=0;LocateActor(cat);ApplyReplicaPose();Actor.dead=false;
        Actor.statMana=state.Vitals.Mana;Actor.statManaMax2=state.Vitals.MaxMana;Actor.statLife=state.Vitals.Health;
        Actor.itemLocation=Actor.Center+ReplicaItemOffset;Actor.lastVisualizedSelectedItem=Actor.HeldItem.Clone();
    }
    public void Tick(Player owner,CompanionPlayer state,Projectile cat,NPC target,bool allowAttack) {
        Cat=cat;State=state;
        if(!EnsureActorSlot()){Cancel();return;}
        if(!initialized){Actor.inventory=InventoryTransfer.Empty(59);Actor.whoAmI=slot;Actor.ResetEffects();initialized=true;}
        if(!Available){Cancel();return;}
        Item weapon=CompanionWeapons.Active(state.Gear);
        CompanionRestock.Maintain(state);
        if(type!=weapon.type||prefix!=weapon.prefix) {
            Cancel(depletedThrow&&state.Gear[0].IsAir);depletedThrow=false;type=weapon.type;prefix=weapon.prefix;Actor.inventory[0]=weapon.Clone();
            ReachFactor=1;FlailRange=100;reachTarget=-1;missTicks=0;
            // Player.ItemCheckWrapped normally refreshes this while idle. A
            // companion must do the same before starting a newly equipped item.
            Actor.lastVisualizedSelectedItem=Actor.HeldItem.Clone();Actor.itemRotation=0;
        }
        // ModItem can own charge/reload state. Use the equipped instance so
        // re-equipping, saving and network snapshots cannot reset that state.
        if(weapon.ModItem!=null)Actor.inventory[0]=weapon;
        LocateActor(cat);
        // Rebuild temporary effects before applying buffs, as Player.Update
        // does. Additive effects must not accumulate on a persistent actor.
        Actor.ResetEffects();
        // Repeated AI attacks use vanilla's hold-to-repeat policy. ItemCheck
        // owns cadence, reuse delay and CanAutoReuseItem overrides; manually
        // waiting for animation zero introduces a blank frame between casts.
        Actor.autoReuseAllWeapons=true;
        // Native trailing mode 4 translates old trail points by player movement.
        // Leaving oldPosition at (0,0) stretches Zenith trails across the world.
        Actor.direction=cat.direction;Actor.gravDir=1;Actor.dead=false;Actor.selectedItem=0;
        Actor.statLife=state.Vitals.Health;Actor.statLifeMax=Actor.statLifeMax2=state.Vitals.MaxHealth;
        Actor.statMana=state.Vitals.Mana;Actor.statManaMax=Actor.statManaMax2=state.Vitals.MaxMana;
        if(weapon.consumable)Actor.inventory[0].stack=weapon.stack;
        Actor.maxMinions=MinionLimit(owner);Actor.maxTurrets=SentryLimit(owner);
        Actor.downedDD2EventAnyDifficulty=owner.downedDD2EventAnyDifficulty;
        Actor.manaCost=state.Training.Find(state,3)>=0&&state.Training.ManaDance>0&&state.Training.ManaTarget==1?.9f-.05f*state.Training.Tier:1;
        Actor.GetDamage(DamageClass.Generic)=StatModifier.Default*(Actor.HeldItem.CountsAsClass(DamageClass.Summon)?1:state.Training.Damage(state));
        Actor.GetAttackSpeed(DamageClass.Generic)=Actor.HeldItem.CountsAsClass(DamageClass.Summon)?1:state.Training.Speed(state);
        Actor.GetDamage(DamageClass.Magic)=StatModifier.Default*state.Vitals.MagicMultiplier;
        // Map only the cat's bag. Mod weapons may consume materials rather than
        // ammo (for example Wulfrum scrap); never borrow the human inventory.
        for(int i=1;i<59;i++)Actor.inventory[i]=Empty;
        bool materials=weapon.ModItem!=null||CompanionWeaponPolicy.Sentry(weapon);
        for(int i=0;i<Math.Min(state.Bag.Length,49);i++)if(!state.Bag[i].favorited&&(materials||state.Bag[i].ammo>0))Actor.inventory[i+1]=state.Bag[i];
        // Follow native limit changes and DD2 persistence in this cat's scope.
        using(new Scope(this))Actor.UpdateMaxTurrets();
        CountOwnedProjectiles();
        using(new Scope(this))Actor.UpdateBuffs(slot);
        Actor.tileSpeed=1/Math.Clamp(Actor.tileSpeed,1,3);
        Actor.wallSpeed=1/Math.Clamp(Actor.wallSpeed,1,3);
        Actor.slotsMinions=SummonSlots;
        bool summon=Summoning(weapon);
        // The item declares the cost of the complete summon. A head projectile
        // alone may cost zero or only part of the creature's slots.
        float slots=summon?ItemID.Sets.StaffMinionSlotsRequired[weapon.type]:0;
        bool wants=allowAttack&&(summon?Actor.slotsMinions+Math.Max(.01f,slots)<=Actor.maxMinions:target!=null);
        ObserveReach(weapon,target,wants&&target!=null&&CompanionCombatReach.Clear(cat.Center,target.Center));
        Aim=summon?CompanionWeaponAim.SummonPoint(cat,target):target!=null?CompanionWeaponAim.Point(weapon,this,target):cat.Center+new Vector2(cat.direction*80,-24);
        Vector2 plannedAim=Aim;
        bool planned=CompanionWeaponPolicy.Plan(weapon,this,target,ref plannedAim);
        Aim=plannedAim;
        // ItemCheck can finish one animation and start another in the same tick.
        // Release the input as soon as a persistent projectile is in flight.
        wants&=planned;
        if(target!=null&&!CompanionCombatReach.Clear(cat.Center,target.Center))wants=false;
        if(target!=null)Actor.MinionAttackTargetNPC=target.whoAmI;else Actor.MinionAttackTargetNPC=-1;
        // Finish a natural refill after running dry. Native regeneration speeds
        // up as the bar fills; restarting early discards that faster portion and
        // pays the regeneration delay (and a held weapon's windup) again.
        int cost=Actor.GetManaCost(weapon);
        if(WaitingForMana){
            if(Actor.statMana<cost)RestoreMana(cost);
            if(cost<=0||Actor.statMana>=State.Vitals.MaxMana)WaitingForMana=false;
            if(WaitingForMana)wants=false;
        }
        if(wants&&Actor.itemAnimation==0) {
            if(weapon.useAmmo>0&&!Actor.HasAmmo(weapon)) {wants=false;state.Expression.Say(state,cat,"NoAmmo",Terraria.GameContent.UI.EmoteID.EmoteConfused);}
            else if(cost>Actor.statMana) {
                RestoreMana(cost);
                if(cost>Actor.statMana){wants=false;WaitForMana();}
            }
        }
        if(target!=null&&wants){cat.direction=target.Center.X<cat.Center.X?-1:1;Actor.direction=cat.direction;}
        PressedThisTick=wants&&(!Actor.controlUseItem||Actor.itemAnimation==0);
        Actor.controlUseItem=wants;Actor.releaseUseItem=Actor.itemAnimation==0;Actor.controlUseTile=false;
        if(!wants)Actor.channel=false;
        int manaBefore=Actor.statMana;
        int restoredBefore=ManaRestored;
        int stackBefore=Actor.HeldItem.stack;
        try {using(new Scope(this)){Actor.UpdateMeleeHitCooldowns();if(Actor.attackCD>0)Actor.attackCD--;Actor.ItemCheck();}}
        finally {
            if(materials)for(int i=0;i<Math.Min(state.Bag.Length,49);i++)if(!state.Bag[i].favorited)state.Bag[i]=Actor.inventory[i+1];
            int spent=Math.Max(0,manaBefore+ManaRestored-restoredBefore-Actor.statMana);
            ManaSpent+=spent;state.Vitals.ManaDelay=Math.Max(0,(int)Math.Ceiling(Actor.manaRegenDelay));state.Vitals.Mana=Math.Clamp(Actor.statMana,0,state.Vitals.MaxMana);
            if(weapon.consumable) {
                int consumed=Math.Clamp(stackBefore-Actor.HeldItem.stack,0,weapon.stack);
                ThrowablesSpent+=consumed;if(!ReferenceEquals(weapon,Actor.HeldItem))weapon.stack-=consumed;
                if(weapon.stack<=0){weapon.TurnToAir();depletedThrow=true;}
            }
        }
        if(Actor.itemAnimation==Actor.itemAnimationMax&&Actor.itemAnimation>0){Uses++;if(CompanionNetwork.Server)CompanionNetwork.FeedbackAt(4,cat.Center,weapon.type);}
        // Projectile.Update totals minion slots again as each minion updates.
        // Feeding it the pre-use census would double-count and sacrifice pets.
        Actor.slotsMinions=0;Actor.numMinions=0;
        if(Actor.itemAnimation==0)Actor.lastVisualizedSelectedItem=Actor.HeldItem.Clone();
        CompanionWeaponRecovery.LifeDrain(this);
        // Consumption mutates mapped Items. Never copy cloned counts back.
    }
    static readonly Item Empty=new();
    readonly List<DrawData> drawCache=new();
    // Projectile.Update enforces maxMinions, including reconnecting segmented
    // summons before sacrificing excess bodies. Do not bypass it with Kill().
    public void Cancel(bool preserveReleased=false,bool preserveSkills=true) {
        WaitingForMana=false;
        Actor.controlUseItem=Actor.channel=false;Actor.itemAnimation=Actor.itemTime=Actor.reuseDelay=0;Actor.heldProj=-1;
        foreach(var p in Main.projectile)if(p.active&&CompanionOwnedShot.Of(p)==this) {
            if(preserveSkills&&Present&&p.ModProjectile is TrainingDart or TrainingBomb)continue;
            // The final thrown grenade must keep flying/exploding after its
            // real stack reaches zero. Explicit pause/swap still cancels all.
            if(preserveReleased&&!p.minion)continue;
            // Stop child spawns during cancellation; no resource refund/duplication.
            p.active=false;if(CompanionNetwork.Server)NetMessage.SendData(MessageID.KillProjectile,-1,-1,null,p.identity,p.owner);
        }
        Array.Clear(Actor.buffType);Array.Clear(Actor.buffTime);
    }
    public void Draw(Color light) {
        if(!Available||!EnsureActorSlot())return;
        using var pose=new ReplicaDrawScope(this);
        // Main.DrawProjectiles skips hide=true held projectiles. A real Player's
        // draw layer submits them separately; the cat must supply that layer.
        if(Actor.heldProj>=0&&Actor.heldProj<Main.maxProjectiles) {
            var held=Main.projectile[Actor.heldProj];
            if(held.active&&held.hide&&CompanionOwnedShot.Of(held)==this) {
                // This extra held-weapon pass must supply the native owner too:
                // hidden projectiles sample light at their owner's mounted center.
                // An inactive placeholder player would sample the world origin.
                var entity=Main.CurrentDrawnEntity;int shader=Main.CurrentDrawnEntityShader;
                try {using(new Scope(this,false))Main.instance.DrawProj(held.whoAmI);}
                finally {Main.instance.PrepareDrawnEntityDrawing(entity,shader,Main.GameViewMatrix.TransformationMatrix);}
            }
        }
        Item item=Actor.HeldItem;if(!Busy||item.IsAir||item.noUseGraphic)return;
        // Call the actual vanilla held-item draw layer: item offsets, staff/gun
        // origins, flipping, glow masks and special cases remain engine-owned.
        drawCache.Clear();
        bool nativeCasting=CompanionGrip.NativeCastingVisual(item);
        Vector2 location=CompanionSprite.Hand(Cat);
        // Gun/bow itemLocation is the TOP of the held frame, not the grip.
        // Keep ItemCheck_ApplyUseStyle's offset: the vanilla draw layer adds
        // DrawPlayerItemPos afterwards. Staff/swing origins already are pivots.
        if(nativeCasting||(item.useStyle==ItemUseStyleID.Shoot&&!Item.staff[item.type]))
            location+=Actor.itemLocation-Actor.MountedCenter;
        var info=new PlayerDrawSet {drawPlayer=Actor,heldItem=item,DrawDataCache=drawCache,
            Position=Actor.position,ItemLocation=location,itemColor=light,
            itemEffect=Actor.direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None};
        using(new Scope(this))PlayerDrawLayers.DrawPlayer_27_HeldItem(ref info);
        if(!nativeCasting&&drawCache.Count>0) {
            Vector2 correction=CompanionSprite.Hand(Cat)-Main.screenPosition-CompanionGrip.DrawnGrip(drawCache[0]);
            for(int i=0;i<drawCache.Count;i++){var data=drawCache[i];data.position+=correction;drawCache[i]=data;}
        }
        foreach(var data in drawCache)data.Draw(Main.spriteBatch);
        var companion=(CompanionCat)Cat.ModProjectile;
        if(!nativeCasting)CompanionItemVisual.DrawGrip(Main.spriteBatch,(Cat.Bottom+new Vector2(0,Cat.gfxOffY)-Main.screenPosition).Floor(),companion.Animation.SpriteFrame,Cat.direction,light);
    }
    public sealed class Scope : IDisposable {
        readonly CompanionNativeUse context,previous;
        readonly Player backup;
        readonly int myPlayer,mouseX,mouseY;
        readonly bool blockMouse;
        readonly bool mouseLeft,mouseLeftRelease,mouseRight,mouseRightRelease;
        readonly bool actorActive;
        readonly int actorSlot;readonly CompanionProjectileScope projectiles;
        readonly IDisposable hitCooldowns;readonly bool healthScope;readonly int healthBefore;
        public Scope(CompanionNativeUse use,bool trackHealing=true) {
            if(!use.EnsureActorSlot())throw new InvalidOperationException("No free native companion actor slot");
            // This actor runs native item/projectile code, not Player.Update.
            // Reuse the owner's native clock for colour cycles and idle motion.
            // Sampling here covers ItemCheck, projectile AI and drawing on all
            // peers without advancing time per projectile, extra update or draw.
            if(use.State?.Player is {} owner)use.Actor.miscCounter=owner.miscCounter;
            if(use.Cat is {active:true})use.LocateActor(use.Cat);
            // Optional adapters run before swapping global state, so an adapter
            // failure cannot leave the human player replaced by an actor.
            CompanionCrossMod.PrepareActor?.Invoke(use.Actor,use.Aim);
            actorSlot=use.slot;actorActive=use.Actor.active;projectiles=new CompanionProjectileScope(actorSlot,p=>CompanionOwnedShot.Of(p)==use);
            if(Current!=use){
                if(Main.netMode!=Terraria.ID.NetmodeID.SinglePlayer)hitCooldowns=use.HitCooldowns.Enter(actorSlot);
                healthScope=trackHealing;healthBefore=CompanionWeaponRecovery.Begin(use);
            }
            context=use;previous=Current;backup=Main.player[use.slot];myPlayer=Main.myPlayer;mouseX=Main.mouseX;mouseY=Main.mouseY;
            blockMouse=Main.blockMouse;
            mouseLeft=Main.mouseLeft;mouseLeftRelease=Main.mouseLeftRelease;mouseRight=Main.mouseRight;mouseRightRelease=Main.mouseRightRelease;
            if(use.Cat is {active:true})use.LocateActor(use.Cat);
            Main.player[use.slot]=use.Actor;use.Actor.active=true;Main.myPlayer=CompanionNetwork.Client?myPlayer:use.slot;Current=use;
            Main.mouseX=(int)(use.Aim.X-Main.screenPosition.X);Main.mouseY=(int)(use.Aim.Y-Main.screenPosition.Y);
            Main.mouseLeft=use.Actor.controlUseItem;Main.mouseLeftRelease=use.PressedThisTick;
            Main.mouseRight=use.Actor.controlUseTile;Main.mouseRightRelease=false;
        }
        public void Dispose() {
            try {if(healthScope)CompanionWeaponRecovery.End(context,healthBefore);}
            finally {try {hitCooldowns?.Dispose();}finally {
            projectiles.Dispose();Main.player[actorSlot]=backup;Main.myPlayer=myPlayer;Main.mouseX=mouseX;Main.mouseY=mouseY;Current=previous;
            Main.blockMouse=blockMouse;
            Main.mouseLeft=mouseLeft;Main.mouseLeftRelease=mouseLeftRelease;Main.mouseRight=mouseRight;Main.mouseRightRelease=mouseRightRelease;
            context.Actor.active=actorActive;
            if(context.Cat==null){CompanionActorSlots.Release(context,actorSlot);context.slot=-1;}
            }}
        }
    }
    public void RestoreMana(int required) {
        if(!Available||!(CompanionNetwork.Server?State.NetAutoHeal:ModContent.GetInstance<CompanionConfig>().AutoHeal))return;
        State.Vitals.Mana=Math.Clamp(Actor.statMana,0,State.Vitals.MaxMana);
        int restored=Drink(true,required,out int potion);
        if(restored==0)return;
        WaitingForMana=false;
        ManaPotions++;ManaRestored+=restored;Actor.statMana=State.Vitals.Mana;
        Actor.GetDamage(DamageClass.Magic)=StatModifier.Default*State.Vitals.MagicMultiplier;
        (Cat.ModProjectile as CompanionCat)?.ActionVisual.Begin((CompanionCat)Cat.ModProjectile,CompanionGesture.Drink,potion);
        CompanionFeedback.Show(2,Cat.Center,restored,NativeDrink&&!CompanionNetwork.Server?"skill":"");
        State.Expression.Say(State,Cat,"ManaPotion",Terraria.GameContent.UI.EmoteID.ItemManaPotion);
    }
    internal void CommitProjectileMana(int before,int restoredBefore) {
        int spent=Math.Max(0,before+ManaRestored-restoredBefore-Actor.statMana);
        ManaSpent+=spent;State.Vitals.ManaDelay=Math.Max(0,(int)Math.Ceiling(Actor.manaRegenDelay));
        State.Vitals.Mana=Math.Clamp(Actor.statMana,0,State.Vitals.MaxMana);
    }
}
public sealed class CompanionOwnedShot : GlobalProjectile {
    public override bool InstancePerEntity=>true;
    public CompanionNativeUse Context;
    public bool Thrown;
    internal int CatIdentity=-1;
    internal ulong CatInstance;
    internal int BondOwner;
    internal bool PendingNativeLinkRepair;
    int unboundTicks;
    public override void SendExtraAI(Projectile p,Terraria.ModLoader.IO.BitWriter bits,System.IO.BinaryWriter w){
        bits.WriteBit(Context!=null);if(Context!=null){w.Write(Context.State.Player.whoAmI);w.Write(Context.Cat.identity);w.Write(((CompanionCat)Context.Cat.ModProjectile).NetworkInstance);w.Write(Thrown);}
    }
    public override void ReceiveExtraAI(Projectile p,Terraria.ModLoader.IO.BitReader bits,System.IO.BinaryReader r){
        if(bits.ReadBit()){int bondOwner=r.ReadInt32(),identity=r.ReadInt32();ulong instance=r.ReadUInt64();bool thrown=r.ReadBoolean();if(CompanionNetwork.Client){BondOwner=bondOwner;CatIdentity=identity;CatInstance=instance;Thrown=thrown;PendingNativeLinkRepair=true;Resolve(p);
}}
        else if(CompanionNetwork.Client){Context=null;CatIdentity=-1;CatInstance=0;Thrown=false;PendingNativeLinkRepair=false;}
    }
    internal void Resolve(Projectile p){
        if(CatIdentity<0)return;
        if(Context!=null){if(Context.Cat is {active:true}&&Context.Cat.ModProjectile is CompanionCat live&&live.NetworkInstance==CatInstance)return;Context=null;}
        foreach(var cat in Main.projectile)
        if(cat.active&&cat.ModProjectile is CompanionCat c&&c.OwnerIndex==BondOwner&&c.NetworkInstance==CatInstance){
            var owner=Main.player[BondOwner];Context=c.Weapons;c.Weapons.BindReplica(owner,owner.GetModPlayer<CompanionPlayer>(),cat);return;
        }
    }
    internal bool Waiting(Projectile p){
        if(CatIdentity<0||Context!=null){unboundTicks=0;return false;}
        if(CompanionRoster.Retired(BondOwner,CatInstance)||++unboundTicks>180)p.active=false;
        return true;
    }
    public static CompanionNativeUse Of(Projectile p)=>p.TryGetGlobalProjectile<CompanionOwnedShot>(out var tag)?tag.Context:null;
    public override bool ShouldUpdatePosition(Projectile p)=>Context==null||!CompanionGrip.ShortStab(p);
    public override void PostAI(Projectile p) {
        if(Context?.Available==true){CompanionGrip.FitStab(p,Context);CompanionGrip.FitHeld(p,Context);}
    }
    public override void OnHitNPC(Projectile projectile,NPC target,NPC.HitInfo hit,int damageDone) {
        if(Context!=null&&damageDone>0)Context.Hits++;
        if(Context!=null&&Thrown&&damageDone>0)Context.ThrownHits++;
    }
    public override void ModifyHitNPC(Projectile projectile,NPC target,ref NPC.HitModifiers modifiers) {
        if(Context!=null)modifiers.FinalDamage*=CompanionBalanceConfig.Current.DamageScale;
    }
    public override void OnSpawn(Projectile p,IEntitySource source) {
        Context=!p.hostile&&p.owner==CompanionNativeUse.Current?.Actor.whoAmI?CompanionNativeUse.Current:null;
        if(Context==null&&!p.hostile&&source is EntitySource_Parent parent&&parent.Entity is Projectile ancestor&&ancestor.owner==p.owner)Context=CompanionOwnedShot.Of(ancestor);
        // Companion gameplay is server-owned, including children created by
        // modded AI without an owner check. A replica must not create a second
        // local projectile. Network receipt uses SetDefaults/ReceiveExtraAI,
        // not this local spawn hook. Do not Kill(): that could spawn more shots.
        if(CompanionNetwork.Client&&Context!=null){p.active=false;return;}
        Thrown=Context!=null&&(Context.Actor.HeldItem.consumable||source is EntitySource_Parent parentShot&&parentShot.Entity is Projectile from&&from.GetGlobalProjectile<CompanionOwnedShot>().Thrown);
        if(Context!=null)Context.Shots++;
    }
}
public sealed class CompanionNativeMeleeHit : GlobalNPC {
    public override void ModifyHitByItem(NPC npc,Player player,Item item,ref NPC.HitModifiers modifiers) {
        var use=CompanionNativeUse.Current;
        if(use!=null&&ReferenceEquals(player,use.Actor))modifiers.FinalDamage*=CompanionBalanceConfig.Current.DamageScale;
    }
    public override void OnHitByItem(NPC npc,Player player,Item item,NPC.HitInfo hit,int damageDone) {
        var use=CompanionNativeUse.Current;
        if(use!=null&&ReferenceEquals(player,use.Actor)&&damageDone>0)use.Hits++;
    }
}
public sealed class CompanionCombatTerrain : GlobalTile {
    public override bool CanExplode(int i,int j,int type)=>CompanionNativeUse.Current==null;
}
public sealed class CompanionNativeHooks : ModSystem {
    public override void Load(){IL_Player.ItemCheck_Shoot+=HeadlessShoot;IL_Player.ItemCheck_ApplyUseStyle_Inner+=HeadlessGeometry;IL_Player.ItemCheck_GetMeleeHitbox+=HeadlessGeometry;On_Item.GetDrawHitbox+=DrawHitbox;On_Player.GetItemDrawFrame+=ItemFrame;On_NetMessage.SendData+=SendNative;On_Player.ItemCheck_PlayInstruments+=Instruments;On_Projectile.Update+=UpdateShot;On_Main.DrawProjDirect+=DrawShot;On_Player.ItemCheck_CutTiles+=CutTiles;On_Main.GetPlayerArmPosition+=ArmPosition;On_NPC.PlayerInteraction+=Interaction;On_Player.HealEffect+=Heal;On_Player.ManaEffect+=ManaFeedback;}
    public override void Unload(){IL_Player.ItemCheck_Shoot-=HeadlessShoot;IL_Player.ItemCheck_ApplyUseStyle_Inner-=HeadlessGeometry;IL_Player.ItemCheck_GetMeleeHitbox-=HeadlessGeometry;On_Item.GetDrawHitbox-=DrawHitbox;On_Player.GetItemDrawFrame-=ItemFrame;On_NetMessage.SendData-=SendNative;On_Player.ItemCheck_PlayInstruments-=Instruments;On_Projectile.Update-=UpdateShot;On_Main.DrawProjDirect-=DrawShot;On_Player.ItemCheck_CutTiles-=CutTiles;On_Main.GetPlayerArmPosition-=ArmPosition;On_NPC.PlayerInteraction-=Interaction;On_Player.HealEffect-=Heal;On_Player.ManaEffect-=ManaFeedback;}
    // These two native methods contain server early-outs for human rendering.
    // Their geometry is also used by melee hits and replicated held weapons.
    // Override only the branch value during the companion actor scope; never
    // change the global dedicated-server flag or create GPU assets on a server.
    static void HeadlessGeometry(ILContext il){var c=new ILCursor(il);while(c.TryGotoNext(MoveType.After,i=>i.MatchLdsfld<Main>(nameof(Main.dedServ))))c.EmitDelegate<Func<bool,bool>>(dedicated=>dedicated&&CompanionNativeUse.Current==null&&CompanionFishing.Current==null);}
    static Rectangle DrawHitbox(On_Item.orig_GetDrawHitbox orig,int type,Player player)=>Main.dedServ&&(CompanionNativeUse.Current?.Actor==player||CompanionFishing.Current?.Actor==player)?CompanionGrip.ItemFrame(type):orig(type,player);
    static void HeadlessShoot(ILContext il){
        var c=new ILCursor(il);
        // Harp note speed/pitch uses viewport height. The server has no view
        // matrix; use a stable 720-world-pixel viewport for its actor only.
        // Keep native Shoot, random note selection, pitch and network AI data.
        if(!c.TryGotoNext(i=>i.MatchLdsfld<Main>(nameof(Main.screenHeight)),i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Conv_R4,
            i=>i.MatchLdsfld<Main>(nameof(Main.GameViewMatrix))))throw new InvalidOperationException("Native harp viewport expression not found");
        int start=c.Index;
        if(!c.TryGotoNext(MoveType.After,i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Div))throw new InvalidOperationException("Native harp viewport division not found");
        int length=c.Index-start;c.Index=start;c.RemoveRange(length);
        c.EmitDelegate<Func<float>>(()=>Main.dedServ&&CompanionNativeUse.Current!=null?720f:Main.screenHeight/Main.GameViewMatrix.Zoom.Y);
    }
    static Rectangle ItemFrame(On_Player.orig_GetItemDrawFrame orig,Player player,int type)=>Main.dedServ&&(CompanionNativeUse.Current?.Actor==player||CompanionFishing.Current?.Actor==player)?CompanionGrip.ItemFrame(type):orig(player,type);
    internal static void SendNative(On_NetMessage.orig_SendData orig,int msg,int remote,int ignore,Terraria.Localization.NetworkText text,int n,float n2,float n3,float n4,int n5,int n6,int n7){
        // Suppress the borrowed actor's player packets, not another mod's real
        // player updates triggered synchronously during ItemCheck/projectile AI.
        var actor=CompanionNativeUse.Current?.Actor??CompanionFishing.Current?.Actor;
        if(actor!=null&&n==actor.whoAmI&&Main.netMode!=NetmodeID.SinglePlayer&&msg is MessageID.PlayerControls or MessageID.SyncEquipment or MessageID.PlayerMana or MessageID.PlayerBuffs or MessageID.PlayerLifeMana or 41 or 66)return;
        orig(msg,remote,ignore,text,n,n2,n3,n4,n5,n6,n7);
    }
    static void ManaFeedback(On_Player.orig_ManaEffect orig,Player p,int amount){if(CompanionNativeUse.Current?.Actor==p)return;orig(p,amount);}
    static void Heal(On_Player.orig_HealEffect orig,Player p,int amount,bool broadcast){
        if(CompanionNativeUse.Current?.Actor==p)return;
        orig(p,amount,broadcast);
    }
    static void Instruments(On_Player.orig_ItemCheck_PlayInstruments orig,Player p,Item item){
        if(Main.dedServ&&(CompanionNativeUse.Current?.Actor==p||CompanionFishing.Current?.Actor==p)){
            // Instruments have no ordinary UseSound. Relay the native trigger;
            // clients choose the chord using their own viewport, as vanilla does.
            if(item.type==ItemID.SparkleGuitar&&p.ItemAnimationJustStarted&&CompanionNativeUse.Current is {} use)
                CompanionNetwork.FeedbackAt(9,p.Center,(int)Vector2.Distance(p.Center,use.Aim));
            return;
        }
        orig(p,item);
    }
    static void Interaction(On_NPC.orig_PlayerInteraction orig,NPC npc,int playerIndex) {
        // StrikeNPC uses Main.myPlayer in single player. Credit the real owner
        // BEFORE lethal damage/loot processing, never the temporary actor slot.
        var use=CompanionNativeUse.Current;
        orig(npc,use!=null&&playerIndex==use.Actor.whoAmI?use.State.Player.whoAmI:playerIndex);
    }
    static Vector2 ArmPosition(On_Main.orig_GetPlayerArmPosition orig,Projectile p) {
        var context=CompanionOwnedShot.Of(p);
        // Whips and other arm-anchored native projectiles ask the engine for a
        // human body-frame hand. Substitute only the tagged cat's front paw.
        return context?.Available==true?CompanionSprite.Hand(context.Cat):orig(p);
    }
    static void CutTiles(On_Player.orig_ItemCheck_CutTiles orig,Player p,Item item,Rectangle box,bool[] ignore) {
        if(CompanionNativeUse.Current?.Actor==p)return;orig(p,item,box,ignore);
    }
    static void UpdateShot(On_Projectile.orig_Update orig,Projectile p,int index) {
        if(p.TryGetGlobalProjectile<CompanionOwnedShot>(out var tag)){tag.Resolve(p);if(tag.Waiting(p))return;}
        var context=CompanionOwnedShot.Of(p);
        if(context==null){orig(p,index);return;}
        if(!context.CanMaintainShot(p)||!context.EnsureActorSlot()||!p.active){p.active=false;return;}
        bool instant=CompanionInstantShot.IsTrail(p);
        // Presentation of these one-update shots comes from the resolved path.
        if(instant&&CompanionNetwork.Client){p.active=false;return;}
        // A child's packet can arrive before its cat's binding. Repair the
        // native link once binding succeeds, before the child executes its AI.
        if(tag.PendingNativeLinkRepair)p.ProjectileFixDesperation();
        int manaBefore=context.Actor.statMana,restoredBefore=context.ManaRestored;
        var trail=instant&&CompanionNetwork.Server?p.GetGlobalProjectile<CompanionInstantShot>():null;
        trail?.Begin(p);
        float previousRotation=context.Actor.itemRotation;
        try {using(new CompanionNativeUse.Scope(context))orig(p,index);}
        finally {
            // A returning tether can coincide with its owner's mounted center.
            // Native DirectionFrom then has no direction; retain the last arm
            // angle instead of letting NaN enter drawing or network snapshots.
            if(!float.IsFinite(context.Actor.itemRotation))context.Actor.itemRotation=float.IsFinite(previousRotation)?previousRotation:0;
            if(!CompanionNetwork.Client)context.CommitProjectileMana(manaBefore,restoredBefore);trail?.End(p);
        }
    }
    static void DrawShot(On_Main.orig_DrawProjDirect orig,Main self,Projectile projectile) {
        var context=CompanionOwnedShot.Of(projectile);
        if(context==null){orig(self,projectile);return;}
        if(!context.CanMaintainShot(projectile)||!context.EnsureActorSlot()||!projectile.active)return;
        using(new CompanionNativeUse.ReplicaDrawScope(context))
        using(new CompanionNativeUse.Scope(context))orig(self,projectile);
        if(context.Available&&context.Actor.heldProj==projectile.whoAmI) {
            var cat=context.Cat;var companion=(CompanionCat)cat.ModProjectile;
            CompanionItemVisual.DrawGrip(Main.spriteBatch,(cat.Bottom+new Vector2(0,cat.gfxOffY)-Main.screenPosition).Floor(),companion.Animation.SpriteFrame,cat.direction,Lighting.GetColor(cat.Center.ToTileCoordinates()));
        }
    }
}
