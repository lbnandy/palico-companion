using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PalicoCompanion.Systems;

// An encounter owns one immutable bonus, including zero. Parts and later phases
// inherit it; config changes, a downed companion or a joining player cannot heal
// or shrink an ongoing fight. Native player counts and difficulty remain intact.
public sealed class CompanionBossHealth : GlobalNPC {
    internal const float ParticipationRange=4000;
    sealed record Encounter(float Bonus);
    Encounter encounter;
    NPC parent;
    CompanionBossHealth parentIdentity;
    bool initialized,applied;
    int unscaledMaximum;
    internal float Bonus=>encounter?.Bonus??0;
    public override bool InstancePerEntity=>true;

    public override void Load()=>On_NPC.Transform+=Transform;
    public override void Unload()=>On_NPC.Transform-=Transform;

    public override void OnSpawn(NPC npc,IEntitySource source) {
        if(source is EntitySource_Parent {Entity:NPC owner}) {
            parent=owner;parentIdentity=owner.GetGlobalNPC<CompanionBossHealth>();
            encounter=parentIdentity.encounter;
        }
        if(IsBossPart(npc))encounter??=FindEncounter(npc)??Capture(npc.Center);
    }

    public override void PostAI(NPC npc) {
        // Let native AI create/link its segments before scaling. Some heads
        // clamp their life to a newly created body's still-unscaled life.
        // Passive bosses may only become hostile much later in the encounter.
        if(Main.netMode!=NetmodeID.MultiplayerClient&&!initialized)Initialize(npc);
    }

    void Initialize(NPC npc) {
        if(npc.friendly||npc.lifeMax<=1)return;
        NPC root=SharedRoot(npc);
        if(!IsBossPart(npc)&&root==null)return;
        initialized=true;
        if(root!=null)encounter??=root.GetGlobalNPC<CompanionBossHealth>().encounter;
        encounter??=FindEncounter(npc)??Capture(npc.Center);
        if(Bonus<=0)return;
        // Some modded worms copy their already-scaled head's maximum into each
        // segment. That shared pool must not receive the multiplier twice.
        if(root!=null&&root.GetGlobalNPC<CompanionBossHealth>().applied&&npc.lifeMax==root.lifeMax) {
            applied=true;unscaledMaximum=root.GetGlobalNPC<CompanionBossHealth>().unscaledMaximum;
        }
        else Apply(npc);
        npc.netUpdate=true;
    }

    void Apply(NPC npc) {
        if(applied||Bonus<=0||npc.lifeMax<=1)return;
        unscaledMaximum=npc.lifeMax;
        int maximum=Scale(npc.lifeMax,Bonus);
        npc.life=(int)Math.Clamp(Math.Round(npc.life*(double)maximum/npc.lifeMax),0,maximum);
        npc.lifeMax=maximum;applied=true;
    }

    internal static int Scale(int health,float bonus)=>(int)Math.Clamp(Math.Round(health*(1d+bonus)),1,int.MaxValue);
    internal static float CalculateBonus(int players,int cats,int percent)=>players<=0?0:Math.Clamp(percent,0,100)*.01f*Math.Clamp(cats,0,players)/players;

    static Encounter Capture(Vector2 center) {
        var config=CompanionBalanceConfig.Current;
        if(!config.BossHealthBoost)return new(0);
        int players=0,cats=0;
        // Native ItemCheck actors are never additional human participants.
        for(int i=0;i<Main.maxPlayers;i++) {
            var player=Main.player[i];
            if(!player.active||player.dead||player.ghost||
                ReferenceEquals(player,CompanionNativeUse.Current?.Actor)||
                ReferenceEquals(player,CompanionFishing.Current?.Actor)||
                Vector2.DistanceSquared(player.Center,center)>ParticipationRange*ParticipationRange)continue;
            players++;
            var state=player.GetModPlayer<CompanionPlayer>();
            if(state.Enabled&&!state.Stowed&&!state.Paused&&state.Combat&&!state.Vitals.Downed&&
                (Main.netMode!=NetmodeID.Server||state.NetReady&&!state.NetLeaving)&&
                state.Cat is {active:true} cat&&
                Vector2.DistanceSquared(cat.Center,center)<=ParticipationRange*ParticipationRange)cats++;
        }
        return new(CalculateBonus(players,cats,config.BossHealthPercent));
    }

