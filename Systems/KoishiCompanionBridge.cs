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
    readonly Dictionary<Type,int> releaseBows=new();
    Func<ModPlayer,int,float> value;
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
        var method=charge?.GetType().GetMethod("GetChargeValue");
        var key=method?.GetParameters().SingleOrDefault()?.ParameterType;
        if(method?.ReturnType!=typeof(float)||key?.IsEnum!=true)return;
        var player=Expression.Parameter(typeof(ModPlayer));
        var chargeKey=Expression.Parameter(typeof(int));
        value=Expression.Lambda<Func<ModPlayer,int,float>>(Expression.Call(Expression.Convert(player,charge.GetType()),method,Expression.Convert(chargeKey,key)),player,chargeKey).Compile();
        // Only release-to-fire bows use this input policy. Their own ChargePlayer
        // supplies the duration and attack-speed scaling; continuous bows stay held.
        foreach(string name in new[]{"WoodBow","OreBow","BloodRainBow","BeesKnees","DD2PhoenixBow","DemonBow","HellwingBow","IceBow","Marrow","MoltenFury","TendonBow","ShadowFlameBow","SkeletonBow","WoodlandStalker"})Register(name,"BowCharge");
        foreach(string name in new[]{"Phantasm","Tsunami","DaedalusStormbow","DD2BetsyBow","FairyQueenRangedItem","PulseBow"})Register(name,name);
        CompanionCrossMod.ChargeComplete.Add(Complete);
        void Register(string name,string chargeName){
            var type=mod.Code.GetType("OriginalWeaponResetBH.Content.Projectiles.Ranged.Bows."+name+"LeftHoldout");
            if(type==null||!Enum.IsDefined(key,chargeName))return;
            int id=Convert.ToInt32(Enum.Parse(key,chargeName));
            foreach(var projectile in mod.GetContent<ModProjectile>())
                if(type.IsInstanceOfType(projectile))releaseBows[projectile.GetType()]=id;
        }
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
    bool? Complete(Item item,Projectile shot,CompanionNativeUse use)=>shot.ModProjectile!=null&&releaseBows.TryGetValue(shot.ModProjectile.GetType(),out int key)?value(use.Actor.GetModPlayer(charge),key)>=1:null;
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
    public override void Unload(){foreach(var hook in reloadInputs)hook.Dispose();reloadInputs.Clear();CompanionCrossMod.BeforeReset-=Advance;CompanionCrossMod.ChargeComplete.Remove(Complete);charge=magazine=null;releaseBows.Clear();value=null;}
}
