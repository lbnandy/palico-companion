using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PalicoCompanion.Systems;

// Native hit callbacks identify real combat; swinging at empty air is not an order.
// As with vanilla damage, human hits originate on that human's client. The server
// resolves the target and retains ownership of companion movement and attacks.
public sealed class CompanionCombatBehavior:ModPlayer {
    NPC reported;CompanionNpcEpoch reportedEpoch;ulong reportedAt;
    public override void OnHitNPCWithItem(Item item,NPC target,NPC.HitInfo hit,int damageDone){if(damageDone>0)Report(target);}
    public override void OnHitNPCWithProj(Projectile proj,NPC target,NPC.HitInfo hit,int damageDone){if(damageDone>0&&CompanionOwnedShot.Of(proj)==null)Report(target);}
    public override void OnHitByNPC(NPC npc,Player.HurtInfo hurtInfo){if(hurtInfo.Damage>0)Report(npc);}
    public override void OnHitByProjectile(Projectile proj,Player.HurtInfo hurtInfo){if(hurtInfo.Damage>0)Report(CompanionAttackSource.Of(proj));}
    void Report(NPC npc){
        if(CompanionNetwork.Server||Player.whoAmI!=Main.myPlayer||CompanionNativeUse.Current!=null||CompanionFishing.Current!=null)return;
        var state=Player.GetModPlayer<CompanionPlayer>();
        if(!Ready(state)||npc is not {active:true}||!npc.CanBeChasedBy())return;
        var epoch=npc.GetGlobalNPC<CompanionNpcEpoch>();
        if(npc==reported&&ReferenceEquals(epoch,reportedEpoch)&&Main.GameUpdateCount-reportedAt<15)return;
        reported=npc;reportedEpoch=epoch;reportedAt=Main.GameUpdateCount;
        if(CompanionNetwork.Client)CompanionNetwork.CombatContact(npc);else Engage(state,npc);
    }
    static bool Ready(CompanionPlayer s)=>s.CooperativeCombat&&s.Combat&&s.Enabled&&!s.Stowed&&!s.Paused&&!s.Vitals.Downed&&s.Player.active&&!s.Player.dead&&!s.NetLeaving;
    internal static void Engage(CompanionPlayer s,NPC npc){
        if(CompanionNetwork.Client||!Ready(s)||s.Cat?.ModProjectile is not CompanionCat cat||
            !CompanionTargeting.Attackable(s.Player,cat.Projectile.Center,npc,CompanionCombatReach.TrackingRange))return;
        cat.Engage(npc);
    }
    internal static bool Allows(CompanionPlayer s,NPC npc)=>s.CombatMode!=CompanionCombatMode.Passive&&(!s.CooperativeCombat||
        s.Cat?.ModProjectile is CompanionCat cat&&cat.PermitsCombatTarget(npc));
}

// Carry the actual NPC source through child projectiles. Do not blame an
// unrelated nearby enemy for traps, environmental damage or an unknown source.
public sealed class CompanionAttackSource:GlobalProjectile {
    public override bool InstancePerEntity=>true;
    internal static NPC Of(Projectile projectile)=>CompanionProjectileData.TryGet<CompanionAttackSource>(projectile,out var tag)?tag.Attacker:null;
    NPC source;CompanionNpcEpoch epoch;
    internal NPC Attacker=>source is {active:true}&&ReferenceEquals(epoch,source.GetGlobalNPC<CompanionNpcEpoch>())?source:null;
    void Bind(NPC npc){source=npc;epoch=npc?.GetGlobalNPC<CompanionNpcEpoch>();}
    public override void OnSpawn(Projectile projectile,IEntitySource origin){
        if(origin is EntitySource_Parent parent){
            if(parent.Entity is NPC npc)Bind(npc);
            else if(parent.Entity is Projectile ancestor)Bind(Of(ancestor));
        }
    }
    public override void SendExtraAI(Projectile p,BitWriter bits,BinaryWriter w){
        var npc=Attacker;bits.WriteBit(npc!=null);
        if(npc!=null){w.Write((short)npc.whoAmI);w.Write(npc.type);}
    }
    public override void ReceiveExtraAI(Projectile p,BitReader bits,BinaryReader r){
        bool present=bits.ReadBit();int id=present?r.ReadInt16():-1,type=present?r.ReadInt32():-1;
        if(!CompanionNetwork.Client)return;
        Bind(id>=0&&id<Main.maxNPCs&&Main.npc[id].active&&Main.npc[id].type==type?Main.npc[id]:null);
    }
}

internal static partial class CompanionNetwork {
    const byte CombatContactRequest=48;
    internal static void CombatContact(NPC npc){
        var s=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();
        if(!Client||!s.NetReady||CompanionSession.Closing)return;
        var p=Packet(CombatContactRequest);p.Write((short)npc.whoAmI);p.Write(npc.type);p.Send();
    }
    static void ReadCombatContact(CompanionPlayer s,BinaryReader r){
        int id=r.ReadInt16(),type=r.ReadInt32();
        if(id>=0&&id<Main.maxNPCs&&Main.npc[id].type==type)CompanionCombatBehavior.Engage(s,Main.npc[id]);
    }
}

// Scope the native chaseable flag alongside the existing isolated weapon actor.
// This also covers vanilla/mod AI that inlines CanBeChasedBy. Restore before
// returning to the world, so human and other companions' searches are untouched.
internal sealed class CompanionCombatTargets:IDisposable {
    readonly System.Collections.Generic.List<(NPC npc,CompanionNpcEpoch epoch)> hidden=new();
    internal CompanionCombatTargets(CompanionPlayer state){
        if(state.CombatMode==CompanionCombatMode.Proactive)return;
        foreach(var npc in Main.ActiveNPCs)if(npc.chaseable&&!CompanionCombatBehavior.Allows(state,npc)){
            hidden.Add((npc,npc.GetGlobalNPC<CompanionNpcEpoch>()));npc.chaseable=false;
        }
    }
    public void Dispose(){foreach(var (npc,epoch) in hidden)if(ReferenceEquals(epoch,npc.GetGlobalNPC<CompanionNpcEpoch>()))npc.chaseable=true;}
}
