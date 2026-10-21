using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
namespace PalicoCompanion.Systems;

// Native owner target first; otherwise protect the owner and retain a viable
// target unless another is meaningfully nearer. No extra command UI required.
internal sealed class CompanionTargeting {
    NPC current;int currentType;ulong lastSeen;
    internal const int SightGrace=45;
    internal bool Obscured {get;private set;}
    internal bool Holding=>Obscured&&current is {active:true}&&current.type==currentType&&Main.GameUpdateCount-lastSeen<SightGrace;
    internal void Clear(){current=null;Obscured=false;}
    NPC Remember(NPC target){current=target;currentType=target?.type??0;Obscured=false;if(target!=null)lastSeen=Main.GameUpdateCount;return target;}
    internal static bool Destructible(NPC npc)=>npc is {active:true,friendly:false,dontTakeDamage:false,immortal:false}&&npc.type>=NPCID.None&&NPCID.Sets.ProjectileNPC[npc.type];
    internal static bool Incoming(NPC npc,Vector2 point){
        if(!Destructible(npc)||npc.velocity.LengthSquared()<.1f)return false;
        float t=Math.Clamp(Vector2.Dot(point-npc.Center,npc.velocity)/npc.velocity.LengthSquared(),0,18);
        return t>0&&Vector2.DistanceSquared(npc.Center+npc.velocity*t,point)<48*48;
    }
    static bool InReach(Player owner,Vector2 cat,NPC npc)=>npc is {active:true}&&(npc.CanBeChasedBy()||Destructible(npc))&&
        Vector2.DistanceSquared(npc.Center,owner.Center)<CompanionCombatReach.SearchRange*CompanionCombatReach.SearchRange&&
        Vector2.DistanceSquared(npc.Center,cat)<CompanionCombatReach.SearchRange*CompanionCombatReach.SearchRange;
    internal static bool Valid(Player owner,Vector2 cat,NPC npc)=>InReach(owner,cat,npc)&&CompanionCombatReach.Clear(cat,npc.Center);
    internal static NPC Marked(Player owner,Vector2 cat){
        int id=owner.MinionAttackTargetNPC;
        return owner.GetModPlayer<CompanionPlayer>().Combat&&id>=0&&id<Main.maxNPCs&&Valid(owner,cat,Main.npc[id])?Main.npc[id]:null;
    }
    internal NPC Select(Player owner,Vector2 cat,bool working){
        if(Marked(owner,cat) is {} marked)return Remember(marked);
        bool Intercept(NPC n)=>Incoming(n,owner.Center)||Incoming(n,cat);
        bool Eligible(NPC n)=>Valid(owner,cat,n)&&(!working||Intercept(n)||CompanionThreat.InterruptsWork(n,owner,cat));
        bool Protect(NPC n)=>Vector2.DistanceSquared(n.Center,owner.Center)<160*160&&CompanionCombatReach.Clear(owner.Center,n.Center);
        float Distance(NPC n)=>Vector2.Distance(n.Center,Protect(n)?owner.Center:cat);
        NPC best=null;
        foreach(var n in Main.npc)if(Eligible(n)&&
            (best==null||Intercept(n)&&!Intercept(best)||Intercept(n)==Intercept(best)&&(Protect(n)&&!Protect(best)||Protect(n)==Protect(best)&&Distance(n)<Distance(best))))best=n;
        if(current!=null&&current.type==currentType&&Eligible(current)&&best!=null&&Intercept(current)==Intercept(best)&&Protect(current)==Protect(best)&&Distance(current)<=Distance(best)+48)best=current;
        // A brief occlusion retains combat ownership, not permission to shoot
        // through a wall or track an unseen enemy's current position.
        if(best==null&&current!=null&&current.type==currentType&&InReach(owner,cat,current)&&
            !CompanionCombatReach.Clear(cat,current.Center)&&Main.GameUpdateCount-lastSeen<SightGrace){Obscured=true;return current;}
        return Remember(best);
    }
}
