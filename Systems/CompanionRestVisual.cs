using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
namespace PalicoCompanion.Systems;

// Brief stunned pose, then a vanilla dirt/smoke retreat. No persistent corpse.
public static class CompanionRestVisual {
    public const int StunTicks=30;
    public static bool Hidden(CompanionVitals v)=>v.Downed&&v.RecoveryProgress>=StunTicks;
    public static int Frame(CompanionVitals v)=>CompanionSpriteLayout.RestBlink;
    public static int BreathOffset(CompanionVitals v)=>0;
    public static void DustPuff(Projectile cat,bool returning=false)=>CompanionTransitEffects.At(cat.Center);
}
