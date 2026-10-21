using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
namespace PalicoCompanion.Systems;

// Local ground navigation, not an unlimited tile pathfinder. Predict whole-body
// collision before jumping; recovery handles routes outside this short horizon.
public sealed class CompanionNavigation
{
    public const int Width=24, Height=28;
    int jumpCooldown, recoveryCooldown, noProgress;
    float bestDistance=float.MaxValue;
    Vector2 trackedGoal;
    bool trackedWork;
    internal readonly CompanionFlightRoute WorkRoute=new();
    public static float FollowSpeed(float ownerHorizontalSpeed)=>Math.Clamp(Math.Abs(ownerHorizontalSpeed)+1.5f,3.8f,9f);
    public static Vector2 FollowGoal(Player owner,Vector2 catCenter) {
        Vector2 behind=owner.Center-new Vector2(owner.direction*52,0);
        // A collision-free pocket behind the player may be on the far side of
        // a cave wall. Reaching it must not count as successfully following.
        return Math.Abs(owner.Center.Y-catCenter.Y)>48||!Fits(behind)||!Reachability.Clear(behind,owner.Center)?owner.Center:behind;
    }
    public int Jumps { get; private set; }
    public int Recoveries { get; private set; }
    public bool Riding { get; private set; }
    public bool WorkFlying {get;private set;}
    internal void HoldFishingPosition(Projectile cat){
        if(!Riding&&!Supported(cat.position,cat.width,cat.height)&&Fits(cat.Center)){Riding=true;RideEntries++;Puff(cat.Center);}
        if(Riding){WorkFlying=true;cat.tileCollide=true;DropThrough=false;cat.velocity*=.5f;if(cat.velocity.LengthSquared()<.01f)cat.velocity=Vector2.Zero;}
        else cat.velocity.X*=.5f;
    }
    // Work flight never phases through terrain. Sample the whole collision body,
    // rather than only a center-point ray, before committing to an approach.
    public static bool ClearFlight(Vector2 from,Vector2 to) {
        float length=Vector2.Distance(from,to);if(length>640)return false;
        int steps=Math.Max(1,(int)Math.Ceiling(length/8));
        for(int i=0;i<=steps;i++)if(!Fits(Vector2.Lerp(from,to,i/(float)steps)))return false;
        return true;
    }
    public bool UpdateWorkFlight(Projectile cat,Player owner,Vector2 goal,bool working) {
        if(!working){WorkFlying=false;WorkRoute.Clear();return false;}
        if(!Riding&&!WorkFlying&&noProgress<45&&goal.Y>=cat.Center.Y-48)return false;
        if(Vector2.DistanceSquared(goal,owner.Center)>480*480||!WorkRoute.Next(cat.Center,goal,out var next))return false;
        if(!Riding){Riding=true;RideEntries++;Puff(cat.Center);}
        WorkFlying=true;cat.tileCollide=true;DropThrough=false;noProgress=0;
        Vector2 desired=(next-cat.Center)*0.2f;
        if(desired.LengthSquared()>36)desired=desired.SafeNormalize(Vector2.Zero)*6;
        cat.velocity=Vector2.Lerp(cat.velocity,desired,0.2f);
        // Brake away from a corner instead of carrying lateral inertia into it.
        if(!ClearFlight(cat.Center,cat.Center+cat.velocity*2))cat.velocity=desired*0.2f;
        if(Vector2.DistanceSquared(cat.Center,goal)<4&&cat.velocity.LengthSquared()<0.25f)cat.velocity=Vector2.Zero;
        if(Math.Abs(cat.velocity.X)>0.5f)cat.direction=Math.Sign(cat.velocity.X);
        return true;
    }
    public int RideEntries { get; private set; }
    int rideTicks,ownerGroundTicks;
    // Cattiva supplies the shoulder destination. AI_026 pets / AI_067's generic
    // return and ExampleSimpleMinion supply the complete velocity-based approach.
    // Do not copy Cattiva's direct Center assignment without its visual handling.
    public bool UpdateReturn(Projectile cat,Player owner,NPC combatTarget=null,Item weapon=null,bool cautious=false,bool obscured=false) {
        WorkFlying=false;
        Vector2 separation=owner.Center-cat.Center;
        if(!Riding&&(separation.LengthSquared()>500*500||Math.Abs(separation.Y)>300||noProgress>=120)) {
            Riding=true;RideEntries++;rideTicks=ownerGroundTicks=0;Puff(cat.Center);
        }
        if(!Riding){cat.tileCollide=true;return false;}
        cat.tileCollide=false;rideTicks++;
        Vector2 destination=owner.MountedCenter+new Vector2(-owner.direction*24,-30);
        // Vanilla's shoulder offset can sit inside a narrow shaft wall. Return
        // through terrain as usual, but settle in a whole-body empty pocket so
        // nearby work can resume instead of failing its Fits guard forever.
        if(!Fits(destination)) {
            foreach(var offset in new[]{Vector2.Zero,new Vector2(0,-16),new Vector2(0,-32),new Vector2(-8,0),new Vector2(8,0),new Vector2(-16,0),new Vector2(16,0),new Vector2(0,-48)}) {
                var candidate=owner.Center+offset;
                if(Fits(candidate)&&Reachability.Clear(candidate,owner.Center)){destination=candidate;break;}
            }
        }
        bool engage=weapon!=null&&combatTarget!=null&&(obscured||cautious||!CompanionCombatReach.Distance(weapon)||
            Vector2.DistanceSquared(destination,combatTarget.Hitbox.ClosestPointInRect(destination))>MathF.Pow(CompanionCombatReach.EffectiveRange(weapon,cat,combatTarget)*.8f,2)||
            combatTarget.Hitbox.Intersects(new Rectangle((int)destination.X-cat.width/2,(int)destination.Y-cat.height/2,cat.width,cat.height)))&&Vector2.DistanceSquared(combatTarget.Center,owner.Center)<700*700&&
            separation.LengthSquared()<900*900&&(obscured||CompanionCombatReach.Clear(cat.Center,combatTarget.Center));
        if(engage) {
            destination=obscured?cat.Center:CompanionCombatReach.Stand(weapon,cat,combatTarget,cautious);
        }
        if(separation.LengthSquared()>2000*2000) {
            // Vanilla pets retain an emergency relocation for multi-screen gaps.
            // Normal catch-up, direction changes and landing never use this path.
            CompanionTransitEffects.Move(cat,destination);Recoveries++;
        }
        bool landed=Math.Abs(owner.velocity.Y)<0.6f&&Supported(owner.position,owner.width,owner.height);
        ownerGroundTicks=landed?ownerGroundTicks+1:0;
        bool landing=false;
        if(!engage&&rideTicks>=30&&ownerGroundTicks>=6&&separation.LengthSquared()<200*200) {
            foreach(int dx in new[]{-32,32,-48,48,0,-64,64})
            foreach(int dy in new[]{0,-16,16}) {
                var candidate=owner.Bottom+new Vector2(dx,dy-cat.height/2f);
                if(!TryStand(candidate,out var stand)||!Reachability.Clear(stand,owner.Center))continue;
                destination=stand;landing=true;goto LandingFound;
            }
        }
        LandingFound:
        if(landing&&Vector2.DistanceSquared(cat.Center,destination)<=2*2) {
            cat.Center=destination;cat.velocity=Vector2.Zero;cat.tileCollide=true;
            Riding=false;noProgress=0;bestDistance=float.MaxValue;jumpCooldown=20;DropThrough=false;
            return true;
        }
        // Accelerate, turn and brake instead of snapping to the destination.
        // Include owner motion so a fast wing user cannot outrun the return.
        Vector2 delta=destination-cat.Center;
        float speed=Math.Max(10,owner.velocity.Length()+4);
        Vector2 desired=delta*0.2f+(engage?(obscured?Vector2.Zero:combatTarget.velocity):owner.velocity);
        if(desired.LengthSquared()>speed*speed)desired=desired.SafeNormalize(Vector2.Zero)*speed;
        cat.velocity=Vector2.Lerp(cat.velocity,desired,engage?0.3f:0.15f);
        if(cat.velocity.LengthSquared()>speed*speed)cat.velocity=cat.velocity.SafeNormalize(Vector2.Zero)*speed;
        if(Math.Abs(cat.velocity.X)>0.5f)cat.direction=Math.Sign(cat.velocity.X);
        else if(delta.LengthSquared()<48*48)cat.direction=owner.direction;
        // Even the landing tick is transit-only; resume jobs on the next tick.
        return true;
    }
    public void HoldReturn(Projectile cat) {
        WorkRoute.Clear();
        WorkFlying=false;
        cat.velocity=Vector2.Zero;
        if(Fits(cat.Center)){Riding=false;cat.tileCollide=true;noProgress=0;DropThrough=false;}
        else cat.tileCollide=false;
    }
    public int NoProgress => noProgress;
    public bool DropThrough { get; private set; }
    public bool Grounded(Projectile cat) {
        if(cat.velocity.Y<0)return false;
        return Supported(cat.position,cat.width,cat.height,DropThrough);
    }
    public static bool Supported(Vector2 position,int width=Width,int height=Height,bool dropThrough=false) {
        if(Collision.TileCollision(position,new Vector2(0,2),width,height,dropThrough).Y<2)return true;
        // TileCollision deliberately skips the sloped surface. The projectile
        // engine resolves it separately; use that same resolver for foot support.
        var probe=position+new Vector2(0,2);
        var slope=Collision.SlopeCollision(probe,new Vector2(0,2),width,height,0,dropThrough);
        return slope.Y<probe.Y-0.1f&&slope.W<2;
    }
    // A tile-grid candidate can sit 8px above a half block. Resolve at most one
    // tile downward; do not route to unsupported midair or through a ceiling.
    public static bool TryStand(Vector2 candidate,out Vector2 stand) {
        stand=candidate;
        for(int down=0;down<=16;down+=2) {
            var point=candidate+new Vector2(0,down);
            if(!Fits(point))return false;
            if(Supported(point-new Vector2(Width/2f,Height/2f))) {stand=point;return true;}
        }
        return false;
    }
    public static bool Fits(Vector2 center,int width=Width,int height=Height) =>
        WorldGen.InWorld((int)(center.X/16),(int)(center.Y/16),10) &&
        !Collision.SolidCollision(center-new Vector2(width/2f,height/2f),width,height) &&
        !Collision.WetCollision(center-new Vector2(width/2f,height/2f),width,height);

