using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed partial class CompanionNativeUse {
    // Ask the attack's native/modded hit permission using our existing isolated
    // actor. Never run the human owner's Hurt, accessory or on-hit effects.
    internal bool CanReceiveHit(CompanionPlayer state,Projectile cat,NPC npc,Projectile shot,ref int cooldown){
        Cat=cat;State=state;if(!EnsureActorSlot())return false;
        using var scope=new Scope(this,trackHealing:false);
        var position=Actor.position;int width=Actor.width,height=Actor.height;
        try {
            Actor.position=cat.position;Actor.width=cat.width;Actor.height=cat.height;
            return npc!=null?NPCLoader.CanHitPlayer(npc,Actor,ref cooldown):ProjectileLoader.CanHitPlayer(shot,Actor);
        } finally {Actor.position=position;Actor.width=width;Actor.height=height;}
    }
}
