using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

[ExtendsFromMod("ThoriumMod")]
[JITWhenModsEnabled("ThoriumMod")]
public sealed class RagnarokCompanionHealing:ModSystem {
    delegate void HealCall(Projectile shot,int amount,float radius,bool effects,bool bonus,object custom,Func<Player,bool> predicate,int specific,bool ignoreHealer,bool ignoreTarget,bool statistics);
    delegate void RelicHeal(Projectile shot,int amount,float radius,bool effects,bool bonus,object custom,Func<Player,bool> predicate,int specific,bool ignoreHealer,bool ignoreTarget,bool statistics,Player patient);
    HealCall original;
    Func<Projectile,Player,int,bool,bool,bool,bool,object,bool> heal;
    Func<Projectile,Player,Player,float,Func<Player,bool>,int,bool,bool> eligible;
    FieldInfo healList;
    ILHook relic;
    bool patched;
    int itemType;
    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("RagnarokMod",out var mod)||
            !mod.TryFind<ModItem>("RelicOfConvergenceOverride",out var item)||
            !mod.TryFind<ModProjectile>("RelicOfConvergenceCrystalOverride",out var crystal))return;
        var helper=mod.Code.GetType("RagnarokMod.Utils.ProjectileHelper");
        const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        var full=helper?.GetMethod("ThoriumHeal",flags);
        var target=helper?.GetMethod("ThoriumHealTarget",flags);
        var canHeal=helper?.GetMethod("CanBeHealed",flags);
        healList=crystal.GetType().GetField("healList");
        if(full==null||target==null||canHeal==null||healList?.FieldType!=typeof(List<bool>))return;
        try {
            original=ThoriumCompanionPatient.Bind<HealCall>(full);
            heal=ThoriumCompanionPatient.Bind<Func<Projectile,Player,int,bool,bool,bool,bool,object,bool>>(target);
            eligible=canHeal.CreateDelegate<Func<Projectile,Player,Player,float,Func<Player,bool>,int,bool,bool>>();
        } catch(ArgumentException){Mod.Logger.Warn("Ragnarok healing interface changed; relic targeting adapter was not installed.");return;}
        relic=new ILHook(crystal.GetType().GetMethod("AI"),PatchRelic);
        if(!patched){relic.Dispose();relic=null;Mod.Logger.Warn("Ragnarok relic patient loop changed; targeting adapter was not installed.");return;}
        itemType=item.Type;ThoriumCompanionBridge.NativeSelfHealing.Add(itemType);
    }
    void PatchRelic(ILContext il){
        // The native loop already selects each patient around the aim point
        // and computes that patient's amount. Its nested all-player heal loses
        // the selected patient and checks distance from the held crystal instead.
        // Retain the loop, charge, visuals and native addon healing effects.
        var patients=il.Body.Variables.Where(v=>v.VariableType.FullName==typeof(Player).FullName).ToArray();
        if(patients.Length!=1)return;
        var patient=patients[0];
        var c=new ILCursor(il);
        if(!c.TryGotoNext(i=>i.OpCode==OpCodes.Call&&i.Operand is MethodReference m&&
            m.DeclaringType.FullName=="RagnarokMod.Utils.ProjectileHelper"&&m.Name=="ThoriumHeal"))
            return;
        var bound=new ILCursor(il);
        if(!bound.TryGotoNext(MoveType.After,i=>i.MatchLdcI4(255)&&i.Next!=null&&
            (i.Next.OpCode==OpCodes.Blt||i.Next.OpCode==OpCodes.Blt_S)))return;
        c.Emit(OpCodes.Ldloc,patient);c.EmitDelegate<RelicHeal>(HealPatient);c.Remove();
        // Multiplayer companions use the non-network player sentinel. Include
        // it in this local patient loop, with matching native per-patient state.
        bound.Emit(OpCodes.Ldarg_0);bound.EmitDelegate<Func<int,ModProjectile,int>>(PatientCount);
        patched=true;
    }
    int PatientCount(int count,ModProjectile crystal){
        if(CompanionOwnedShot.Of(crystal.Projectile)==null)return count;
        var list=(List<bool>)healList.GetValue(crystal);
        int limit=Main.player.Length;
        while(list.Count<limit)list.Add(false);
        return limit;
    }
    void HealPatient(Projectile shot,int amount,float radius,bool effects,bool bonus,object custom,Func<Player,bool> predicate,int specific,bool ignoreHealer,bool ignoreTarget,bool statistics,Player patient){
        var use=CompanionOwnedShot.Of(shot);
        if(use==null){original(shot,amount,radius,effects,bonus,custom,predicate,specific,ignoreHealer,ignoreTarget,statistics);return;}
        if(CompanionNetwork.Client||CompanionNativeUse.Current!=use)return;
        // Range and team were checked by the original outer loop. Do not heal
        // its neighbours again, nor repeat the pulse on network replicas.
        if(eligible(shot,use.Actor,patient,0,predicate,-1,ignoreHealer))
            heal(shot,patient,amount,effects,bonus,ignoreTarget,statistics,custom);
    }
    public override void Unload(){relic?.Dispose();relic=null;ThoriumCompanionBridge.NativeSelfHealing.Remove(itemType);original=null;heal=null;eligible=null;healList=null;}
}
