using System;
using Microsoft.Xna.Framework;
using Terraria;
namespace PalicoCompanion.Systems;

// Metadata for verified native flight rules, in projectile updates. Keep the
// aiming calculation shared; optional adapters only supply their own physics.
internal readonly record struct CompanionBallisticProfile(float Gravity,int Delay=0,float Drag=1,float GravityRamp=0,float MaxFallSpeed=16) {
    internal Vector2 Displacement(float time){
        int whole=(int)time;
        return Vector2.Lerp(Step(whole),Step(whole+1),time-whole);
    }
    Vector2 Step(int t){
        int falling=Math.Max(0,t-Delay);
        float horizontal=Drag==1?t:Math.Min(t,Delay)+Drag*(1-MathF.Pow(Drag,falling))/(1-Drag);
        float drop=Gravity*.5f*falling*(falling+1);
        if(GravityRamp>0){
            int ramp=Math.Min(falling,(int)(Gravity/GravityRamp)),constant=falling-ramp;
            float rampSpeed=GravityRamp*.5f*ramp*(ramp+1);
            drop=GravityRamp*ramp*(ramp+1)*(ramp+2)/6+constant*rampSpeed+Gravity*.5f*constant*(constant+1);
        }
        return new Vector2(horizontal,drop);
    }
}

internal static class CompanionBallistics {
    internal static Vector2 Lead(NPC target,float updates,int extraUpdates){
        var lead=target.velocity*Math.Min(updates/(extraUpdates+1),30);
        return lead.LengthSquared()>96*96?lead.SafeNormalize(Vector2.Zero)*96:lead;
    }
    internal static bool TryAim(Vector2 origin,NPC target,float speed,Projectile sample,CompanionBallisticProfile flight,out Vector2 point){
        point=target.Center;
        if(!float.IsFinite(speed)||speed<=0)return false;
        Vector2 Velocity(float time){
            var delta=target.Center+Lead(target,time,sample.extraUpdates)-origin;
            var travel=flight.Displacement(time);
            return new Vector2(delta.X/travel.X,(delta.Y-travel.Y)/time);
        }
        // The first solution is the low, fast arc. Bounded flight time avoids
        // launching an unreachable throw almost vertically; normal reach
        // feedback can instead bring the companion closer after missed uses.
        for(int end=1;end<=Math.Min(120,sample.timeLeft);end++){
            if(Velocity(end).LengthSquared()>speed*speed)continue;
            float lo=Math.Max(.01f,end-1),hi=end;
            for(int i=0;i<10;i++){
                float mid=(lo+hi)*.5f;
                if(Velocity(mid).LengthSquared()>speed*speed)lo=mid;else hi=mid;
            }
            var velocity=Velocity(hi);
            // Do not extrapolate linear gravity beyond the native falling cap.
            float fallSpeed=flight.Displacement(end).Y-flight.Displacement(end-1).Y;
            if(velocity.Y+fallSpeed>flight.MaxFallSpeed)return false;
            point=origin+velocity*hi;
            return float.IsFinite(point.X)&&float.IsFinite(point.Y);
        }
        return false;
    }
}
