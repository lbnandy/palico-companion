using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
namespace PalicoCompanion.Systems;

// Native owner target first; otherwise protect the owner and retain a viable
// target unless another is meaningfully nearer. No extra command UI required.
internal sealed class CompanionTargeting {
    NPC current;int currentType;CompanionNpcEpoch currentEpoch;ulong lastSeen;
    internal const int SightGrace=45;
    internal bool Obscured {get;private set;}
    // An NPC slot can be reused for another monster of the very same type.
    // Share the per-spawn identity already used by native hit cooldowns.
    bool SameTarget=>current is {active:true}&&current.type==currentType&&ReferenceEquals(currentEpoch,current.GetGlobalNPC<CompanionNpcEpoch>());
    internal bool Holding=>Obscured&&SameTarget&&Main.GameUpdateCount-lastSeen<SightGrace;
    internal NPC VisibleTarget=>SameTarget&&!Obscured?current:null;
    internal void Engage(NPC npc)=>Remember(npc);
    internal void Clear(){current=null;currentEpoch=null;Obscured=false;}
    NPC Remember(NPC target){current=target;currentType=target?.type??0;currentEpoch=target?.GetGlobalNPC<CompanionNpcEpoch>();Obscured=false;if(target!=null)lastSeen=Main.GameUpdateCount;return target;}
    internal static bool Destructible(NPC npc)=>npc is {active:true,friendly:false,dontTakeDamage:false,immortal:false}&&npc.type>=NPCID.None&&NPCID.Sets.ProjectileNPC[npc.type];
    internal static bool Incoming(NPC npc,Vector2 point){
        if(!Destructible(npc)||npc.velocity.LengthSquared()<.1f)return false;
        float t=Math.Clamp(Vector2.Dot(point-npc.Center,npc.velocity)/npc.velocity.LengthSquared(),0,18);
        return t>0&&Vector2.DistanceSquared(npc.Center+npc.velocity*t,point)<48*48;
    }
    static bool InReach(Player owner,Vector2 cat,NPC npc,float range)=>npc is {active:true}&&(npc.CanBeChasedBy()||Destructible(npc))&&
        CompanionStation.Contains(owner.GetModPlayer<CompanionPlayer>(),npc.Center)&&Vector2.DistanceSquared(npc.Center,CompanionStation.Origin(owner))<range*range&&Vector2.DistanceSquared(npc.Center,cat)<range*range;
    internal static bool Valid(Player owner,Vector2 cat,NPC npc)=>Valid(owner,cat,npc,CompanionCombatReach.SearchRange);
    // Support skills share this validator; their visibility must not inherit
    // the primary weapon's ability to attack through terrain.
    internal static bool Valid(Player owner,Vector2 cat,NPC npc,float range)=>InReach(owner,cat,npc,range)&&CompanionCombatReach.Clear(cat,npc.Center);
    internal static bool Attackable(Player owner,Vector2 cat,NPC npc,float range)=>InReach(owner,cat,npc,range)&&
        (CompanionCombatReach.Clear(cat,npc.Center)||CompanionAttackPath.ThroughWalls(owner));
    internal static NPC Marked(Player owner,Vector2 cat){
        int id=owner.MinionAttackTargetNPC;
        return owner.GetModPlayer<CompanionPlayer>().AttacksEnabled&&id>=0&&id<Main.maxNPCs&&Attackable(owner,cat,Main.npc[id],CompanionCombatReach.TrackingRange)?Main.npc[id]:null;
    }
    internal NPC Select(Player owner,Vector2 cat,bool working){
        if(owner.GetModPlayer<CompanionPlayer>().CombatMode==CompanionCombatMode.Passive)return Remember(null);
        if(Marked(owner,cat) is {} marked)return Remember(marked);
        bool retained=SameTarget;
        // Resolve ammunition only if a blocked candidate needs it, at most
        // once per selection rather than once for every NPC.
        bool? throughWalls=null;
        bool Reachable(NPC npc,float range)=>InReach(owner,cat,npc,range)&&
            (CompanionCombatReach.Clear(cat,npc.Center)||(throughWalls??=CompanionAttackPath.ThroughWalls(owner)));
        if(owner.GetModPlayer<CompanionPlayer>().CooperativeCombat){
            if(retained&&Reachable(current,CompanionCombatReach.TrackingRange))return Remember(current);
            if(retained&&InReach(owner,cat,current,CompanionCombatReach.TrackingRange)&&Main.GameUpdateCount-lastSeen<SightGrace){Obscured=true;return current;}
            return Remember(null);
        }
        bool Intercept(NPC n)=>Incoming(n,CompanionStation.Origin(owner))||Incoming(n,cat);
        // A target crossing the acquisition boundary remains engaged until
        // the wider release boundary. Identity includes NPC slot reuse.
        bool Eligible(NPC n)=>Reachable(n,retained&&n==current?CompanionCombatReach.TrackingRange:CompanionCombatReach.SearchRange)&&
            (!working||Intercept(n)||CompanionThreat.InterruptsWork(n,owner,cat));
        bool Protect(NPC n)=>Vector2.DistanceSquared(n.Center,CompanionStation.Origin(owner))<160*160&&CompanionCombatReach.Clear(CompanionStation.Origin(owner),n.Center);
        float Distance(NPC n)=>Vector2.Distance(n.Center,Protect(n)?CompanionStation.Origin(owner):cat);
        NPC best=null;
        foreach(var n in Main.npc)if(Eligible(n)&&
            (best==null||Intercept(n)&&!Intercept(best)||Intercept(n)==Intercept(best)&&(Protect(n)&&!Protect(best)||Protect(n)==Protect(best)&&Distance(n)<Distance(best))))best=n;
        if(retained&&Eligible(current)&&best!=null&&Intercept(current)==Intercept(best)&&Protect(current)==Protect(best)&&Distance(current)<=Distance(best)+48)best=current;
        // Weapons requiring an open path retain ownership briefly on occlusion,
        // but gain no permission to fire until a usable path is available.
        if(best==null&&retained&&InReach(owner,cat,current,CompanionCombatReach.TrackingRange)&&
            !CompanionCombatReach.Clear(cat,current.Center)&&Main.GameUpdateCount-lastSeen<SightGrace){Obscured=true;return current;}
        return Remember(best);
    }
}
