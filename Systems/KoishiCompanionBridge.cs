using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class KoishiCompanionBridge:ModSystem {
    ModPlayer charge,magazine;
    Type bow,oreBow;
    Func<ModPlayer,float> value;
    readonly List<ILHook> reloadInputs=new();
    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("OriginalWeaponResetBH",out var mod))return;
        foreach(var hold in mod.GetContent<GlobalItem>().Select(item=>item.GetType().GetMethod(nameof(GlobalItem.HoldItem))).Distinct()){
            if(hold==null||hold.DeclaringType.Assembly!=mod.Code)continue;
            bool changed=false;
            var hook=new ILHook(hold,il=>changed=IsolateReloadInput(il));
            if(changed)reloadInputs.Add(hook);else hook.Dispose();
        }
        charge=mod.GetContent<ModPlayer>().FirstOrDefault(p=>p.GetType().FullName=="OriginalWeaponResetBH.Players.ChargePlayer");
        magazine=mod.GetContent<ModPlayer>().FirstOrDefault(p=>p.GetType().FullName=="OriginalWeaponResetBH.Players.GunMagazinePlayer");
        if(charge!=null||magazine!=null)CompanionCrossMod.BeforeReset+=Advance;
        bow=mod.Code.GetType("OriginalWeaponResetBH.Content.Projectiles.Ranged.Bows.WoodBowLeftHoldout");
        oreBow=mod.Code.GetType("OriginalWeaponResetBH.Content.Projectiles.Ranged.Bows.OreBowLeftHoldout");
        var method=charge?.GetType().GetMethod("GetChargeValue");
        var key=method?.GetParameters().SingleOrDefault()?.ParameterType;
        if(bow==null||method?.ReturnType!=typeof(float)||key?.IsEnum!=true||!Enum.IsDefined(key,"BowCharge"))return;
        var player=Expression.Parameter(typeof(ModPlayer));
        value=Expression.Lambda<Func<ModPlayer,float>>(Expression.Call(Expression.Convert(player,charge.GetType()),method,Expression.Constant(Enum.Parse(key,"BowCharge"),key)),player).Compile();
        CompanionCrossMod.ChargeComplete.Add(Complete);
    }
    void Advance(CompanionNativeUse use){
        // The mod accumulates last tick's enabled charge before ResetEffects.
        // Use its own clock/configuration rather than duplicating charge rates.
        // Gun holdouts start native reloads themselves; their separate player
        // clock must also advance while the actor is waiting for the magazine.
        using(new CompanionNativeUse.Scope(use)){
            if(charge!=null)use.Actor.GetModPlayer(charge).PreUpdate();
            if(magazine!=null)use.Actor.GetModPlayer(magazine).PreUpdate();
        }
    }
    bool? Complete(Item item,Projectile shot,CompanionNativeUse use)=>shot.ModProjectile!=null&&(bow.IsInstanceOfType(shot.ModProjectile)||oreBow?.IsInstanceOfType(shot.ModProjectile)==true)?value(use.Actor.GetModPlayer(charge))>=1:null;
    static bool IsolateReloadInput(ILContext il){
        var cursor=new ILCursor(il);bool changed=false;
        // Native HoldItem also queries a client-only manual reload key. An AI
        // has no such input, and the getter itself is unavailable on servers.
        // Skip just that query; empty-magazine reloads and sounds stay native.
        while(cursor.TryGotoNext(i=>i.Operand is MethodReference key&&key.Name=="get_GunMagazineKeybind"&&key.DeclaringType.FullName=="OriginalWeaponResetBH.Common.System.KeybindSystem",
            i=>i.MatchCallvirt<ModKeybind>("get_JustPressed"))){
            var native=cursor.DefineLabel();var after=cursor.DefineLabel();
            cursor.EmitDelegate<Func<bool>>(()=>CompanionNativeUse.Current!=null);
            cursor.Emit(OpCodes.Brfalse,native);cursor.Emit(OpCodes.Ldc_I4_0);cursor.Emit(OpCodes.Br,after);
            cursor.MarkLabel(native);cursor.Index+=2;cursor.MarkLabel(after);changed=true;
        }
        return changed;
    }
    public override void Unload(){foreach(var hook in reloadInputs)hook.Dispose();reloadInputs.Clear();CompanionCrossMod.BeforeReset-=Advance;CompanionCrossMod.ChargeComplete.Remove(Complete);charge=magazine=null;bow=oreBow=null;value=null;}
}
