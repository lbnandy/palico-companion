using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// The server owns movement, commitment and damage. Replicas receive only the
// phase clock; repeated projectile snapshots never restart the lift or tumble.
internal sealed class CompanionBarrelCharge {
    internal const int BlastSize=288,LiftTicks=30,RushTicks=100,RecoverTicks=60;
    internal const int ProneTicks=18,GetUpFrameTicks=11,SettleTicks=ProneTicks+2*GetUpFrameTicks;
    const int RetryTicks=120;
    const float Reach=320;
    enum Stage:byte { None,Lift,Rush,Recover,Landing,Settle }
    readonly CompanionBarrelLanding landing=new();
    Stage stage;
    uint sequence;
    ulong started;
    int target=-1,targetType,retry,travelTicks;
    bool automaticLanding;
    Vector2 origin;
    float floorY;
    static float Speed=>Math.Clamp(5f*CompanionBalanceConfig.Current.MovementScale(true),1.5f,8f);
    internal bool Active=>stage!=Stage.None;
    internal bool Preparing=>stage==Stage.Landing;
    internal bool Lifting=>stage==Stage.Lift;
    internal bool Presenting=>stage is Stage.Lift or Stage.Rush or Stage.Recover or Stage.Settle;
    internal bool Carrying=>stage is Stage.Lift or Stage.Rush;
    internal int Age=>(int)Math.Min(1000ul,Main.GameUpdateCount>=started?Main.GameUpdateCount-started:0);
    internal static int Damage(int tier)=>CompanionTraining.BombDamage(tier)*2;
    static bool Working(CompanionPlayer s)=>s.Directive.Active||s.Construction.Active||s.Digging.Active||s.Mining.Active||s.Trees.Active||s.Garden.Active||s.Order.Active||s.Storage.Active;
    static bool Enemy(NPC n)=>n.active&&n.CanBeChasedBy()&&!n.friendly&&!n.dontTakeDamage;
    static bool OnGround(NPC n,Projectile cat)=>Math.Abs(n.Center.Y-cat.Center.Y)<=96&&
        Math.Abs(n.velocity.Y)<3&&CompanionNavigation.Supported(n.position,n.width,n.height);
    static int Group(NPC focus){
        var roots=new HashSet<int>();var area=new Rectangle((int)focus.Center.X-BlastSize/2,(int)focus.Center.Y-BlastSize/2,BlastSize,BlastSize);
        foreach(var n in Main.npc)if(Enemy(n)&&area.Intersects(n.Hitbox))roots.Add(n.realLife>=0?n.realLife:n.whoAmI);
        return roots.Count;
    }
    internal static bool Route(Projectile cat,NPC n)=>Enemy(n)&&OnGround(n,cat)&&
        Vector2.DistanceSquared(n.Hitbox.ClosestPointInRect(cat.Center),cat.Center)<=Reach*Reach&&
        CompanionBarrelNavigation.Plan(cat,n,Speed,RushTicks,cat.position.Y,out _,true);
    static bool Headroom(Vector2 center)=>CompanionBarrelNavigation.Clear(center);
    NPC Select(CompanionPlayer s,CompanionCat cat,bool requested,bool needsLanding,out Vector2 landingPoint){
        landingPoint=default;NPC best=null;float score=float.MinValue;
        foreach(var n in Main.npc){
            if(!requested&&!CompanionCombatBehavior.Allows(s,n))continue;
            if(needsLanding&&!requested&&!CompanionTargeting.Valid(s.Player,cat.Projectile.Center,n))continue;
            if(!Enemy(n)||Vector2.DistanceSquared(n.Hitbox.ClosestPointInRect(cat.Projectile.Center),cat.Projectile.Center)>Reach*Reach)continue;
            int group=Group(n);if(!requested&&!n.boss&&group<2)continue;
            float rank=group*1000+(n.boss?200:0)-Vector2.Distance(cat.Projectile.Center,n.Center);
            if(requested&&n.whoAmI==s.Player.MinionAttackTargetNPC)rank+=100000;
            if(rank<=score)continue;
            if(needsLanding){
                if(Math.Abs(n.velocity.Y)>=3||!CompanionNavigation.Supported(n.position,n.width,n.height)||
                    !landing.Find(cat.Projectile,s.Player,n,Speed,out var point)||
                    !requested&&!CompanionBarrelLanding.KeepsUp(s.Player,point))continue;
                landingPoint=point;
            }else if(!Route(cat.Projectile,n))continue;
            best=n;score=rank;
        }
        return best;
    }
    internal bool TryStart(CompanionPlayer s,CompanionCat cat,bool requested){
        if(CompanionNetwork.Client||s.CombatMode==CompanionCombatMode.Passive||Active||retry>0||!s.Training.Ready(s,TrainingCatalog.Barrel)||!requested&&!s.Combat||
            Working(s)||cat.ActionVisual.Active||cat.Fishing.Active||cat.Catching.Active||cat.Supplies.Active||s.Vitals.Health<=s.Vitals.MaxHealth*.35f)return false;
        bool needsLanding=cat.Navigation.Riding||!cat.Navigation.Grounded(cat.Projectile);
        // Use the ordinary combat selector, not weapon animations or hits:
        // empty ammo and out-of-reach weapons must not prevent a support skill.
        if(needsLanding&&!requested&&(cat.EngagedTarget==null||cat.Navigation.WorkFlying||
            Vector2.DistanceSquared(s.Player.Center,cat.Projectile.Center)>=480*480)||!needsLanding&&!Headroom(cat.Projectile.Center))return false;
        var chosen=Select(s,cat,requested,needsLanding,out var point);
        if(chosen==null){if(!requested)retry=30;return false;}
        if(requested)CompanionCombatBehavior.Engage(s,chosen);
        target=chosen.whoAmI;targetType=chosen.type;
        cat.Weapons.Cancel();
        if(needsLanding){automaticLanding=!requested;landing.Begin(cat,point);Change(cat,Stage.Landing);return true;}
        BeginLift(s,cat,chosen);return true;
    }
    void BeginLift(CompanionPlayer s,CompanionCat cat,NPC chosen){
        origin=cat.Projectile.Center;floorY=cat.Projectile.position.Y;travelTicks=0;
        cat.Projectile.velocity.X=0;cat.Projectile.direction=CompanionCat.Facing(cat.Projectile.Center,chosen.Center,cat.Projectile.direction);
        Change(cat,Stage.Lift);s.Expression.GearUsed(s,TrainingCatalog.Barrel);
    }
    void Change(CompanionCat cat,Stage next){stage=next;started=Main.GameUpdateCount;sequence++;if(sequence==0)sequence=1;cat.PendingPresentation=true;}
    internal void Cancel(CompanionCat cat){if(!Active)return;if(Preparing)landing.Cancel(cat);target=-1;retry=RetryTicks;Change(cat,Stage.None);}
    internal bool Tick(CompanionPlayer s,CompanionCat cat){
        if(CompanionNetwork.Client)return Active;
        if(retry>0)retry--;
        if(!Active)return false;
        var p=cat.Projectile;
        if(!s.Training.Active(s)||s.Training.Find(s,TrainingCatalog.Barrel)<0||Working(s)||s.PetCooldown>510||
            Vector2.DistanceSquared(s.Player.Center,p.Center)>480*480){Cancel(cat);return false;}
        if(s.CombatMode==CompanionCombatMode.Passive&&(Preparing||Carrying)){Cancel(cat);return false;}
        cat.Weapons.Tick(s.Player,s,p,null,false);
        p.tileCollide=true;
        s.Status=CompanionText.Message(Preparing?"CompanionBarrelCharge.Landing":"CompanionBarrelCharge.Busy");
        if(Preparing){
            NPC enemy=target>=0&&target<Main.maxNPCs?Main.npc[target]:null;
            if(automaticLanding&&(!s.Combat||!landing.KeepsUp(s.Player))||Age>=CompanionBarrelLanding.Timeout||s.Vitals.Health<=s.Vitals.MaxHealth*.35f||enemy==null||enemy.type!=targetType||!Enemy(enemy)||
                Math.Abs(enemy.velocity.Y)>=3||!CompanionNavigation.Supported(enemy.position,enemy.width,enemy.height)||
                Vector2.DistanceSquared(enemy.Hitbox.ClosestPointInRect(p.Center),p.Center)>Reach*Reach||!landing.Valid){Cancel(cat);return false;}
            if(landing.Arrived(cat)){
                if(!Route(p,enemy)){Cancel(cat);return false;}
                landing.Finish(cat);BeginLift(s,cat,enemy);return true;
            }
            if(!landing.Approach(cat)){Cancel(cat);return false;}
            return true;
        }
        if(stage==Stage.Recover){
            p.velocity.Y=Math.Min(10,p.velocity.Y+.35f);p.velocity.X*=.97f;
            // Start the small landing beat on authoritative contact, not on a
            // visual timer that could finish while still falling over a ledge.
            if(Age>=4&&p.velocity.Y>=0&&cat.Navigation.Grounded(p)){
                p.velocity.X*=.25f;Change(cat,Stage.Settle);
            }else if(Age>=RecoverTicks)Change(cat,Stage.None);
            return true;
        }
        if(stage==Stage.Settle){
            p.velocity.Y=Math.Min(10,p.velocity.Y+.35f);p.velocity.X*=.65f;
            if(Age>=SettleTicks||!cat.Navigation.Grounded(p))Change(cat,Stage.None);
            return true;
        }
        if(Vector2.DistanceSquared(origin,p.Center)>Reach*Reach||++travelTicks>LiftTicks+RushTicks){Cancel(cat);return false;}
        NPC chosen=target>=0&&target<Main.maxNPCs?Main.npc[target]:null;
        if(chosen==null||chosen.type!=targetType||!Enemy(chosen)||!OnGround(chosen,p)){
            // Retarget only along the already chosen charge direction.
            chosen=null;
            foreach(var n in Main.npc)if(Enemy(n)&&(n.Center.X-p.Center.X)*p.direction>=-16&&Route(p,n)){chosen=n;break;}
            if(chosen==null){Cancel(cat);return false;}
            target=chosen.whoAmI;targetType=chosen.type;
        }
        if(stage==Stage.Lift){p.velocity.Y=Math.Min(10,p.velocity.Y+.35f);p.velocity.X=0;if(Age>=LiftTicks)Change(cat,Stage.Rush);return true;}
        // The barrel leads the contact body, so the gag does not require taking
        // contact damage first. Other incoming damage keeps its normal rules.
        foreach(var n in Main.npc)if(Enemy(n)&&CompanionBarrelNavigation.Contact(p.position,n)){
            Detonate(s,cat);return true;
        }
        if(!Headroom(p.Center)||Age>=RushTicks){Cancel(cat);return false;}
        int direction=CompanionCat.Facing(p.Center,chosen.Center,p.direction);
        if(direction!=p.direction){Cancel(cat);return false;}
        if(cat.Navigation.Grounded(p))floorY=p.position.Y;
        if(!CompanionBarrelNavigation.Plan(p,chosen,Speed,RushTicks-Age,floorY,out var move)){Cancel(cat);return false;}
        p.position=move.Position;p.velocity=move.Velocity;p.gfxOffY+=move.GfxOffset;
        return true;
    }

