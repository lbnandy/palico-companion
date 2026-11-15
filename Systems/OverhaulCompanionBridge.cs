using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class OverhaulCompanionBridge:ModSystem {
    const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    Type javelin;
    Func<ModProjectile,bool> charging,complete;
    readonly List<ILHook> draws=new();
    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("CalamityOverhaul",out var mod))return;
        if(!Main.dedServ)ScopeDraws(mod);
        javelin=mod.Code.GetType("CalamityOverhaul.Content.LegendWeapon.ErlanglaiveLegend.ErlangJavelins.ErlangJavelinHeld");
        var phase=javelin?.GetField("phase",Instance);
        var charge=javelin?.GetField("charge",Instance);
        if(phase?.FieldType.IsEnum!=true||charge?.FieldType!=typeof(float)||!Enum.IsDefined(phase.FieldType,"Charge"))return;
        var shot=Expression.Parameter(typeof(ModProjectile));
        var native=Expression.Convert(shot,javelin);
        charging=Expression.Lambda<Func<ModProjectile,bool>>(Expression.Equal(Expression.Field(native,phase),
            Expression.Constant(Enum.Parse(phase.FieldType,"Charge"),phase.FieldType)),shot).Compile();
        complete=Expression.Lambda<Func<ModProjectile,bool>>(Expression.GreaterThanOrEqual(Expression.Field(native,charge),Expression.Constant(1f)),shot).Compile();
        CompanionCrossMod.ChargeComplete.Add(Charged);
    }
    // The native controller owns charge time, attack-speed scaling and the throw.
    // Feed its completion into the existing release latch; continuous Stack mode
    // must stay held. Older workshop versions do not have this controller.
    bool? Charged(Item item,Projectile shot,CompanionNativeUse use)=>
        shot.ModProjectile?.GetType()==javelin&&charging(shot.ModProjectile)?complete(shot.ModProjectile):null;
    void ScopeDraws(Mod mod){
        const string root="CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains.";
        var renderer=mod.Code.GetType(root+"KikasaRainRender");
        var draw=mod.Code.GetType(root+"KikasaRainUmbrella")?.GetMethod("DrawCanopyGhosts",Instance);
        if(renderer==null||draw==null||draw.ReturnType!=typeof(void)||
            draw.GetParameters().Length!=1||draw.GetParameters()[0].ParameterType!=typeof(SpriteBatch))return;
        var shot=Expression.Parameter(typeof(ModProjectile));var batch=Expression.Parameter(typeof(SpriteBatch));
        var native=Expression.Lambda<Action<ModProjectile,SpriteBatch>>(
            Expression.Call(Expression.Convert(shot,draw.DeclaringType),draw,batch),shot,batch).Compile();
        // Both shader and fallback passes bypass Main.DrawProj. Scope each
        // projectile's draw just as the existing Calamity/Catalyst bridges do;
        // the native renderer still decides ghost count, tier and appearance.
        foreach(string name in new[]{"DrawUmbrellas","DrawCanopyGhosts"}){
            var dispatch=renderer.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic);
            if(dispatch==null)continue;
            bool found=false;
            var hook=new ILHook(dispatch,il=>{
                var cursor=new ILCursor(il);
                while(cursor.TryGotoNext(i=>i.MatchCall(draw.DeclaringType.FullName,draw.Name)||i.MatchCallvirt(draw.DeclaringType.FullName,draw.Name))){
                    cursor.Remove();cursor.EmitDelegate<Action<ModProjectile,SpriteBatch>>((projectile,sprites)=>Draw(projectile,sprites,native));found=true;
                }
            });
            if(found)draws.Add(hook);else hook.Dispose();
        }
    }
    static void Draw(ModProjectile shot,SpriteBatch batch,Action<ModProjectile,SpriteBatch> native){
        if(!CompanionNativeUse.DrawContext(shot.Projectile,out var use))return;
        if(use==null){native(shot,batch);return;}
        using(new CompanionNativeUse.ReplicaDrawScope(use))
        using(new CompanionNativeUse.Scope(use,trackHealing:false))native(shot,batch);
    }
    public override void Unload(){
        foreach(var hook in draws)hook.Dispose();draws.Clear();
        CompanionCrossMod.ChargeComplete.Remove(Charged);
        javelin=null;charging=complete=null;
    }
}
