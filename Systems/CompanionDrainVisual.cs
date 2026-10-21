using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// NPC's native Soul Drain emission only enumerates human player slots. Supply
// the same particles for companion casters; keep Dust.UpdateDust's native
// animation, lighting and homing, replacing only the tagged particle's target.
public sealed class CompanionDrainVisual : ModSystem {
    public override void Load(){
        if(Main.dedServ)return;
        On_NPC.UpdateNPC_SoulDrainDebuff+=Emit;
        IL_Dust.UpdateDust+=Target;
    }
    public override void Unload(){On_NPC.UpdateNPC_SoulDrainDebuff-=Emit;IL_Dust.UpdateDust-=Target;}
    static void Emit(On_NPC.orig_UpdateNPC_SoulDrainDebuff orig,NPC npc){
        orig(npc);
        if(!npc.soulDrain)return;
        foreach(var cat in Main.ActiveProjectiles){
            if(cat.ModProjectile is not CompanionCat companion)continue;
            var use=companion.Weapons;var actor=use.Actor;
            if(!use.Available||actor.HeldItem.type!=ItemID.SoulDrain||!use.Busy||
                Vector2.DistanceSquared(npc.Center,actor.position)>=1100*1100||Main.rand.Next(3)==0)continue;
            // Same distribution as NPC.UpdateNPC_SoulDrainDebuff. An entity
            // reference avoids reserved/network slot IDs (and slot reuse).
            var at=npc.Center+new Vector2(Main.rand.Next(-100,100),Main.rand.Next(-100,100))*.05f+npc.velocity;
            var dust=Dust.NewDustDirect(at,1,1,DustID.LifeDrain);
            dust.velocity=Vector2.Zero;dust.scale=Main.rand.Next(70,85)*.01f;
            dust.fadeIn=1;dust.customData=use;
        }
    }
    static void Target(ILContext il){
        var c=new ILCursor(il);
        // Locate the unique Soul Drain fadeIn-to-player-index conversion.
        if(!c.TryGotoNext(MoveType.Before,i=>i.MatchLdloc(out _),i=>i.MatchLdfld<Dust>(nameof(Dust.fadeIn)),
            i=>i.OpCode==OpCodes.Conv_I4,i=>i.MatchLdcI4(1),i=>i.OpCode==OpCodes.Sub))
            throw new InvalidOperationException("Native Soul Drain dust target changed");
        c.Next.MatchLdloc(out int dust);
        if(!c.TryGotoNext(MoveType.After,i=>i.MatchCallvirt<Entity>("get_Center")||i.MatchCall<Entity>("get_Center")))
            throw new InvalidOperationException("Native Soul Drain dust center changed");
        c.Emit(OpCodes.Ldloc,dust);
        c.EmitDelegate<Func<Vector2,Dust,Vector2>>((center,d)=>{
            if(d.type!=DustID.LifeDrain||d.customData is not CompanionNativeUse use)return center;
            if(!use.Present){d.active=false;return d.position+Vector2.UnitY;}
            return CompanionSprite.Hand(use.Cat);
        });
    }
}
