using System;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class AikoCompanionBridge : ModSystem {
    public override void Load(){
        if(!ModLoader.TryGetMod("Ao",out var mod))return;
        var type=mod.Code.GetType("Ao.ModSystems.TemporaryBodyProj");
        var method=type?.GetMethod(nameof(GlobalProjectile.PreAI),new[]{typeof(Projectile)});
        if(method?.DeclaringType!=type||method?.ReturnType!=typeof(bool)||
            !typeof(GlobalProjectile).IsAssignableFrom(type))return;
        MonoModHooks.Add(method,(Func<Func<GlobalProjectile,Projectile,bool>,GlobalProjectile,Projectile,bool>)PreAI);
    }

    static bool PreAI(Func<GlobalProjectile,Projectile,bool> orig,GlobalProjectile self,Projectile projectile){
        var use=CompanionNativeUse.Current;
        var fishing=CompanionFishing.Current;
        bool scoped=(use!=null&&CompanionOwnedShot.Of(projectile)==use)||
            (fishing!=null&&CompanionFishingBobber.Of(projectile)==fishing);
        if(!scoped)return orig(self,projectile);
        // Aiko restores the human's input for every non-Aiko projectile, even
        // when its body isn't summoned. Preserve the surrounding native actor
        // scope: AI, hit authority and child spawns still belong to this actor.
        // Keep the hook's result and all its own logic; remote peers retain the
        // identity they entered with rather than gaining local authority.
        int player=Main.myPlayer,x=Main.mouseX,y=Main.mouseY;
        try{return orig(self,projectile);}
        finally{Main.myPlayer=player;Main.mouseX=x;Main.mouseY=y;}
    }
}
