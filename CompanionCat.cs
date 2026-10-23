using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using PalicoCompanion.Systems;
namespace PalicoCompanion;
public sealed class CompanionCat : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_0";
    int tick, targetItem=-1, attackCooldown, workCooldown;
    public readonly CompanionNavigation Navigation=new();
    public readonly CompanionNativeUse Weapons=new();
    public readonly CompanionPickup Pickup=new();
    public readonly CompanionFeedback Feedback=new();
    internal readonly CompanionActionVisual ActionVisual=new();
    public readonly CompanionCatching Catching=new();
    public readonly CompanionSupplies Supplies=new();
    public readonly CompanionEvasion Evasion=new();
    public readonly CompanionDivingVisual Diving=new();
    readonly CompanionFace face=new();
    public readonly CompanionFishing Fishing=new();
    readonly CompanionTargeting targeting=new();
    int attackVisualTicks,attackVisualDuration=1;
    bool attackThrust;
    bool animationThreat;
    bool previousWeaponActive;
    internal bool PendingPresentation;
    bool retreated;
    bool networkFlying;int networkFrame;
    internal int NetworkReceives;
    internal int PresentationEpoch;
    internal ulong NetworkInstance;
    // Network owner 255 is the server. The human companion bond is separate.
    public int OwnerIndex=>Main.netMode!=NetmodeID.SinglePlayer?(int)Projectile.ai[0]:Projectile.owner;
    internal static int OwnerOf(Projectile cat)=>cat.ModProjectile is CompanionCat c?c.OwnerIndex:cat.owner;
    readonly CompanionReplica replica=new();
    int networkTool;float networkToolProgress;Vector2 networkToolAim;
    public override void SendExtraAI(System.IO.BinaryWriter w){
        w.Write(NetworkInstance);
        w.Write(Projectile.direction);w.Write(Animation.SpriteFrame);w.Write(Navigation.Riding);
        w.Write(Weapons.Actor.direction);w.Write(Weapons.Actor.itemAnimation);w.Write(Weapons.Actor.itemAnimationMax);w.Write(Weapons.Actor.itemRotation);w.Write(Weapons.Actor.channel);
        ushort count=0;for(int i=0;i<Weapons.Actor.buffType.Length;i++)if(Weapons.Actor.buffType[i]>0&&Weapons.Actor.buffTime[i]>0)count++;
        w.Write(count);for(int i=0;i<Weapons.Actor.buffType.Length;i++)if(Weapons.Actor.buffType[i]>0&&Weapons.Actor.buffTime[i]>0){w.Write(Weapons.Actor.buffType[i]);w.Write(Weapons.Actor.buffTime[i]);}
        Vector2 offset=Weapons.Actor.itemLocation-Weapons.Actor.Center;w.Write(offset.X);w.Write(offset.Y);
        w.Write(Weapons.Aim.X);w.Write(Weapons.Aim.Y);
        var s=Main.player[OwnerIndex].GetModPlayer<CompanionPlayer>();bool mine=s.Mining.SwingTicks>0;
        w.Write(Catching.SwingTicks>0?Catching.Tool.type:mine?s.Mining.SwingItemType:s.Trees.SwingTicks>0?s.Trees.SwingItemType:0);
        w.Write(Catching.SwingTicks>0?1-Catching.SwingTicks/(float)Catching.Duration:mine?1-s.Mining.SwingTicks/(float)s.Mining.SwingDuration:s.Trees.SwingTicks>0?1-s.Trees.SwingTicks/(float)s.Trees.SwingDuration:0);
        var aim=Catching.SwingTicks>0?Catching.Target:mine?s.Mining.SwingTarget:s.Trees.Target;w.Write(aim.X);w.Write(aim.Y);
        w.Write((ulong)Main.GameUpdateCount);w.Write(PresentationEpoch);w.Write((byte)Animation.Motion);w.Write(Animation.IdleTicks);w.Write(Animation.StateTicks);w.Write(Animation.WalkPhase);
        w.Write(Catching.SwingTicks>0?Catching.Duration:mine?s.Mining.SwingDuration:s.Trees.SwingDuration);
        ActionVisual.Write(w);
    }
    public override void ReceiveExtraAI(System.IO.BinaryReader r){
        ulong previousInstance=NetworkInstance,instance=r.ReadUInt64();if(CompanionNetwork.Client)NetworkInstance=instance;
        NetworkReceives++;Projectile.direction=r.ReadInt32();networkFrame=r.ReadInt32();networkFlying=r.ReadBoolean();
        int itemDirection=r.ReadInt32(),itemAnimation=r.ReadInt32(),itemMax=r.ReadInt32();float itemRotation=r.ReadSingle();bool channel=r.ReadBoolean();
        int buffs=r.ReadUInt16();Array.Clear(Weapons.Actor.buffType);Array.Clear(Weapons.Actor.buffTime);for(int i=0;i<buffs;i++){int type=r.ReadInt32(),time=r.ReadInt32();if(i<Weapons.Actor.buffType.Length){Weapons.Actor.buffType[i]=type;Weapons.Actor.buffTime[i]=time;}}
        var itemOffset=new Vector2(r.ReadSingle(),r.ReadSingle());var weaponAim=new Vector2(r.ReadSingle(),r.ReadSingle());networkTool=r.ReadInt32();networkToolProgress=r.ReadSingle();networkToolAim=new Vector2(r.ReadSingle(),r.ReadSingle());
        var pose=new CompanionPose{Time=r.ReadUInt64(),Epoch=r.ReadInt32(),Motion=(CatMotion)r.ReadByte(),IdleTicks=r.ReadInt32(),StateTicks=r.ReadInt32(),WalkPhase=r.ReadDouble(),ToolDuration=r.ReadInt32(),
            Position=Projectile.position,Velocity=Projectile.velocity,Direction=Projectile.direction,Frame=networkFrame,Flying=networkFlying,
            ItemDirection=itemDirection,ItemAnimation=itemAnimation,ItemMax=itemMax,ItemRotation=itemRotation,Channel=channel,ItemOffset=itemOffset,
            Tool=networkTool,ToolProgress=networkToolProgress,ToolAim=networkToolAim,WeaponAim=weaponAim};
        ActionVisual.Read(r,previousInstance!=instance);
        if(CompanionNetwork.Client&&CompanionRoster.Observe(this,(ulong)pose.Time)){if(previousInstance!=instance)replica.Clear();replica.Receive(pose,Main.GameUpdateCount);Projectile.position=replica.Position;}

    }
    Vector2 workFacing,attackFacing;
    int workFacingTicks;
    public void FaceWork(Vector2 target) {workFacing=target;workFacingTicks=12;}
    public static int Facing(Vector2 center,Vector2 target,int current)=>Math.Abs(target.X-center.X)>2?Math.Sign(target.X-center.X):current;
    public readonly CompanionAnimation Animation=new();
    public readonly CompanionFlightVisual Flight=new();
    public readonly CompanionLighting Light=new();
    public override void OnSpawn(Terraria.DataStructures.IEntitySource source){
        CompanionIncomingDamage.Register(this);
        if(CompanionNetwork.Server&&Projectile.owner!=255){
            int owner=Projectile.owner;Projectile.ai[0]=owner;
            if(Main.projectileIdentity[owner,Projectile.identity]==Projectile.whoAmI)Main.projectileIdentity[owner,Projectile.identity]=-1;
            Projectile.owner=255;Main.projectileIdentity[255,Projectile.identity]=Projectile.whoAmI;
        }
        if(CompanionNetwork.Server)NetworkInstance=CompanionRoster.Next();
        var state=Main.player[OwnerIndex].GetModPlayer<CompanionPlayer>();
        if(!CompanionNetwork.Server&&!state.Vitals.Downed)CompanionTransitEffects.At(Projectile.Center);
    }
    public static bool ShowWorldHealth(CompanionVitals v)=>!v.Downed&&v.Health>0&&v.Health<v.MaxHealth;
    public override void SetDefaults() {
        // Vanilla Projectile.Update otherwise overwrites PostAI direction after
        // movement (including zero velocity -> right). Ground pets own direction.
        Projectile.manualDirectionChange=true;
        Projectile.width=CompanionNavigation.Width; Projectile.height=CompanionNavigation.Height; Projectile.tileCollide=true; Projectile.friendly=false;
        Projectile.penetrate=-1; Projectile.timeLeft=2; Projectile.netImportant=true;
    }
    // Terraria has already resolved each collision axis. A side collision must
    // not erase downward velocity or the cat can hang above a one-tile hollow.
    public override bool OnTileCollide(Vector2 oldVelocity) => false;
    public override bool? CanDamage()=>false;
    public override bool ShouldUpdatePosition()=>!CompanionNetwork.Client;
    public override void OnKill(int timeLeft){
        CompanionIncomingDamage.Remove(this);
        Weapons.ReleaseActorSlot();Fishing.Cancel();Supplies.Cancel();if(CompanionNetwork.Server)NetMessage.SendData(MessageID.KillProjectile,-1,-1,null,Projectile.identity,Projectile.owner);
    }
    public override bool? CanCutTiles()=>false;
    public override bool TileCollideStyle(ref int width,ref int height,ref bool fallThrough,ref Vector2 hitboxCenterFrac) {
        var owner=Main.player[OwnerIndex];var state=owner.GetModPlayer<CompanionPlayer>();
        fallThrough=!state.Paused&&!state.Vitals.Downed&&Navigation.DropThrough;
        return true;
    }
    public override void AI() {
        Player owner=Main.player[OwnerIndex]; var state=owner.GetModPlayer<CompanionPlayer>();
        if(CompanionNetwork.Client){
            tick++;Projectile.timeLeft=60;Projectile.tileCollide=false;Projectile.netUpdate=false;
            if(replica.Ready){var pose=replica.Advance(Main.GameUpdateCount);Projectile.position=pose.Position;Projectile.velocity=pose.Velocity;Projectile.direction=pose.Direction;
                networkFlying=pose.Flying;networkTool=pose.Tool;networkToolProgress=pose.ToolProgress;networkToolAim=pose.ToolAim;
                Weapons.ReceivePose(pose);
                Weapons.PlacementAim(pose.WeaponAim);
                Animation.PlayReplica(pose,replica.Elapsed);
            }
            Weapons.BindReplica(owner,state,Projectile);return;
        }
        if(!owner.active || owner.dead || !state.Enabled || CompanionNetwork.Server&&(!state.NetReady||state.NetLeaving)) { Fishing.Cancel();Supplies.Cancel();Weapons.Cancel();Projectile.Kill(); return; }
        Fishing.Maintain(owner,state,Projectile);
        animationThreat=false;
        if(workFacingTicks>0)workFacingTicks--;
        state.Travel.ClearGoal();
        if(attackVisualTicks>0)attackVisualTicks--;
        if(state.Paused||state.Vitals.Downed){attackVisualTicks=0;Weapons.Cancel();}
        state.Mining.UpdateVisual();state.Trees.UpdateVisual();
        if(state.Paused||state.Vitals.Downed||state.Construction.Active||state.Digging.Active||state.Mining.Active||state.Trees.Active||state.Garden.Active||state.Order.Active||state.Storage.Active){Catching.Cancel();Supplies.Cancel();}
        Projectile.timeLeft=2; tick++; if(attackCooldown>0)attackCooldown--; if(workCooldown>0)workCooldown--;
        if(Projectile.ai[1]>0)Projectile.ai[1]--;
        Vector2 goal=state.Digging.Active?Projectile.Center:CompanionNavigation.FollowGoal(owner,Projectile.Center);
        if(!state.Paused && !state.Vitals.Downed) {
            bool bossNearby=CompanionIncomingDamage.BossNear(owner);
            foreach(var enemy in Main.npc) {
                if(enemy.active&&!enemy.friendly&&enemy.damage>0&&!enemy.dontTakeDamage&&enemy.Hitbox.Intersects(Projectile.Hitbox))
                    Feedback.Hurt(state,Projectile,enemy.damage,bossNearby);
            }
            // Hostile shots are handled at their real native Damage pass, not
            // a second rectangular collision approximation here.
        }
        if(state.Vitals.Downed) {
            CancelTransitWork(state);Weapons.Cancel();Navigation.HoldReturn(Projectile);
            if(CompanionRestVisual.Hidden(state.Vitals)){
                if(!retreated&&state.Vitals.RecoveryProgress<=CompanionRestVisual.StunTicks+2){CompanionRestVisual.DustPuff(Projectile);}
                retreated=true;Projectile.tileCollide=false;Projectile.velocity=Vector2.Zero;
            }
            else Projectile.velocity.X=0;
            if(!retreated&&Projectile.tileCollide)Projectile.velocity.Y=Math.Min(10,Projectile.velocity.Y+.35f);
            state.RecoveryPosition=Projectile.Center;state.RecoveryWorld=CompanionLife.World;
            state.Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.Recovering",(state.Vitals.Recovery+59)/60);
            Light.Update(owner,state,Projectile,true);return;
        }
        if(retreated){
            retreated=false;Projectile.Center=owner.Center;Projectile.velocity=Vector2.Zero;
            Navigation.HoldReturn(Projectile);CompanionRestVisual.DustPuff(Projectile,true);
        }
        if(!state.Paused&&(CompanionNetwork.Server?state.NetAutoHeal:ModContent.GetInstance<CompanionConfig>().AutoHeal)) {
            int healed=Weapons.Heal(owner,state,Projectile,out int potion);
            if(healed>0) {
                ActionVisual.Begin(this,CompanionGesture.Drink,potion);
                CompanionFeedback.Show(1,Projectile.Center,healed,Weapons.NativeDrink&&!CompanionNetwork.Server?"skill":"");
                state.Report(CompanionText.Message("CompanionCat.DrankPotion" ,healed));
                state.Expression.Say(state,Projectile,"Heal",Terraria.GameContent.UI.EmoteID.ItemLifePotion);
            }
        }
        if(Navigation.Riding&&(state.Paused||state.Vitals.Downed)) {
            CancelTransitWork(state);Navigation.HoldReturn(Projectile);
            Light.Update(owner,state,Projectile,true);state.Status=state.Paused?"Paused":CompanionText.Message("CompanionCat.Recovering");return;
        }
        // Directed mining returns below, so collect nearby drops before either
        // work branch takes ownership of movement. Pickup never changes its goal.
        if(!state.Paused&&!state.Vitals.Downed&&state.Pickup&&(state.Digging.Active||state.Mining.Active)) {
            targetItem=-1;Vector2 nearby=Projectile.Center;Pickup.Tick(owner,state,Projectile,ref nearby,false);
        }
        if(state.Directive.Active&&!state.Paused) {
            bool danger=FindThreat(owner)!=null;
            if(state.Directive.Tick(state,this,danger,out var directedGoal)) {
                targetItem=-1;Supplies.Cancel();Catching.Cancel();
                Weapons.Tick(owner,state,Projectile,null,false);Light.Update(owner,state,Projectile,true);
                if(state.Mining.Active)FaceWork(state.Mining.Target);else if(state.Trees.Active)FaceWork(state.Trees.Target);
                if(Fishing.Active)CompanionFishing.HoldPosition(Projectile);else Move(directedGoal,owner,state);return;
            }
        }
        // An existing cast owns the shore position, not the owner's movement.
        // Real digging/construction/recall cancels it in Maintain; loose drops
        // are collected locally without pulling the cat away from its bobber.
        if(Fishing.Active&&!state.Paused){
            if(FindThreat(owner)!=null)Fishing.Cancel();
            else if(Supplies.Tick(owner,state,Projectile,ref goal)){Fishing.Cancel();Move(goal,owner,state);return;}
            else if(Fishing.Tick(owner,state,Projectile)){
                targetItem=-1;if(state.Pickup){Vector2 nearby=Projectile.Center;Pickup.Tick(owner,state,Projectile,ref nearby,false);}
                Projectile.velocity.X*=.5f;Weapons.Tick(owner,state,Projectile,null,false);Light.Update(owner,state,Projectile,true);return;
            }
        }
        var airborneTarget=!state.Paused&&!state.Vitals.Downed&&Navigation.Riding?FindThreat(owner):null;
        var airWeapon=CompanionWeapons.Active(state.Gear);
        bool airWorkDanger=airborneTarget!=null&&(HoldingThreat||CompanionThreat.InterruptsWork(airborneTarget,owner,Projectile.Center)||CompanionTargeting.Marked(owner,Projectile.Center)==airborneTarget);
        if(!state.Paused&&!state.Vitals.Downed&&Navigation.Riding&&!airWorkDanger&&Vector2.DistanceSquared(owner.Center,Projectile.Center)<480*480) {
            if(state.Construction.Active&&state.Construction.Tick(owner,state,Projectile,false)&&Navigation.UpdateWorkFlight(Projectile,owner,state.Construction.MoveTarget,true)){
                Weapons.Tick(owner,state,Projectile,null,false);Light.Update(owner,state,Projectile,true);return;
            }
            if(state.Mining.Active&&CompanionNavigation.Fits(Projectile.Center)) {
                targetItem=-1;
                state.Mining.Tick(owner,Projectile.Center,false,false);
                if(state.Mining.Active&&Navigation.UpdateWorkFlight(Projectile,owner,state.Mining.MoveTarget,true)) {
                    Weapons.Tick(owner,state,Projectile,null,false);FaceWork(state.Mining.Target);
                    Light.Update(owner,state,Projectile,true);state.Status=state.Mining.Status;return;
                }
            }
            if(!state.Construction.Active&&!state.Digging.Active&&!state.Trees.Requested&&state.Storage.Tick(owner,state,Projectile,false)){
                Vector2 storageGoal=Vector2.DistanceSquared(Projectile.Center,state.Storage.Target)<60*60?Projectile.Center:state.Storage.Target;
                if(Navigation.UpdateWorkFlight(Projectile,owner,storageGoal,true)){
                    targetItem=-1;Weapons.Tick(owner,state,Projectile,null,false);Light.Update(owner,state,Projectile,true);return;
                }
            }
            if(!state.Construction.Active&&!state.Digging.Active&&!state.Mining.Active&&!state.Trees.Active&&!state.Garden.Active&&!state.Order.Active&&!state.Storage.Active) {
                Vector2 supplyGoal=Projectile.Center;
                if(Supplies.Tick(owner,state,Projectile,ref supplyGoal)&&Navigation.UpdateWorkFlight(Projectile,owner,supplyGoal,true)){
                    Catching.Cancel();targetItem=-1;Weapons.Tick(owner,state,Projectile,null,false);Light.Update(owner,state,Projectile,true);return;
                }
            }
            if(!state.Construction.Active&&!state.Digging.Active&&!state.Mining.Active&&!state.Trees.Requested&&state.Pickup) {
                Vector2 airGoal=Projectile.Center;targetItem=Pickup.Tick(owner,state,Projectile,ref airGoal);
                if(targetItem>=0&&Navigation.UpdateWorkFlight(Projectile,owner,airGoal,true)) {
                    Weapons.Tick(owner,state,Projectile,null,false);Light.Update(owner,state,Projectile,true);state.Status=CompanionText.Message("CompanionCat.FlyingToLoot");return;
                }
            }
            if(!state.Construction.Active&&!state.Digging.Active&&!state.Mining.Active&&targetItem<0&&state.Trees.Tick(owner,state,Projectile,false)&&Navigation.UpdateWorkFlight(Projectile,owner,state.Trees.MoveTarget,true)) {
                Weapons.Tick(owner,state,Projectile,null,false);FaceWork(state.Trees.Target);
                Light.Update(owner,state,Projectile,true);state.Status=state.Trees.Status;return;
            }
            if(!state.Construction.Active&&!state.Digging.Active&&!state.Mining.Active&&!state.Trees.Active&&targetItem<0&&state.Garden.Tick(owner,state,Projectile,false)) {
                Vector2 gardenGoal=Vector2.DistanceSquared(Projectile.Center,state.Garden.Target)<32*32?Projectile.Center:state.Garden.Target;
                if(Navigation.UpdateWorkFlight(Projectile,owner,gardenGoal,true)) {
                    Weapons.Tick(owner,state,Projectile,null,false);FaceWork(state.Garden.Target);Light.Update(owner,state,Projectile,true);return;
                }
            }
        }
        if(!state.Paused&&!state.Vitals.Downed&&Navigation.Riding&&!airWorkDanger&&!state.Construction.Active&&!state.Digging.Active&&!state.Mining.Active&&!state.Trees.Active&&!state.Garden.Active&&!state.Order.Active&&!state.Storage.Active&&targetItem<0&&!Supplies.Active){
            if(Fishing.Tick(owner,state,Projectile)){
                if(!Fishing.Active)Navigation.UpdateWorkFlight(Projectile,owner,Fishing.MoveTarget,true);
                Weapons.Tick(owner,state,Projectile,null,false);Light.Update(owner,state,Projectile,true);return;
            }
            Vector2 catchGoal=Projectile.Center;
            if(Catching.Tick(owner,state,Projectile,ref catchGoal)&&Navigation.UpdateWorkFlight(Projectile,owner,catchGoal,true)){
                Weapons.Tick(owner,state,Projectile,null,false);Light.Update(owner,state,Projectile,true);return;
            }
        }
        // Selector retries between strokes are still the same work intent.
        if(!state.Paused&&!state.Vitals.Downed&&Navigation.Riding&&!airWorkDanger&&state.Digging.Active&&!state.Mining.Active&&Vector2.DistanceSquared(owner.Center,Projectile.Center)<480*480){
            Projectile.velocity*=.5f;Weapons.Tick(owner,state,Projectile,null,false);Light.Update(owner,state,Projectile,true);return;
        }
        if(!state.Paused&&!state.Vitals.Downed&&Navigation.UpdateReturn(Projectile,owner,state.Combat?airborneTarget:null,airWeapon,tactics.Cautious,HoldingThreat)) {
            // Nearby hovering/landing is still a working state. Only genuine
            // long-distance catch-up clears the owner's current digging intention;
            // nearby combat suspends a still-valid mining job.
            if(Vector2.DistanceSquared(owner.Center,Projectile.Center)>=480*480)CancelTransitWork(state);
            else if(airborneTarget!=null){SuspendWorkForCombat(state);state.Mining.Tick(owner,Projectile.Center,false,true);}
            targetItem=-1;
            var airTarget=airborneTarget??FindThreat(owner);
            Weapons.Tick(owner,state,Projectile,airTarget,state.Combat&&(CompanionNativeUse.Summoning(airWeapon)||CompanionCombatReach.CanUse(airWeapon,Projectile,airTarget)));
            animationThreat=airTarget!=null;attackThrust=!CompanionNativeUse.SwingArm(airWeapon);
            attackVisualTicks=Weapons.Actor.itemAnimation;attackVisualDuration=Math.Max(1,Weapons.Actor.itemAnimationMax);
            if(airTarget!=null)attackFacing=airTarget.Center;
            // Holding light is fine; placement is forbidden while carried.
            Light.Update(owner,state,Projectile,true);state.Status=HoldingThreat?CompanionText.Message("CompanionCat.WatchingEnemies"):airTarget!=null?(tactics.Cautious?CompanionText.Message("CompanionCat.KeepingDistance"):"Guarding"):CompanionText.Message("CompanionCat.CatchingUp");return;
        }
        if(state.Vitals.Downed) {
            Light.Update(owner,state,Projectile,false);
            state.Construction.Cancel();state.Storage.Cancel();state.Mining.Cancel();state.Garden.Cancel();state.Trees.Cancel();state.Order.Cancel();targetItem=-1;
            state.Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.Recovering",(state.Vitals.Recovery+59)/60);
            Move(goal,owner,state);return;
        }
        if(state.Paused) { Light.Update(owner,state,Projectile,false);state.Construction.Cancel();state.Storage.Cancel();state.Mining.Cancel();state.Garden.Cancel();state.Trees.Cancel();targetItem=-1; Projectile.velocity.X*=0.7f; Projectile.velocity.Y=Math.Min(9,Projectile.velocity.Y+0.4f); state.Status="Paused"; return; }
        if(!state.Pickup)targetItem=-1;
        // Respect native focus marks and retain nearby targets across small movements.
        NPC target=FindThreat(owner);
        bool threat=target!=null;
        if(threat)SuspendWorkForCombat(state);
        animationThreat=threat;
        Light.Update(owner,state,Projectile,threat);
        if(target is {} npc) {
            state.Status=HoldingThreat?CompanionText.Message("CompanionCat.WatchingEnemies"):tactics.Cautious?CompanionText.Message("CompanionCat.KeepingDistance"):"Guarding";targetItem=-1;
            Item weapon=CompanionWeapons.Active(state.Gear);
            if(state.Combat)goal=HoldingThreat?Projectile.Center:CompanionCombatReach.Stand(weapon,Projectile,npc,tactics.Cautious);
            attackFacing=npc.Center;
        }
        Item activeWeapon=CompanionWeapons.Active(state.Gear);
        Weapons.Tick(owner,state,Projectile,target,state.Combat&&(CompanionNativeUse.Summoning(activeWeapon)||CompanionCombatReach.CanUse(activeWeapon,Projectile,target)));
        attackThrust=!CompanionNativeUse.SwingArm(activeWeapon);
        attackVisualTicks=Weapons.Actor.itemAnimation;attackVisualDuration=Math.Max(1,Weapons.Actor.itemAnimationMax);

        bool mining=state.Mining.Active;
        if(mining) {
            targetItem=-1;
            if(!threat&&Vector2.DistanceSquared(Projectile.Center,state.Mining.MoveTarget)<24*24)FaceWork(state.Mining.Target);
            state.Mining.Tick(owner,Projectile.Center,state.Paused,threat);
            if(!state.Mining.Active)state.Report(state.Mining.Status);
            if(!threat&&state.Mining.Active)goal=state.Mining.MoveTarget;
            if(!threat)state.Status=state.Mining.Status;
        }
        bool ordered=state.Order.Active;
        if(ordered) {
            targetItem=-1;
            if(!threat&&Vector2.DistanceSquared(Projectile.Center,state.Order.CurrentTarget)<56*56)FaceWork(state.Order.CurrentTarget);
            state.Order.Tick(owner,Projectile.Center,state.Bag,state.Paused,threat);
            if(!state.Order.Active)state.Report(state.Order.Status);
            if(!threat)goal=Vector2.DistanceSquared(Projectile.Center,state.Order.CurrentTarget)<56*56?Projectile.Center:state.Order.CurrentTarget;
            state.Status=state.Order.Status;
        }
        bool building=state.Construction.Tick(owner,state,Projectile,threat);
        if(building){targetItem=-1;goal=state.Construction.MoveTarget;}
        bool logistics=!state.Digging.Active&&!state.Trees.Requested&&!state.Construction.Active&&state.Storage.Tick(owner,state,Projectile,threat);
        if(logistics){targetItem=-1;goal=Vector2.DistanceSquared(Projectile.Center,state.Storage.Target)<60*60?Projectile.Center:state.Storage.Target;}
        bool gardening=!state.Digging.Active&&!state.Trees.Requested&&!state.Construction.Active&&state.Garden.Tick(owner,state,Projectile,threat);
        if(gardening){targetItem=-1;goal=Vector2.DistanceSquared(Projectile.Center,state.Garden.Target)<32*32?Projectile.Center:state.Garden.Target;}
        bool forestry=!state.Digging.Active&&!state.Construction.Active&&state.Trees.Tick(owner,state,Projectile,threat);
        if(forestry){targetItem=-1;goal=state.Trees.MoveTarget;}
        if(building||state.Digging.Active||forestry||gardening||logistics||mining||ordered||threat){Catching.Cancel();Supplies.Cancel();}
        if(!state.Construction.Active&&!state.Digging.Active&&!forestry&&!gardening&&!logistics&&!mining&&!ordered&&!threat&&Supplies.Tick(owner,state,Projectile,ref goal)){
            targetItem=-1;Fishing.Cancel();Catching.Cancel();Move(goal,owner,state);return;
        }
        if(!state.Construction.Active && !state.Digging.Active && !forestry && !gardening && !logistics && !mining && !ordered && !threat && state.Pickup) {
            targetItem=Pickup.Tick(owner,state,Projectile,ref goal);
            if(targetItem>=0){Catching.Cancel();Fishing.Cancel();}
        }
        if(!state.Construction.Active && !state.Digging.Active && !forestry && !gardening && !logistics && !mining && !ordered && !threat && targetItem<0) {
            if(Fishing.Tick(owner,state,Projectile)){if(Fishing.Active)Projectile.velocity.X*=.5f;else Move(Fishing.MoveTarget,owner,state);return;}
            if(Catching.Tick(owner,state,Projectile,ref goal)){Fishing.Cancel();Move(goal,owner,state);return;}
            state.Travel.Tick(owner,state,Projectile);
            if(state.Travel.Active)goal=state.Travel.MoveTarget;
            if(state.Travel.Status.Length>0)state.Status=state.Travel.Status;
            else if(tick%15==0)state.Status=state.Trees.Status.Length>0?state.Trees.Status:"Following";
        }
        if((Light.Holding||Light.Status.Length>0)&&!building&&!mining&&!ordered&&!logistics&&!gardening&&!forestry&&!threat)state.Status=Light.Status.Length>0?Light.Status:CompanionText.Message("CompanionCat.HoldingTorch");
        Move(goal,owner,state,threat&&state.Combat);
    }
    public override void PostAI() {
        if(!Projectile.active)return;
        var owner=Main.player[OwnerIndex];var s=owner.GetModPlayer<CompanionPlayer>();
        Diving.Update(Projectile,s.Paused,s.Vitals.Downed);
        face.Update(s.Vitals.Health,s.Vitals.Downed,(int)Projectile.ai[1]);
        if(CompanionNetwork.Client){
            Flight.Update(networkFlying&&!Diving.InWater,s.Paused,s.Vitals.Downed,Projectile.velocity.X);
            Light.Update(owner,s,Projectile,true);
            if(Light.Holding&&!Light.HoldingGlowstick)Light.Glow.Emit(Projectile,Light.HeldItem,Light.Flame(Projectile)-new Vector2(2,2));
            if(owner.whoAmI==Main.myPlayer){s.Expression.AdvancePresentation();s.Expression.ObserveEquipment(s,Projectile);s.Discovery.Tick(s,Projectile);}return;
        }
        Evasion.Update(owner,s,Projectile,Navigation);
        Flight.Update(Navigation.Riding&&!Diving.InWater,s.Paused,s.Vitals.Downed,Projectile.velocity.X);
        // A nearly zero vertical speed at the jump apex is not ground contact.
        bool drop=!s.Paused&&!s.Vitals.Downed&&Navigation.DropThrough;
        bool grounded=!Navigation.Riding&&Navigation.Grounded(Projectile);
        bool busy=s.Construction.Active||Fishing.Active||Supplies.Active||Catching.Active||s.Mining.Active||s.Order.Active||s.Storage.Active||s.Garden.Active||s.Trees.Active||targetItem>=0||Light.Holding;
        bool toolSwing=Fishing.Gesture||s.Mining.SwingTicks>0||s.Trees.SwingTicks>0||Catching.SwingTicks>0;
        float toolProgress=Fishing.Gesture?Fishing.GestureProgress:Catching.SwingTicks>0?1-Catching.SwingTicks/(float)Catching.Duration:s.Mining.SwingTicks>0?1-s.Mining.SwingTicks/(float)s.Mining.SwingDuration:1-s.Trees.SwingTicks/(float)s.Trees.SwingDuration;
        Animation.Update(Projectile.velocity.X,Projectile.velocity.Y,s.Vitals.Downed,s.Paused,
            toolSwing,attackVisualTicks>0,s.PetCooldown>510,animationThreat,busy,
            Vector2.DistanceSquared(s.WorkAnchor,Projectile.Center)<120*120&&Math.Abs(owner.velocity.X)<0.2f,grounded,
            toolSwing?toolProgress:1-attackVisualTicks/(float)attackVisualDuration,Navigation.Riding,attackThrust,Diving.Submerged&&!grounded);
        if(!s.Paused&&!s.Vitals.Downed) {
            if(attackVisualTicks>0)Projectile.direction=Facing(Projectile.Center,attackFacing,Projectile.direction);
            else if(!animationThreat) {
                if(toolSwing)FaceWork(Fishing.Gesture?Fishing.Aim:Catching.SwingTicks>0?Catching.Target:s.Mining.SwingTicks>0?s.Mining.SwingTarget:s.Trees.Target);
                if(workFacingTicks>0)Projectile.direction=Facing(Projectile.Center,workFacing,Projectile.direction);
            }
        }
        if(Animation.Motion==CatMotion.Rest)s.Status="Resting";
        s.Expression.Tick(s,Projectile);
        s.Discovery.Tick(s,Projectile);
        Supplies.UpdateCarried(Projectile);
        // Native chance, scale, velocity, gravity and TorchID light colour.
        if(Light.Holding&&!Light.HoldingGlowstick)Light.Glow.Emit(Projectile,Light.HeldItem,Light.Flame(Projectile)-new Vector2(2,2));
        bool sendMotion=tick%6==0;
        // Preserve the actual start/end of held-item visibility. A one-tick
        // native pause must not become a whole six-tick snapshot interval.
        bool weaponActive=Weapons.Busy;
        sendMotion|=weaponActive!=previousWeaponActive;
        previousWeaponActive=weaponActive;
        if(CompanionNetwork.Server){PendingPresentation|=sendMotion||Projectile.netUpdate;Projectile.netUpdate=false;}
    }
    NPC FindThreat(Player owner) {
        var state=owner.GetModPlayer<CompanionPlayer>();
        bool working=state.Directive.Active||state.Construction.Active||state.Digging.Active||state.Mining.Active||state.Trees.Active||state.Garden.Active||state.Order.Active||state.Storage.Active||targetItem>=0;
        var target=targeting.Select(owner,Projectile.Center,working);
        tactics.Update(state,target!=null);return target;
    }
    readonly CompanionCombatTactics tactics=new();
    internal bool HoldingThreat=>targeting.Holding;
    internal bool Cautious=>tactics.Cautious;
    public void StopWork(CompanionPlayer s){targeting.Clear();tactics.Update(s,false);CancelTransitWork(s);Weapons.Cancel();}
    // Ground and flight share the same interruption contract: preserve pending
    // world work and its progress, relinquish movement and transient tool use.
    void SuspendWorkForCombat(CompanionPlayer s) {
        Fishing.Cancel();Supplies.Cancel();Catching.Cancel();
        if(s.Trees.Active||s.Trees.ReplantPending)s.Trees.Suspend();
        targetItem=-1;workFacingTicks=0;
    }
    void CancelTransitWork(CompanionPlayer s) {
        s.Directive.Clear(s);
        SuspendWorkForCombat(s);
        s.Construction.Cancel();s.Order.Cancel();s.Storage.Cancel();s.Garden.Cancel();s.Trees.Cancel();s.Travel.ClearGoal();
        s.Mining.Cancel();s.Digging.Clear(s);attackVisualTicks=0;
    }
    void Move(Vector2 goal,Player owner,CompanionPlayer s,bool combat=false) {
        bool working=s.Directive.Active||s.Construction.Active||Supplies.Active||Catching.Active||s.Mining.Active||s.Order.Active||s.Storage.Active||s.Garden.Active||s.Trees.Active||targetItem>=0;
        if(Navigation.UpdateWorkFlight(Projectile,owner,goal,working&&!combat))return;
        if(Navigation.Update(Projectile,goal,owner,working||s.Travel.Active,combat)) {
            if(s.Mining.Active)s.Report(s.Mining.FailRoute());
            s.Construction.Cancel();s.Order.Cancel();s.Storage.Cancel();s.Garden.Cancel();if(s.Trees.Active){s.Trees.FailRoute();s.Report(s.Trees.Status);}
            targetItem=-1;workCooldown=180;s.Status=CompanionText.Message("CompanionCat.RouteBlocked");
            workFacingTicks=0;s.Expression.Say(s,Projectile,"RouteRecovered",Terraria.GameContent.UI.EmoteID.EmoteConfused);
        }
    }
    void DrawTools(CompanionPlayer state,Color light) {
        if(CompanionNetwork.Client){
            if(networkTool>0&&networkTool<Terraria.ModLoader.ItemLoader.ItemCount&&!state.Paused&&!state.Vitals.Downed){
                Vector2 hand=CompanionSprite.Hand(Projectile);float angle=CompanionItemVisual.SwingAngle((networkToolAim-hand).ToRotation(),networkToolProgress,Projectile.direction);
                var tool=new Item(networkTool);
                CompanionItemVisual.DrawSwing(Main.spriteBatch,tool,hand-Main.screenPosition,angle,Projectile.direction,light,tick,CompanionItemVisual.ToolRotation(tool,networkToolProgress,Projectile.direction));
                CompanionItemVisual.DrawGrip(Main.spriteBatch,(Projectile.Bottom+new Vector2(0,Projectile.gfxOffY)-Main.screenPosition).Floor(),Animation.SpriteFrame,Projectile.direction,light);
            }return;
        }
        if((state.Mining.SwingTicks>0||state.Trees.SwingTicks>0||Catching.SwingTicks>0)&&!state.Paused&&!state.Vitals.Downed) {
            bool mine=state.Mining.SwingTicks>0;var tool=Catching.SwingTicks>0?Catching.Tool:mine?state.Mining.SwingItem:state.Trees.SwingItem;
            if(tool==null||tool.IsAir)return;
            Vector2 hand=CompanionSprite.Hand(Projectile);
            float progress=Catching.SwingTicks>0?1-Catching.SwingTicks/(float)Catching.Duration:mine?1-state.Mining.SwingTicks/(float)state.Mining.SwingDuration:1-state.Trees.SwingTicks/(float)state.Trees.SwingDuration;
            float aim=((Catching.SwingTicks>0?Catching.Target:mine?state.Mining.SwingTarget:state.Trees.Target)-hand).ToRotation();
            float angle=CompanionItemVisual.SwingAngle(aim,progress,Projectile.direction);
            CompanionItemVisual.DrawSwing(Main.spriteBatch,tool,hand-Main.screenPosition,angle,Projectile.direction,light,tick,CompanionItemVisual.ToolRotation(tool,progress,Projectile.direction));
            CompanionItemVisual.DrawGrip(Main.spriteBatch,(Projectile.Bottom+new Vector2(0,Projectile.gfxOffY)-Main.screenPosition).Floor(),Animation.SpriteFrame,Projectile.direction,light);
        }
    }
    public override bool PreDraw(ref Color lightColor) {
        var state=Main.player[OwnerIndex].GetModPlayer<CompanionPlayer>();
        if(CompanionRestVisual.Hidden(state.Vitals))return false;
        using var dyeScope=new CompanionDye.Scope(Projectile);
        lightColor=CompanionFeedback.ImmuneTint(lightColor,state.Vitals.Immunity,state.Vitals.Downed);
        var carried=CompanionCarriedItem.Find(this);
        bool gesture=carried==null&&ActionVisual.Visible(this,state);
        int pose=state.Vitals.Downed?CompanionRestVisual.Frame(state.Vitals):carried!=null?CompanionCarriedItem.Frame:gesture?ActionVisual.Frame:Animation.SpriteFrame;
        var texture=CompanionSprite.Atlas;
        if(texture is null)return false;
        var source=new Rectangle(0,pose*CompanionSprite.Height,CompanionSprite.Width,CompanionSprite.Height);
        // Terraria supplies the world light here; do not draw the body with Color.White.
        var position=Projectile.Bottom+new Vector2(0,Projectile.gfxOffY)-Main.screenPosition;
        position=new Vector2((int)position.X,(int)position.Y);
        if(!Diving.InWater)Flight.Draw(Main.spriteBatch,position,Projectile.direction,lightColor,
            Lighting.GetColor((Projectile.Bottom+new Vector2(-8*Projectile.direction,-42)).ToTileCoordinates()));
        if(state.Vitals.Downed)position.Y+=CompanionRestVisual.BreathOffset(state.Vitals);
        bool riding=!state.Vitals.Downed&&(CompanionNetwork.Client?networkFlying:Navigation.Riding);
        bool swimming=Diving.Submerged&&!state.Vitals.Downed&&(riding||!Navigation.Grounded(Projectile));
        CompanionSprite.DrawBody(Main.spriteBatch,position,pose,Projectile.direction,lightColor,riding,swimming?Diving.Phase:-1);
        if(face.Closed)CompanionFace.Draw(Main.spriteBatch,position,pose,Projectile.direction,lightColor);
        if(Diving.Submerged)CompanionDivingVisual.DrawGoggles(Main.spriteBatch,position,pose,Projectile.direction,lightColor);
        if(Light.HoldingGlowstick)Light.Glow.Draw(Projectile,Light.HeldItem,pose,lightColor);
        else if(Light.Holding)
            CompanionTorchDrawing.Draw(Main.spriteBatch,texture,position,pose,Projectile.direction,lightColor,Lighting.GetColor(Light.Hand(Projectile).ToTileCoordinates()),tick,item:Light.HeldItem);
        DrawTools(state,lightColor);
        Weapons.Draw(lightColor);Fishing.Draw(lightColor);
        if(gesture)ActionVisual.Draw(Main.spriteBatch,position,Projectile.direction,lightColor);
        if(carried!=null)CompanionCarriedItem.Draw(carried,Projectile,position,lightColor);
        if(state.Vitals.Downed) {
            Main.instance.DrawHealthBar(Projectile.Center.X,Projectile.Bottom.Y+10+Projectile.gfxOffY,Math.Max(1,state.Vitals.RecoveryProgress),state.Vitals.RecoveryTotal,1f,1f);
            Utils.DrawBorderString(Main.spriteBatch,$"{(state.Vitals.Recovery+59)/60}s",Projectile.Bottom-Main.screenPosition+new Vector2(0,18),Color.LightGoldenrodYellow,.65f,.5f);
        }
        else if(ShowWorldHealth(state.Vitals)&&Main.HealthBarDrawSettings!=0)
            Main.instance.DrawHealthBar(Projectile.Center.X,Projectile.Bottom.Y+10+Projectile.gfxOffY,state.Vitals.Health,state.Vitals.MaxHealth,1f,1f);
        return false;
    }

}
