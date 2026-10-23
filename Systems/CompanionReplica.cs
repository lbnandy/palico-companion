using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
namespace PalicoCompanion.Systems;

// Presentation snapshots only. Inventory, hits and world edits remain on the
// server. Two snapshot intervals absorb ordinary packet timing variation.
internal struct CompanionPose {
    internal double Time,WalkPhase;
    internal Vector2 Position,Velocity,ItemOffset,ToolAim,WeaponAim;
    internal int Direction,ItemDirection,Frame,IdleTicks,StateTicks,ItemAnimation,ItemMax,Tool,ToolDuration,Epoch;
    internal float ItemRotation,ToolProgress;
    internal bool Flying,Channel;
    internal CatMotion Motion;
}

internal sealed class CompanionReplica {
    internal const int Delay=12,MaxExtrapolation=6;
    readonly List<CompanionPose> poses=new(32);
    double clock,arrival;
    bool recovering;
    internal bool Ready=>poses.Count>0;
    internal Vector2 Position {get;private set;}
    internal double Elapsed {get;private set;}
    internal void Clear(){poses.Clear();clock=arrival=Elapsed=0;recovering=false;}
    internal bool Receive(CompanionPose pose,double localTick) {
        if(Ready&&(pose.Time<poses[^1].Time||pose.Time==poses[^1].Time&&pose.Epoch==poses[^1].Epoch))return false;
        bool reset=!Ready||pose.Epoch!=poses[^1].Epoch;
        if(Ready){var last=poses[^1];double gap=pose.Time-last.Time;
            // Catch a genuine discontinuity even if an external teleport did
            // not use our explicit epoch. Ordinary jumps/flight keep the buffer.
            reset|=gap>120;
            float possible=96+(float)gap*Math.Max(16,last.Velocity.Length());
            reset|=Vector2.DistanceSquared(last.Position,pose.Position)>possible*possible;
        }
        if(reset){poses.Clear();clock=pose.Time-Delay;recovering=false;Position=pose.Position;}
        poses.Add(pose);arrival=localTick;
        if(poses.Count>32)poses.RemoveAt(0);
        return reset;
    }
    internal CompanionPose Advance(double localTick) {
        if(!Ready)return default;
        var latest=poses[^1];double target=latest.Time+(localTick-arrival)-Delay;
        // Packet arrival jitter is not a change in movement speed. Keep a small
        // phase deadband and correct ordinary drift by at most three percent.
        // Only a depleted buffer needs the faster recovery rate.
        double error=target-(clock+1);
        if(Math.Abs(error)>Delay)recovering=true;
        else if(Math.Abs(error)<=2)recovering=false;
        double correction=Math.Abs(error)<=2?0:Math.Clamp(error*.02,-.03,.03);
        if(recovering)correction=Math.Clamp(error*.02,-.25,.25);
        clock=Math.Min(clock+1+correction,latest.Time+MaxExtrapolation);
        while(poses.Count>1&&poses[1].Time<=clock)poses.RemoveAt(0);
        var a=poses[0];double elapsed=Math.Max(0,clock-a.Time);Elapsed=elapsed;
        var result=a;
        result.ItemAnimation=Math.Max(0,a.ItemAnimation-(int)elapsed);
        if(poses.Count>1){var b=poses[1];float mix=(float)Math.Clamp(elapsed/(b.Time-a.Time),0,1);
            result.Position=Vector2.Lerp(a.Position,b.Position,mix);
            result.Velocity=Vector2.Lerp(a.Velocity,b.Velocity,mix);
            result.WeaponAim=Vector2.Lerp(a.WeaponAim,b.WeaponAim,mix);
            // Native ItemCheck and held projectiles can renew an animation
            // between snapshots. Two active endpoints are not a completed
            // single use: keep the received pose visible through that interval.
            // An idle endpoint (or no next packet) still lets the timer expire.
            if(a.ItemAnimation>0&&b.ItemAnimation>0&&a.ItemMax==b.ItemMax)
                result.ItemAnimation=Math.Max(1,result.ItemAnimation);
            // Do not interpolate across a new weapon swing or a facing flip.
            if(a.ItemDirection==b.ItemDirection&&a.ItemMax==b.ItemMax&&b.ItemAnimation<=a.ItemAnimation){
                result.ItemOffset=Vector2.Lerp(a.ItemOffset,b.ItemOffset,mix);
                result.ItemRotation=a.ItemRotation+MathHelper.WrapAngle(b.ItemRotation-a.ItemRotation)*mix;
            }
        }else result.Position+=a.Velocity*(float)Math.Min(elapsed,MaxExtrapolation);
        if(a.Tool>0){result.ToolProgress=Math.Clamp(a.ToolProgress+(float)elapsed/Math.Max(1,a.ToolDuration),0,1);if(result.ToolProgress>=1)result.Tool=0;}
        Position=result.Position;
        return result;
    }
}
