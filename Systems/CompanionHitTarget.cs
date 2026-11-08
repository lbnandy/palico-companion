using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed partial class CompanionNativeUse {
    internal bool ReceivingHit {get;private set;}
    // Ask the attack's native/modded hit permission using our existing isolated
    // actor. Never run the human owner's Hurt, accessory or on-hit effects.
    internal bool CanReceiveHit(CompanionPlayer state,Projectile cat,NPC npc,Projectile shot,ref int cooldown){
        Cat=cat;State=state;
        if(CompanionNetwork.Client||state.Vitals.Downed||state.Vitals.Immunity>0||!EnsureActorSlot())return false;
        using var scope=new Scope(this,trackHealing:false);
        var position=Actor.position;int width=Actor.width,height=Actor.height;
        bool receiving=ReceivingHit,dead=Actor.dead,immune=Actor.immune;int immuneTime=Actor.immuneTime;
        try {
            Actor.position=cat.position;Actor.width=cat.width;Actor.height=cat.height;
            Actor.dead=false;Actor.immune=false;Actor.immuneTime=0;
            int before=Actor.statLife=state.Vitals.Health;ReceivingHit=true;
            bool allowed=npc!=null?NPCLoader.CanHitPlayer(npc,Actor,ref cooldown):ProjectileLoader.CanHitPlayer(shot,Actor);
            if(allowed)
                allowed=npc!=null?PlayerLoader.CanBeHitByNPC(Actor,npc,ref cooldown):PlayerLoader.CanBeHitByProjectile(Actor,shot);
            // Some hazards apply life loss inside CanHitPlayer and return false
            // to bypass ordinary hit damage. Preserve that loss without adding
            // defense reduction or ordinary hit immunity a second time.
            CommitHitHealth(before,CompanionIncomingDamage.BossNear(state.Player),0);
            return allowed&&!state.Vitals.Downed;
        } finally {
            ReceivingHit=receiving;Actor.dead=dead;Actor.immune=immune;Actor.immuneTime=immuneTime;
            Actor.position=position;Actor.width=width;Actor.height=height;
        }
    }
}

public sealed class CompanionHitLifecycle : ModSystem {
    public override void Load()=>On_Player.KillMe+=Kill;
    public override void Unload()=>On_Player.KillMe-=Kill;
    static void Kill(On_Player.orig_KillMe orig,Player player,PlayerDeathReason reason,double damage,int direction,bool pvp){
        // A native hazard may request death directly. The isolated hit actor is
        // not a human player: its caller commits the loss to companion Vitals.
        // Do not create player death messages, dropped items or respawn state.
        var use=CompanionNativeUse.Current;
        if(use is {ReceivingHit:true}&&ReferenceEquals(player,use.Actor)){
            player.statLife=0;
            if(CompanionEquipment.Independent(use.State)){
                bool sound=false,gore=false;
                // Let native equipment veto death and consume its own cooldown.
                // Only the human death/respawn pipeline is replaced by knockdown.
                if(!PlayerLoader.PreKill(player,damage,direction,pvp,ref sound,ref gore,ref reason))
                    player.statLife=Math.Max(1,player.statLife);
            }
            return;
        }
        orig(player,reason,damage,direction,pvp);
    }
}
