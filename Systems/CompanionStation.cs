using Microsoft.Xna.Framework;
using Terraria;

namespace PalicoCompanion.Systems;

// A session-local navigation anchor, not a second owner or a frozen AI.
// Work, targeting and recovery all use the same bounded area.
internal static class CompanionStation {
    internal const float Radius=320;
    internal static Vector2 Origin(Player owner)=>owner.GetModPlayer<CompanionPlayer>().WorkAnchor;
    internal static Vector2 Velocity(Player owner)=>owner.GetModPlayer<CompanionPlayer>().Stationed?Vector2.Zero:owner.velocity;
    internal static Vector2 Bottom(Player owner)=>owner.GetModPlayer<CompanionPlayer>() is {Stationed:true} s?s.StationPoint+new Vector2(0,CompanionNavigation.Height/2f):owner.Bottom;
    internal static bool Contains(CompanionPlayer s,Vector2 point)=>!s.Stationed||Vector2.DistanceSquared(s.StationPoint,point)<=Radius*Radius;
    internal static Vector2 Limit(CompanionPlayer s,Vector2 point){
        if(Contains(s,point))return point;
        return s.StationPoint+(point-s.StationPoint).SafeNormalize(Vector2.Zero)*Radius;
    }
    internal static bool Set(CompanionPlayer s,bool stationed){
        if(CompanionNetwork.Client||!s.Enabled||s.Stowed||s.Vitals.Downed||s.Cat?.ModProjectile is not CompanionCat cat)return false;
        if(s.Stationed==stationed)return true;
        if(stationed&&!CompanionNavigation.Fits(cat.Projectile.Center))return false;
        CompanionLife.Stop(s);cat.Barrel.Cancel(cat);
        s.StationPoint=cat.Projectile.Center.Floor();s.Stationed=stationed;s.Paused=false;
        cat.Navigation.HoldReturn(cat.Projectile);
        if(stationed&&!cat.Navigation.Grounded(cat.Projectile))cat.Navigation.ResumeFlight(cat.Projectile);
        s.Status=CompanionText.MessageFull("Mods.PalicoCompanion.Station."+(stationed?"Guarding":"Following"));
        cat.Projectile.netUpdate=true;
        return true;
    }
}
