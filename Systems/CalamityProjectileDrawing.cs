using System;
using System.Reflection;
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
    delegate void Queue(Action<Matrix> draw,GeneralDrawLayer layer,BlendState blend);
    public override void Load(){
        if(Main.dedServ)return;
        primitives=new ILHook(typeof(PrimitivePixelationSystem).GetMethod("DrawPrimsToRenderTarget",BindingFlags.NonPublic|BindingFlags.Static)!,ScopePrimitives);
        queuedDrawers=new Hook(typeof(PixelationManager).GetMethod(nameof(PixelationManager.AddPixelatedDrawer))!,Capture);
    }
    public override void Unload(){primitives?.Dispose();queuedDrawers?.Dispose();primitives=null;queuedDrawers=null;}
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
        var use=CompanionNativeUse.Current;
        if(use==null){orig(draw,layer,blend);return;}
        // The callback often closes over a projectile, but executes next frame,
        // after its original PreDraw scope has restored the native player slot.
        orig(matrix=>{
            if(!use.Present||!use.EnsureActorSlot())return;
            using(new CompanionNativeUse.ReplicaDrawScope(use))
            using(new CompanionNativeUse.Scope(use,trackHealing:false))draw(matrix);
        },layer,blend);
    }
}
