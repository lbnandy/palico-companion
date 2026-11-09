using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using CalamityMod.Enums;
using CalamityMod.Graphics.Primitives;
using CalamityMod.Systems.Graphic.PixelationSystem;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Calamity's off-screen primitives and queued pixel drawers bypass DrawProj.
// Re-enter the existing owner scope when each draw actually runs, rather than
// exposing virtual players throughout a frame or disabling accessory visuals.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityProjectileDrawing:ModSystem {
    ILHook primitives;
    Hook queuedDrawers;
    static readonly Dictionary<Type,FieldInfo[]> capturedProjectiles=new();
    delegate void Queue(Action<Matrix> draw,GeneralDrawLayer layer,BlendState blend);
    public override void Load(){
        if(Main.dedServ)return;
        primitives=new ILHook(typeof(PrimitivePixelationSystem).GetMethod("DrawPrimsToRenderTarget",BindingFlags.NonPublic|BindingFlags.Static)!,ScopePrimitives);
        queuedDrawers=new Hook(typeof(PixelationManager).GetMethod(nameof(PixelationManager.AddPixelatedDrawer))!,Capture);
    }
    public override void Unload(){primitives?.Dispose();queuedDrawers?.Dispose();primitives=null;queuedDrawers=null;capturedProjectiles.Clear();}
    static void ScopePrimitives(ILContext il){
        var cursor=new ILCursor(il);
        if(!cursor.TryGotoNext(i=>i.MatchCallvirt<IPixelatedPrimitiveRenderer>(nameof(IPixelatedPrimitiveRenderer.RenderPixelatedPrimitives))))
            throw new InvalidOperationException("Calamity primitive draw dispatch was not found");
        cursor.Remove();
        cursor.EmitDelegate<Action<IPixelatedPrimitiveRenderer,SpriteBatch,GeneralDrawLayer>>(DrawPrimitive);
    }
    static void DrawPrimitive(IPixelatedPrimitiveRenderer renderer,SpriteBatch batch,GeneralDrawLayer layer){
        if(renderer is not ModProjectile shot){renderer.RenderPixelatedPrimitives(batch,layer);return;}
        if(!CompanionNativeUse.DrawContext(shot.Projectile,out var use))return;
        if(use==null){renderer.RenderPixelatedPrimitives(batch,layer);return;}
        using(new CompanionNativeUse.ReplicaDrawScope(use))
        using(new CompanionNativeUse.Scope(use,trackHealing:false))renderer.RenderPixelatedPrimitives(batch,layer);
    }
    static void Capture(Queue orig,Action<Matrix> draw,GeneralDrawLayer layer,BlendState blend){
        // Other render passes may call PreDraw without going through DrawProj.
        // In that case the callback's projectile, not the ambient execution
        // context, identifies its owner. Only inspect typed captures in compiler
        // closures; never search arbitrary object graphs or guess from owner 255.
        var projectile=CapturedProjectile(draw.Target);
        var use=CompanionNativeUse.Current;
        if(projectile!=null&&!CompanionNativeUse.DrawContext(projectile,out use))return;
        if(use==null){orig(draw,layer,blend);return;}
        var renderer=projectile?.ModProjectile;
        // The callback often closes over a projectile, but executes next frame,
        // after its original PreDraw scope has restored the native player slot.
        orig(matrix=>{
            if(!use.Present||!use.EnsureActorSlot())return;
            // A queued draw can outlive the projectile or its slot can be reused.
            if(projectile!=null&&(!ReferenceEquals(projectile.ModProjectile,renderer)||
                !CompanionNativeUse.DrawContext(projectile,out var live)||live!=use))return;
            using(new CompanionNativeUse.ReplicaDrawScope(use))
            using(new CompanionNativeUse.Scope(use,trackHealing:false))draw(matrix);
        },layer,blend);
    }
    static Projectile CapturedProjectile(object target){
        if(target is ModProjectile renderer)return renderer.Projectile;
        if(target is Projectile projectile)return projectile;
        if(target==null)return null;
        var type=target.GetType();
        if(!capturedProjectiles.TryGetValue(type,out var fields)){
            var captures=new List<FieldInfo>();
            if(type.IsDefined(typeof(CompilerGeneratedAttribute),false))
                foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                    if(typeof(ModProjectile).IsAssignableFrom(field.FieldType)||typeof(Projectile).IsAssignableFrom(field.FieldType))captures.Add(field);
            capturedProjectiles[type]=fields=captures.ToArray();
        }
        Projectile result=null;
        foreach(var field in fields){
            var value=field.GetValue(target);
            var candidate=value is ModProjectile shot?shot.Projectile:value as Projectile;
            if(candidate==null)continue;
            if(result!=null&&!ReferenceEquals(result,candidate))return null;
            result=candidate;
        }
        return result;
    }
}
