using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Native dust rays can spawn and die between client updates. Capture their
// actual substeps (including bounces), then replay only vanilla dust AI.
// The server remains responsible for movement, collisions and all damage.
public sealed class CompanionInstantShot : GlobalProjectile {
    public override bool InstancePerEntity=>true;
    internal readonly record struct Step(Vector2 Position,Vector2 Velocity);
    List<Step> path;
    float localClock;bool recording;
    internal static bool IsTrail(Projectile p)=>p.type<ProjectileID.Count&&p.aiStyle==ProjAIStyleID.Ray&&p.MaxUpdates<=256;
    internal void Begin(Projectile p){path??=new();path.Clear();localClock=p.localAI[0];recording=true;}
    public override void PostAI(Projectile p){if(recording&&path.Count<256)path.Add(new Step(p.position,p.velocity));}
    internal void End(Projectile p){recording=false;if(path.Count>0)CompanionNetwork.Trail(p.type,localClock,path);}
    static bool Finite(Vector2 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y);
    internal static void Present(int type,float clock,Step[] path){
        if(Main.dedServ||path.Length==0||!ContentSamples.ProjectilesByType.TryGetValue(type,out var sample)||!IsTrail(sample)||!float.IsFinite(clock))return;
        if(Vector2.DistanceSquared(Main.LocalPlayer.Center,path[0].Position)>2400*2400)return;
        foreach(var step in path)if(!Finite(step.Position)||!Finite(step.Velocity))return;
        var visual=new Projectile();visual.SetDefaults(type);visual.localAI[0]=clock;
        foreach(var step in path){visual.position=step.Position;visual.velocity=step.Velocity;visual.VanillaAI();}
    }
}
internal static partial class CompanionNetwork {
    const byte InstantTrail=44;
    internal static void Trail(int type,float clock,List<CompanionInstantShot.Step> path){
        if(!Server)return;using var p=Packet(InstantTrail);p.Write((short)type);p.Write(clock);p.Write((ushort)path.Count);
        foreach(var step in path){p.Write(step.Position.X);p.Write(step.Position.Y);p.Write(step.Velocity.X);p.Write(step.Velocity.Y);}p.Send();
    }
    static void ReadTrail(BinaryReader r){
        int type=r.ReadInt16();float clock=r.ReadSingle();int count=r.ReadUInt16();
        if(count>256)throw new InvalidDataException("Companion ray exceeds native substep limit");
        var path=new CompanionInstantShot.Step[count];for(int i=0;i<count;i++)path[i]=new(new Vector2(r.ReadSingle(),r.ReadSingle()),new Vector2(r.ReadSingle(),r.ReadSingle()));
        CompanionInstantShot.Present(type,clock,path);
    }
}
