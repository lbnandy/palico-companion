using Microsoft.Xna.Framework;
using Terraria;
namespace PalicoCompanion.Systems;

// Look ahead briefly instead of requiring the owner to stand still. The normal
// job range and native tool checks still decide whether a target is workable.
internal static class CompanionWorkOpportunity {
    internal static bool KeepingUp(Player owner,Projectile cat)=>cat!=null&&
        Vector2.DistanceSquared(cat.Center,owner.Center)<480*480&&
        Vector2.DistanceSquared(cat.Center,owner.Center+owner.velocity*30)<480*480;
    internal static bool RemainsNearby(Player owner,Vector2 target,float radius)=>
        Vector2.DistanceSquared(target,owner.Center+owner.velocity*30)<=radius*radius;
}
