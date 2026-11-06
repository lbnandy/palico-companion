using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class LostbeltCompanionBridge:ModSystem {
    ModPlayer input;
    FieldInfo mouse,left,right;
    Hook playerAuthority,projectileAuthority,projectileOwner;
    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("HJScarletRework",out var mod))return;
        input=mod.GetContent<ModPlayer>().FirstOrDefault(p=>p.GetType().Name=="HJScarletPlayer");
        var type=input?.GetType();mouse=type?.GetField("SyncedMouseWorld");left=type?.GetField("MouseLeft");right=type?.GetField("MouseRight");
        if(mouse?.FieldType==typeof(Vector2))CompanionCrossMod.PrepareActor+=Prepare;
        var authority=mod.Code.GetType("HJScarletRework.Core.NetCode.HJNetAuthority");
        if(authority?.GetMethod("IsOwnerSide",new[]{typeof(Player)}) is {} player)
            playerAuthority=new Hook(player,PlayerAuthority);
        if(authority?.GetMethod("IsOwnerSide",new[]{typeof(Projectile)}) is {} projectile)
            projectileAuthority=new Hook(projectile,ProjectileAuthority);
        if(authority?.GetMethod("OwnerOrNull",new[]{typeof(Projectile)}) is {} owner)
            projectileOwner=new Hook(owner,ProjectileOwner);
    }
    void Prepare(Player actor,Vector2 aim){
        var state=actor.GetModPlayer(input);mouse.SetValue(state,aim);
        if(left?.FieldType==typeof(bool))left.SetValue(state,actor.controlUseItem);
        if(right?.FieldType==typeof(bool))right.SetValue(state,actor.controlUseTile);
    }
    // Human weapons remain client-owned. Tagged companions are simulated on
    // the server, and their borrowed actor need not occupy a real player slot.
    static bool PlayerAuthority(Func<Player,bool> original,Player player)=>CompanionNativeUse.Current?.Actor==player?!CompanionNetwork.Client:original(player);
    static bool ProjectileAuthority(Func<Projectile,bool> original,Projectile projectile)=>CompanionOwnedShot.Of(projectile)!=null?!CompanionNetwork.Client:original(projectile);
    static Player ProjectileOwner(Func<Projectile,Player> original,Projectile projectile)=>CompanionOwnedShot.Of(projectile) is {Available:true} use?use.Actor:original(projectile);
    public override void Unload(){CompanionCrossMod.PrepareActor-=Prepare;playerAuthority?.Dispose();projectileAuthority?.Dispose();projectileOwner?.Dispose();playerAuthority=projectileAuthority=projectileOwner=null;input=null;mouse=left=right=null;}
}