    Encounter FindEncounter(NPC npc) {
        if(parent is {active:true}&&ReferenceEquals(parentIdentity,parent.GetGlobalNPC<CompanionBossHealth>())&&parentIdentity.encounter!=null)
            return parentIdentity.encounter;
        int family=Family(npc.type);
        if(family==0)return null;
        NPC nearest=null;
        foreach(var other in Main.ActiveNPCs) {
            if(ReferenceEquals(other,npc)||Family(other.type)!=family||other.GetGlobalNPC<CompanionBossHealth>().encounter==null)continue;
            float distance=Vector2.DistanceSquared(other.Center,npc.Center);
            if(distance<=ParticipationRange*ParticipationRange&&(nearest==null||distance<Vector2.DistanceSquared(nearest.Center,npc.Center)))nearest=other;
        }
        return nearest?.GetGlobalNPC<CompanionBossHealth>().encounter;
    }

    static NPC SharedRoot(NPC npc) {
        int id=npc.realLife;
        if(id<0||id>=Main.maxNPCs||id==npc.whoAmI)return null;
        var root=Main.npc[id];
        return root.active&&IsBossPart(root)?root:null;
    }

    static bool IsBossPart(NPC npc)=>!npc.friendly&&npc.lifeMax>1&&
        npc.type is not (NPCID.LunarTowerSolar or NPCID.LunarTowerVortex or NPCID.LunarTowerNebula or NPCID.LunarTowerStardust)&&
        (npc.boss||NPCID.Sets.ShouldBeCountedAsBoss[npc.type]||Family(npc.type)!=0);

    // These vulnerable parts contribute to a boss fight even without npc.boss.
    // Ordinary spawned enemies (servants, probes, bees, slimes) stay unchanged.
    static int Family(int type)=>type switch {
        NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail=>NPCID.EaterofWorldsHead,
        NPCID.BrainofCthulhu or NPCID.Creeper=>NPCID.BrainofCthulhu,
        NPCID.SkeletronHead or NPCID.SkeletronHand=>NPCID.SkeletronHead,
        NPCID.WallofFlesh or NPCID.WallofFleshEye=>NPCID.WallofFlesh,
        NPCID.Retinazer or NPCID.Spazmatism=>NPCID.Retinazer,
        NPCID.TheDestroyer or NPCID.TheDestroyerBody or NPCID.TheDestroyerTail=>NPCID.TheDestroyer,
        NPCID.SkeletronPrime or NPCID.PrimeCannon or NPCID.PrimeLaser or NPCID.PrimeSaw or NPCID.PrimeVice=>NPCID.SkeletronPrime,
        NPCID.Golem or NPCID.GolemHead or NPCID.GolemHeadFree or NPCID.GolemFistLeft or NPCID.GolemFistRight=>NPCID.Golem,
        NPCID.MoonLordCore or NPCID.MoonLordHead or NPCID.MoonLordHand=>NPCID.MoonLordCore,
        _=>0
    };

    static void Transform(On_NPC.orig_Transform orig,NPC npc,int type) {
        var previous=npc.GetGlobalNPC<CompanionBossHealth>();
        var saved=previous.encounter;
        int health=npc.life;
        orig(npc,type);
        if(Main.netMode==NetmodeID.MultiplayerClient||saved==null||!IsBossPart(npc))return;
        var next=npc.GetGlobalNPC<CompanionBossHealth>();
        next.encounter=saved;next.initialized=true;next.applied=false;
        // Native Transform preserves health for equal base maxima. Our extra
        // maximum must not turn an Eater segment transformation into a heal.
        bool sameMaximum=previous.applied&&npc.lifeMax==previous.unscaledMaximum;
        next.Apply(npc);
        if(sameMaximum)npc.life=Math.Min(health,npc.lifeMax);
        npc.netUpdate=true;
        if(Main.netMode==NetmodeID.Server)NetMessage.SendData(MessageID.SyncNPC,-1,-1,null,npc.whoAmI);
    }

    public override void SendExtraAI(NPC npc,BitWriter bits,BinaryWriter writer) {
        bits.WriteBit(applied);
        if(applied){bits.WriteBit(npc.life==npc.lifeMax);writer.Write(npc.lifeMax);}
    }
    public override void ReceiveExtraAI(NPC npc,BitReader bits,BinaryReader reader) {
        applied=bits.ReadBit();
        if(applied) {
            // SyncNPC omits current life when full and fills it from the old
            // client maximum before ExtraAI arrives (including on late join).
            bool full=bits.ReadBit();npc.lifeMax=Math.Max(1,reader.ReadInt32());
            if(full)npc.life=npc.lifeMax;
        }
    }
}
