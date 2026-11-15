using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using CalamityMod.Enums;
using CalamityMod.CalPlayer;
using CalamityMod.Graphics.Primitives;
using CalamityMod.Systems.Graphic.PixelationSystem;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using Mono.Cecil.Cil;
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
    ILHook primitives, queuedDispatch, catalystRocks;
    Hook queuedDrawers;
    static readonly Dictionary<Type,FieldInfo[]> capturedProjectiles=new();
    delegate void Queue(Action<Matrix> draw,GeneralDrawLayer layer,BlendState blend);
    public override void Load(){
        if(Main.dedServ)return;
        primitives=new ILHook(typeof(PrimitivePixelationSystem).GetMethod("DrawPrimsToRenderTarget",BindingFlags.NonPublic|BindingFlags.Static)!,ScopePrimitives);
        queuedDispatch=new ILHook(typeof(PixelationManager).GetMethod("DrawCollectionsToTarget",BindingFlags.NonPublic|BindingFlags.Static)!,ScopeQueuedDraws);
        queuedDrawers=new Hook(typeof(PixelationManager).GetMethod(nameof(PixelationManager.AddPixelatedDrawer))!,Capture);
        if(ModLoader.TryGetMod("CatalystMod",out var catalyst)){
            var dispatch=catalyst.Code.GetType("CatalystMod.CatalystDetours")?.GetMethod("Main_DrawProjectiles",BindingFlags.NonPublic|BindingFlags.Static);
            var draw=catalyst.Code.GetType("CatalystMod.Projectiles.Summon.IntergelacticMinionOrbitingRocks")?.GetMethod("DrawRocks",new[]{typeof(SpriteBatch),typeof(CalamityPlayer),typeof(bool)});
            if(dispatch!=null&&draw!=null){
                // Bind the optional native renderer once; do not copy its rock
                // animation or require Catalyst when only Calamity is installed.
                var renderer=Expression.Parameter(typeof(ModProjectile));var batch=Expression.Parameter(typeof(SpriteBatch));
                var player=Expression.Parameter(typeof(CalamityPlayer));var top=Expression.Parameter(typeof(bool));
                var native=Expression.Lambda<Action<ModProjectile,SpriteBatch,CalamityPlayer,bool>>(
                    Expression.Call(Expression.Convert(renderer,draw.DeclaringType),draw,batch,player,top),renderer,batch,player,top).Compile();
                catalystRocks=new ILHook(dispatch,il=>ScopeCatalystRocks(il,draw.DeclaringType.FullName,native));
            }
        }
    }
    public override void Unload(){primitives?.Dispose();queuedDispatch?.Dispose();queuedDrawers?.Dispose();catalystRocks?.Dispose();primitives=null;queuedDispatch=null;catalystRocks=null;queuedDrawers=null;capturedProjectiles.Clear();}
    static void ScopeCatalystRocks(ILContext il,string renderer,Action<ModProjectile,SpriteBatch,CalamityPlayer,bool> native){
        var cursor=new ILCursor(il);int count=0;
        // Both rock layers read the owner BEFORE invoking DrawRocks. Scope that
        // lookup too: DrawProj's existing scope cannot cover this separate pass.
        while(cursor.TryGotoNext(i=>i.MatchLdsfld<Main>(nameof(Main.player)),i=>i.MatchLdloc(out _),
            i=>i.MatchLdfld<Projectile>(nameof(Projectile.owner)),i=>i.OpCode==OpCodes.Ldelem_Ref,
            i=>i.MatchCall("CalamityMod.CalamityUtils","Calamity"),i=>i.MatchLdcI4(out _),i=>i.MatchCallvirt(renderer,"DrawRocks"))){
            cursor.RemoveRange(5);cursor.Index++;cursor.Remove();
            cursor.EmitDelegate<Action<ModProjectile,SpriteBatch,bool>>((shot,batch,top)=>{
                if(!CompanionNativeUse.DrawContext(shot.Projectile,out var use))return;
                if(use==null){native(shot,batch,CalamityMod.CalamityUtils.Calamity(Main.player[shot.Projectile.owner]),top);return;}
                using(new CompanionNativeUse.ReplicaDrawScope(use))
                using(new CompanionNativeUse.Scope(use,trackHealing:false))native(shot,batch,CalamityMod.CalamityUtils.Calamity(use.Actor),top);
            });count++;
        }
        if(count!=2)throw new InvalidOperationException("Catalyst rock draw dispatch was not found");
    }
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
        var projectile=CapturedProjectile(draw.Target,out bool stale);
        if(stale)return;
        var use=CompanionNativeUse.Current;
        if(projectile!=null&&!CompanionNativeUse.DrawContext(projectile,out use))return;
        if(use==null){orig(draw,layer,blend);return;}
        orig(new QueuedDraw(draw,use,projectile).Invoke,layer,blend);
    }
    // Guard execution as well as registration: a native/third-party drawer may
    // reach the collection without passing our registration hook. Never infer
    // ownership from a vacant player slot; use the captured projectile tag.
    static void ScopeQueuedDraws(ILContext il){
        var cursor=new ILCursor(il);
        if(!cursor.TryGotoNext(i=>i.MatchCallvirt<Action<Matrix>>(nameof(Action<Matrix>.Invoke))))
            throw new InvalidOperationException("Calamity queued draw dispatch was not found");
        cursor.Remove();
        cursor.EmitDelegate<Action<Action<Matrix>,Matrix>>(DrawQueued);
    }
    static void DrawQueued(Action<Matrix> draw,Matrix matrix){
        if(draw.Target is QueuedDraw){draw(matrix);return;}
        var projectile=CapturedProjectile(draw.Target,out bool stale);
        if(stale)return;
        if(projectile==null){draw(matrix);return;}
        if(!projectile.active)return;
        if(!CompanionNativeUse.DrawContext(projectile,out var use))return;
        if(use==null){draw(matrix);return;}
        using(new CompanionNativeUse.ReplicaDrawScope(use))
        using(new CompanionNativeUse.Scope(use,trackHealing:false))draw(matrix);
    }
    sealed class QueuedDraw {
        readonly Action<Matrix> draw;
        readonly CompanionNativeUse use;
        readonly Projectile projectile;
        readonly ModProjectile renderer;
        internal QueuedDraw(Action<Matrix> draw,CompanionNativeUse use,Projectile projectile){
            this.draw=draw;this.use=use;this.projectile=projectile;renderer=projectile?.ModProjectile;
        }
        internal void Invoke(Matrix matrix){
            if(!use.Present||!use.EnsureActorSlot())return;
            // A queued draw can outlive the projectile or its slot can be reused.
            if(projectile!=null&&(!ReferenceEquals(projectile.ModProjectile,renderer)||
                !CompanionNativeUse.DrawContext(projectile,out var live)||live!=use))return;
            using(new CompanionNativeUse.ReplicaDrawScope(use))
            using(new CompanionNativeUse.Scope(use,trackHealing:false))draw(matrix);
        }
    }
    static Projectile CapturedProjectile(object target,out bool stale){
        stale=false;
        if(target is ModProjectile renderer){stale=!ReferenceEquals(renderer.Projectile.ModProjectile,renderer);return renderer.Projectile;}
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
            if(value is ModProjectile captured&&!ReferenceEquals(candidate.ModProjectile,captured))stale=true;
            if(result!=null&&!ReferenceEquals(result,candidate))return null;
            result=candidate;
        }
        return result;
    }
}