    public bool Update(Projectile cat,Vector2 goal,Player owner,bool working,bool combat=false)
    {
        if(jumpCooldown>0)jumpCooldown--;
        if(recoveryCooldown>0)recoveryCooldown--;
        DropThrough=goal.Y>cat.Center.Y+24;
        float distance=Vector2.Distance(cat.Center,goal);
        if(Vector2.DistanceSquared(trackedGoal,goal)>32*32) {
            trackedGoal=goal;bestDistance=distance;
            // A recalculated work stand is not evidence of actual travel.
            if(working&&!trackedWork)noProgress=0;
        }
        trackedWork=working;
        // Progress is reduction in distance, not mere movement. A repeated jump
        // against the same ceiling must not reset this timer every half second.
        if(distance<bestDistance-12){bestDistance=distance;noProgress=0;}
        bool blocked=!Reachability.Clear(cat.Center,goal);
        // A work stand must be reached, even when a small ledge lies inside the
        // comfortable follow radius. Otherwise we stop below it forever.
        bool needsTravel=distance>(combat?8:working?8:48)||blocked;
        if(needsTravel)noProgress++;else {noProgress=0;bestDistance=distance;}
        float dx=goal.X-cat.Center.X;
        float desired=Math.Abs(dx)> (working?6:20)?Math.Sign(dx)*(working?3.8f:FollowSpeed(owner.velocity.X)):0;
        if(combat)desired=Math.Clamp(dx*.25f,-FollowSpeed(owner.velocity.X),FollowSpeed(owner.velocity.X));
        bool ground=Grounded(cat);
        cat.velocity.X=MathHelper.Lerp(cat.velocity.X,desired,combat?0.3f:0.18f);
        if(Math.Abs(dx)>8)cat.direction=Math.Sign(dx);
        if(ground&&!DropThrough&&desired!=0) {
            // Same 1.4.4 collision API used by AI_026 ground pets for tile steps.
            Collision.StepUp(ref cat.position,ref cat.velocity,cat.width,cat.height,ref cat.stepSpeed,ref cat.gfxOffY);
        }
        bool wall=Collision.TileCollision(cat.position,new Vector2(Math.Sign(dx)*8,0),cat.width,cat.height,DropThrough).X!=Math.Sign(dx)*8;
        if(ground&&jumpCooldown==0&&needsTravel&&(wall||goal.Y<cat.Center.Y-32)) {
            float baseline=LandingDistance(cat,goal,0,desired);
            float best=baseline;float jump=0;
            foreach(float impulse in new[]{-4.5f,-6f,-7.5f}) {
                float score=LandingDistance(cat,goal,impulse,desired);
                if(score<best-12){best=score;jump=impulse;}
            }
            jumpCooldown=30;
            if(jump!=0){cat.velocity.Y=jump;Jumps++;}
        }
        cat.velocity.Y=Math.Min(10,cat.velocity.Y+0.35f);
        bool far=Vector2.DistanceSquared(owner.Center,cat.Center)>900*900;
        bool extreme=Vector2.DistanceSquared(owner.Center,cat.Center)>2000*2000;
        if(extreme||(recoveryCooldown==0&&(far||noProgress>=150||cat.lavaWet))) {
            // Work is cancelled by the caller before any later hit/placement.
            // Prefer grounded positions near the owner, with an empty body box.
            for(int pass=0;pass<2;pass++)
            foreach(int ox in new[]{0,-24,24,-48,48})
            foreach(int oy in new[]{0,-16,16,-32,32}) {
                Vector2 safe=owner.Bottom+new Vector2(ox,oy-cat.height/2f);
                if(!Fits(safe,cat.width,cat.height))continue;
                if(pass==0&&Collision.TileCollision(safe-cat.Size/2,new Vector2(0,4),cat.width,cat.height).Y>=4)continue;
                if(Vector2.DistanceSquared(safe,cat.Center)<24*24&&!blocked&&!far)continue;
                CompanionTransitEffects.Move(cat,safe);
                cat.velocity=Vector2.Zero;noProgress=0;bestDistance=float.MaxValue;
                recoveryCooldown=180;Recoveries++;return true;
            }
        }
        return false;
    }
    static float LandingDistance(Projectile cat,Vector2 goal,float impulse,float desired)
    {
        Vector2 pos=cat.position,velocity=new(cat.velocity.X,impulse);
        bool drop=goal.Y>cat.Center.Y+24;
        for(int t=0;t<60;t++) {
            float dx=goal.X-(pos.X+cat.width/2f);
            float vx=Math.Abs(dx)>6?Math.Sign(dx)*Math.Abs(desired):0;
            velocity.X=MathHelper.Lerp(velocity.X,vx,0.18f);
            velocity.Y=Math.Min(10,velocity.Y+0.35f);
            var allowed=Collision.TileCollision(pos,velocity,cat.width,cat.height,drop);
            if(velocity.Y<0&&allowed.Y>velocity.Y+0.1f)return float.MaxValue; // ceiling
            pos+=allowed;
            if(t>3&&velocity.Y>0&&allowed.Y<velocity.Y) {
                var center=pos+cat.Size/2;
                return Fits(center,cat.width,cat.height)?Vector2.Distance(center,goal):float.MaxValue;
            }
            if(allowed.X!=velocity.X)velocity.X=0;
        }
        return float.MaxValue; // no safe landing in the bounded prediction
    }
    static void Puff(Vector2 at)
    {
        // Reuse vanilla smoke particles (also used by AI_026 pets), not 1.4.5 assets.
        for(int n=0;n<8;n++) {
            var dust=Dust.NewDustDirect(at-new Vector2(10,12),20,24,DustID.Smoke,0,-0.5f,140,default,0.8f);
            dust.velocity*=0.35f;dust.noGravity=true;
        }
    }
}
