using System;
using Microsoft.Xna.Framework;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Hit callbacks run on authority. Held projectile AI also runs on replicas,
// but network corrections can skip the frame that triggers a swing sound.
public sealed class CompanionHitAudio : ModSystem {
    [ThreadStatic] static Vector2? hitPosition;
    [ThreadStatic] static Projectile activeShot;

    delegate void ModifyItem(Player actor,Item item,NPC npc,ref NPC.HitModifiers modifiers);
    delegate void AfterItem(Player actor,Item item,NPC npc,in NPC.HitInfo hit,int damage);
    delegate void ModifyShot(Projectile shot,NPC npc,ref NPC.HitModifiers modifiers);
    delegate void AfterShot(Projectile shot,NPC npc,in NPC.HitInfo hit,int damage);
    delegate void ModifyItemHook(ModifyItem next,Player actor,Item item,NPC npc,ref NPC.HitModifiers modifiers);
    delegate void AfterItemHook(AfterItem next,Player actor,Item item,NPC npc,in NPC.HitInfo hit,int damage);
    delegate void ModifyShotHook(ModifyShot next,Projectile shot,NPC npc,ref NPC.HitModifiers modifiers);
    delegate void AfterShotHook(AfterShot next,Projectile shot,NPC npc,in NPC.HitInfo hit,int damage);
    delegate SlotId Play(in SoundStyle style,Vector2? position,SoundUpdateCallback callback);
    delegate SlotId PlayHook(Play next,in SoundStyle style,Vector2? position,SoundUpdateCallback callback);

    public override void Load(){
        MonoModHooks.Add(typeof(SoundEngine).GetMethod(nameof(SoundEngine.PlaySound),new[]{typeof(SoundStyle).MakeByRefType(),typeof(Vector2?),typeof(SoundUpdateCallback)}),(PlayHook)Relay);
        MonoModHooks.Add(typeof(ProjectileLoader).GetMethod(nameof(ProjectileLoader.ProjectileAI)),(Action<Action<Projectile>,Projectile>)ProjectileAI);
        if(!Main.dedServ)return;
        var hooks=typeof(CombinedHooks);
        MonoModHooks.Add(hooks.GetMethod(nameof(CombinedHooks.ModifyPlayerHitNPCWithItem)),(ModifyItemHook)ModifyItemHit);
        MonoModHooks.Add(hooks.GetMethod(nameof(CombinedHooks.OnPlayerHitNPCWithItem)),(AfterItemHook)AfterItemHit);
        MonoModHooks.Add(hooks.GetMethod(nameof(CombinedHooks.ModifyHitNPCWithProj)),(ModifyShotHook)ModifyShotHit);
        MonoModHooks.Add(hooks.GetMethod(nameof(CombinedHooks.OnHitNPCWithProj)),(AfterShotHook)AfterShotHit);
    }

    static void ProjectileAI(Action<Projectile> next,Projectile shot){
        if(Main.netMode==Terraria.ID.NetmodeID.SinglePlayer||CompanionNativeUse.Current==null){next(shot);return;}
        var previous=activeShot;
        activeShot=shot;
        try{next(shot);}finally{activeShot=previous;}
    }

    internal readonly struct HitScope : IDisposable {
        readonly Vector2? previous;
        readonly Projectile previousShot;
        internal HitScope(NPC target){
            previous=hitPosition;previousShot=activeShot;activeShot=null;
            hitPosition=target?.Center;
        }
        public void Dispose(){hitPosition=previous;activeShot=previousShot;}
    }

    static void ModifyItemHit(ModifyItem next,Player actor,Item item,NPC npc,ref NPC.HitModifiers modifiers){
        using var scope=new HitScope(CompanionNativeUse.Current?.Actor==actor?npc:null);
        next(actor,item,npc,ref modifiers);
    }
    static void AfterItemHit(AfterItem next,Player actor,Item item,NPC npc,in NPC.HitInfo hit,int damage){
        using var scope=new HitScope(CompanionNativeUse.Current?.Actor==actor?npc:null);
        next(actor,item,npc,in hit,damage);
    }
    static void ModifyShotHit(ModifyShot next,Projectile shot,NPC npc,ref NPC.HitModifiers modifiers){
        using var scope=new HitScope(CompanionOwnedShot.Of(shot)!=null?npc:null);
        next(shot,npc,ref modifiers);
    }
    static void AfterShotHit(AfterShot next,Projectile shot,NPC npc,in NPC.HitInfo hit,int damage){
        using var scope=new HitScope(CompanionOwnedShot.Of(shot)!=null?npc:null);
        next(shot,npc,in hit,damage);
    }
    static SlotId Relay(Play next,in SoundStyle style,Vector2? position,SoundUpdateCallback callback){
        if((hitPosition.HasValue||activeShot!=null)&&!style.IsLooped&&callback==null&&!string.IsNullOrEmpty(style.SoundPath)){
            if(hitPosition is Vector2 target)
                CompanionNetwork.HitSound(in style,position??target);
            else if(CompanionNativeUse.Current is {} use && use.Actor.heldProj==activeShot.whoAmI &&
                    CompanionProjectileData.TryGet<CompanionOwnedShot>(activeShot,out var tag)&&tag.Context==use){
                if(CompanionNetwork.Server)
                    CompanionNetwork.HitSound(in style,position??activeShot.Center,activeShot);
                else if(CompanionNetwork.Client){
                    var key=style.SoundPath;
                    var sounds=tag.HeldSounds??=new();
                    sounds.TryGetValue(key,out var seen);
                    if(seen.FromServer)return SlotId.Invalid;
                    var result=next(in style,position,callback);
                    if(result.IsValid)sounds[key]=(false,seen.LocalPlays+1);
                    return result;
                }
            }
        }
        return next(in style,position,callback);
    }

    // A replica can play the first swing before its packet arrives. Reconcile
    // those early plays once; sounds only requested by the client stay local.
    internal static bool ShouldPlayHeldSound(int owner,int identity,int type,string path){
        foreach(var shot in Main.ActiveProjectiles){
            if(shot.owner!=owner||shot.identity!=identity||shot.type!=type||
                !CompanionProjectileData.TryGet<CompanionOwnedShot>(shot,out var tag))continue;
            var sounds=tag.HeldSounds??=new();
            sounds.TryGetValue(path,out var seen);
            sounds[path]=(true,Math.Max(0,seen.LocalPlays-1));
            return seen.LocalPlays==0;
        }
        return true;
    }
}
