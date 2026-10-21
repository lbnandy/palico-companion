using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Read the native launch budget while the flail is spinning, before release.
// This includes FlailStats overrides and attack speed, without duplicating
// vanilla's per-projectile table or changing flight, collision or damage.
public sealed class CompanionFlailReach : ModSystem {
    public override void Load()=>IL_Projectile.AI_015_Flails+=Observe;
    public override void Unload()=>IL_Projectile.AI_015_Flails-=Observe;
    static void Observe(ILContext il){
        var c=new ILCursor(il);
        if(!c.TryGotoNext(MoveType.Before,i=>i.MatchCall(typeof(ProjectileLoader),nameof(ProjectileLoader.FlailStats))))
            throw new InvalidOperationException("Native flail launch parameters changed");
        // The first three of the nine ref arguments are time, speed and cap.
        var args=il.Body.Instructions;
        if(!args[c.Index-9].MatchLdloca(out int ticks)||!args[c.Index-8].MatchLdloca(out int speed)||!args[c.Index-7].MatchLdloca(out int cap))
            throw new InvalidOperationException("Native flail launch locals changed");
        // Read after vanilla has applied inverseMeleeSpeed to launchSpeed.
        if(!c.TryGotoNext(MoveType.After,i=>i.MatchLdloc(speed),i=>i.MatchLdloc(ticks),
            i=>i.OpCode==OpCodes.Conv_R4,i=>i.OpCode==OpCodes.Mul,i=>i.MatchStloc(out _)))
            throw new InvalidOperationException("Native flail travel calculation changed");
        c.Emit(OpCodes.Ldarg_0);c.Emit(OpCodes.Ldloc,ticks);c.Emit(OpCodes.Ldloc,speed);c.Emit(OpCodes.Ldloc,cap);
        c.EmitDelegate<Action<Projectile,int,float,float>>((shot,time,velocity,maximum)=>{
            var use=CompanionOwnedShot.Of(shot);
            if(use==null)return;
            float travel=Math.Min(velocity*time,maximum);
            if(float.IsFinite(travel))use.FlailRange=Math.Clamp(travel*.85f,32,CompanionCombatReach.SearchRange);
        });
    }
}
