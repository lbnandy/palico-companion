using Microsoft.Xna.Framework;
using Terraria;

namespace PalicoCompanion.Systems;

// Airborne activation reserves a safe landing, not an airborne attack.
// Reuse the ordinary whole-body flight route, then validate the ground charge.
internal sealed class CompanionBarrelLanding {
    internal const int Timeout=180;
    static readonly int[] Columns={0,-16,16,-32,32,-48,48,-64,64,-96,96};
    static readonly Vector2 Half=new(CompanionNavigation.Width/2f,CompanionNavigation.Height/2f);
    readonly CompanionFlightRoute route=new();
    Vector2 goal;
    bool wasRiding;
    // Do not drop behind an owner who is already flying away. Use the same
    // owner leash and the existing lift duration, without a second speed limit.
    internal static bool KeepsUp(Player owner,Vector2 point)=>Vector2.DistanceSquared(CompanionStation.Origin(owner),point)<480*480&&
        Vector2.DistanceSquared(CompanionStation.Origin(owner)+CompanionStation.Velocity(owner)*CompanionBarrelCharge.LiftTicks,point)<480*480;
    internal bool KeepsUp(Player owner)=>KeepsUp(owner,goal);
    internal bool Find(Projectile cat,Player owner,NPC enemy,float speed,out Vector2 landing){
        landing=default;float best=float.MaxValue;
        foreach(int dx in Columns)for(int down=0;down<=240;down+=16){
            var candidate=cat.Center+new Vector2(dx,down);
            if(!CompanionNavigation.TryStand(candidate,out var stand))continue;
            // Only the first supporting surface in a column is a landing.
            float score=Vector2.DistanceSquared(cat.Center,stand);
            if(score<best&&CompanionStation.Contains(owner.GetModPlayer<CompanionPlayer>(),stand)&&Vector2.DistanceSquared(CompanionStation.Origin(owner),stand)<480*480&&
                Vector2.DistanceSquared(enemy.Hitbox.ClosestPointInRect(stand),stand)<=320*320&&
                CompanionBarrelNavigation.Plan(stand-Half,Vector2.Zero,cat.direction,enemy,speed,CompanionBarrelCharge.RushTicks,stand.Y-Half.Y,out _)&&
                route.Next(cat.Center,stand,out _)){
                best=score;landing=stand;
            }
            break;
        }
        route.Clear();return best<float.MaxValue;
    }
    internal void Begin(CompanionCat cat,Vector2 point){goal=point;wasRiding=cat.Navigation.Riding;route.Clear();}
    internal bool Valid=>CompanionNavigation.TryStand(goal,out var stand)&&Vector2.DistanceSquared(stand,goal)<4&&CompanionBarrelNavigation.Clear(goal);
    internal bool Arrived(CompanionCat cat)=>Vector2.DistanceSquared(cat.Projectile.Center,goal)<24*24&&cat.Navigation.Grounded(cat.Projectile);
    internal bool Approach(CompanionCat cat)=>cat.Navigation.ApproachLanding(cat.Projectile,goal,route);
    internal void Cancel(CompanionCat cat){route.Clear();if(!wasRiding)cat.Navigation.HoldReturn(cat.Projectile);}
    internal void Finish(CompanionCat cat){route.Clear();cat.Navigation.HoldReturn(cat.Projectile);}
}
