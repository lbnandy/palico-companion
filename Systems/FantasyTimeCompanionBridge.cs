using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class FantasyTimeCompanionBridge : ModSystem {
    public override void Load(){
        if(!ModLoader.TryGetMod("Vanilla",out var mod))return;
        var type=mod.Code.GetType("Vanilla.Global.SpeedupMult");
        var method=type?.GetMethod(nameof(PostUpdateProjectiles),Type.EmptyTypes);
        if(method?.DeclaringType!=type||method?.ReturnType!=typeof(void)||
            type==null||!typeof(ModSystem).IsAssignableFrom(type))return;
        MonoModHooks.Modify(method,ProjectileOwner);
    }

    // Fantasy Time scans owners outside Projectile.Update, where companion
    // actor slots have already been restored. Resolve the tagged projectile's
    // actual actor without exposing virtual players to the rest of the frame.
    // Its bee speed bonus and extra Update still run through the original code
    // and our existing scoped projectile update hook.
    static void ProjectileOwner(ILContext il){
        var cursor=new ILCursor(il);int projectile=0,player=0;
        if(!cursor.TryGotoNext(i=>i.MatchLdsfld<Main>(nameof(Main.player)),
            i=>i.MatchLdloc(out projectile),i=>i.MatchLdfld<Projectile>(nameof(Projectile.owner)),
            i=>i.OpCode==OpCodes.Ldelem_Ref,i=>i.MatchStloc(out player))){
            ModContent.GetInstance<FantasyTimeCompanionBridge>().Mod.Logger.Warn("Fantasy Time projectile owner lookup was not found; compatibility hook was not applied.");return;
        }
        int ownerRead=cursor.Index;
        if(!cursor.TryGotoNext(MoveType.After,i=>i.MatchLdloc(projectile),
            i=>i.MatchLdfld<Entity>(nameof(Entity.active)))){
            ModContent.GetInstance<FantasyTimeCompanionBridge>().Mod.Logger.Warn("Fantasy Time projectile activity check was not found; compatibility hook was not applied.");return;
        }
        cursor.Emit(OpCodes.Ldloc,player);
        cursor.EmitDelegate<Func<bool,Player,bool>>((active,owner)=>active&&owner!=null);
        cursor.Index=ownerRead;cursor.RemoveRange(4);
        cursor.Emit(OpCodes.Ldloc,projectile);
        cursor.EmitDelegate<Func<Projectile,Player>>(Owner);
    }

    static Player Owner(Projectile projectile){
        if(CompanionProjectileData.TryGet<CompanionOwnedShot>(projectile,out var shot)&&
            (shot.Context!=null||shot.CatIdentity>=0)){
            // A client's projectile can arrive before its companion. Wait for
            // normal binding/AI instead of reading the uninitialized sentinel.
            return CompanionNativeUse.DrawContext(projectile,out var use)?use?.Actor:null;
        }
        if(CompanionFishingBobber.Of(projectile) is {} fishing)return fishing.Actor;
        return Main.player[projectile.owner];
    }
}
