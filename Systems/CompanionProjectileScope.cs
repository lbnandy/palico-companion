using System;
using System.Collections.Generic;
using Terraria;
namespace PalicoCompanion.Systems;

// Vanilla owner-wide queries must see only this actor's projectiles. The real
// player's minions, flails and bobbers retain their owners outside this scope.
internal sealed class CompanionProjectileScope : IDisposable {
    readonly List<(Projectile projectile,int owner)> hidden=new();
    internal CompanionProjectileScope(int owner,Func<Projectile,bool> belongs){
        if(!CompanionNetwork.Server&&!CompanionNetwork.Client)return;
        // The server sentinel also belongs to NPC attacks and other unowned
        // effects. Only isolate identified companion actors from one another;
        // rewriting an unrelated boss projectile can break its death hooks.
        foreach(var p in Main.projectile)if(p.active&&p.owner==owner&&!belongs(p)&&
            (CompanionProjectileData.TryGet<CompanionOwnedShot>(p,out var shot)&&(shot.Context!=null||shot.CatIdentity>=0)||CompanionFishingBobber.Of(p)!=null)){
            hidden.Add((p,p.owner));p.owner=owner==255?254:255;
        }
    }
    public void Dispose(){foreach(var p in hidden)p.projectile.owner=p.owner;}
}