    void Detonate(CompanionPlayer s,CompanionCat cat){
        var use=cat.Weapons;var p=cat.Projectile;
        if(!s.Training.Ready(s,TrainingCatalog.Barrel)||!Array.Exists(Main.projectile,q=>!q.active)){Cancel(cat);return;}
        if(use.State==null)use.BindReplica(s.Player,s,p);
        int id;
        using(var scope=new CompanionNativeUse.Scope(use))
            id=Projectile.NewProjectile(p.GetSource_FromThis(),p.Center+new Vector2(p.direction*12,-6),Vector2.Zero,ModContent.ProjectileType<TrainingBarrelBlast>(),Damage(s.Training.Tier),6,use.Actor.whoAmI);
        if(id>=Main.maxProjectiles){Cancel(cat);return;}
        Main.projectile[id].netUpdate=true;
        s.Training.Use(s,TrainingCatalog.Barrel,CompanionTraining.CooldownSeconds(TrainingCatalog.Barrel));
        CompanionFeedback.Show(6,Main.projectile[id].Center,TrainingCatalog.Barrel,"impact");
        p.velocity=new Vector2(-p.direction*4.8f,-4.8f);target=-1;
        Change(cat,Stage.Recover);
    }
    internal void Write(BinaryWriter w){w.Write(sequence);w.Write((byte)stage);w.Write((ushort)Age);}
    internal void Read(BinaryReader r,bool fresh){
        uint seq=r.ReadUInt32();byte phase=r.ReadByte();int age=r.ReadUInt16();
        if(!fresh&&seq<=sequence)return;
        sequence=seq;stage=phase<=(byte)Stage.Settle?(Stage)phase:Stage.None;
        started=Main.GameUpdateCount>=(ulong)age?Main.GameUpdateCount-(ulong)age:0;
    }
    internal void Draw(SpriteBatch batch,Vector2 feet,CompanionCat cat,Color light)=>CompanionBarrelVisual.Draw(batch,feet,cat,light,(int)stage,Age);
}

public sealed class TrainingBarrelBlast:ModProjectile {
    readonly HashSet<int> roots=new();
    public override string Texture=>"Terraria/Images/Projectile_0";
    public override void SetDefaults(){Projectile.width=Projectile.height=CompanionBarrelCharge.BlastSize;Projectile.friendly=true;Projectile.hostile=false;Projectile.tileCollide=false;Projectile.penetrate=-1;Projectile.timeLeft=2;Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=-1;}
    public override bool? CanDamage()=>!CompanionNetwork.Client;
    public override bool CanHitPvp(Player target)=>false;
    public override bool? CanCutTiles()=>false;
    public override bool? CanHitNPC(NPC target)=>roots.Contains(target.realLife>=0?target.realLife:target.whoAmI)?false:null;
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)=>roots.Add(target.realLife>=0?target.realLife:target.whoAmI);
    public override bool PreDraw(ref Color lightColor)=>false;
}
