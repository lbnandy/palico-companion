using System;
using Terraria;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

internal static class CompanionProjectileData {
    internal static bool TryGet<T>(Projectile projectile,out T tag) where T:GlobalProjectile {
        // Like carried world items, foreign/partially initialized projectiles
        // may lack per-entity globals. tML's TryGetGlobalProjectile indexes
        // that array without checking its length. Missing data is not ours.
        int index=ModContent.GetInstance<T>().PerEntityIndex;
        ReadOnlySpan<GlobalProjectile> globals=projectile.EntityGlobals;
        tag=projectile.type>0&&index>=0&&index<globals.Length?globals[index] as T:null;
        return tag!=null;
    }
}
