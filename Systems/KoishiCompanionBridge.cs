using System;
using System.Linq;
using System.Linq.Expressions;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class KoishiCompanionBridge:ModSystem {
    ModPlayer charge;
    Type bow,oreBow;
    Func<ModPlayer,float> value;
    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("OriginalWeaponResetBH",out var mod))return;
        charge=mod.GetContent<ModPlayer>().FirstOrDefault(p=>p.GetType().FullName=="OriginalWeaponResetBH.Players.ChargePlayer");
        bow=mod.Code.GetType("OriginalWeaponResetBH.Content.Projectiles.Ranged.Bows.WoodBowLeftHoldout");
        oreBow=mod.Code.GetType("OriginalWeaponResetBH.Content.Projectiles.Ranged.Bows.OreBowLeftHoldout");
        var method=charge?.GetType().GetMethod("GetChargeValue");
        var key=method?.GetParameters().SingleOrDefault()?.ParameterType;
        if(bow==null||method?.ReturnType!=typeof(float)||key?.IsEnum!=true||!Enum.IsDefined(key,"BowCharge"))return;
        var player=Expression.Parameter(typeof(ModPlayer));
        value=Expression.Lambda<Func<ModPlayer,float>>(Expression.Call(Expression.Convert(player,charge.GetType()),method,Expression.Constant(Enum.Parse(key,"BowCharge"),key)),player).Compile();
        CompanionCrossMod.BeforeReset+=Advance;
        CompanionCrossMod.ChargeComplete.Add(Complete);
    }
    void Advance(CompanionNativeUse use){
        // The mod accumulates last tick's enabled charge before ResetEffects.
        // Use its own clock/configuration rather than duplicating charge rates.
        using(new CompanionNativeUse.Scope(use))use.Actor.GetModPlayer(charge).PreUpdate();
    }
    bool? Complete(Item item,Projectile shot,CompanionNativeUse use)=>shot.ModProjectile!=null&&(bow.IsInstanceOfType(shot.ModProjectile)||oreBow?.IsInstanceOfType(shot.ModProjectile)==true)?value(use.Actor.GetModPlayer(charge))>=1:null;
    public override void Unload(){CompanionCrossMod.BeforeReset-=Advance;CompanionCrossMod.ChargeComplete.Remove(Complete);charge=null;bow=oreBow=null;value=null;}
}
