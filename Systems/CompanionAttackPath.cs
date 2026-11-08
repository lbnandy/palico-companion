using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Acquisition and firing must agree on obstruction. This grants permission to
// try the native attack, never changes its collision, placement or hit rules.
internal static class CompanionAttackPath {
    internal static bool ThroughWalls(Player owner){
        var state=owner.GetModPlayer<CompanionPlayer>();
        return ThroughWalls(CompanionWeapons.Active(state.Gear),(state.Cat?.ModProjectile as CompanionCat)?.Weapons);
    }
    internal static bool Clear(Item item,CompanionNativeUse use,Vector2 from,Vector2 to)=>
        CompanionCombatReach.Clear(from,to)||ThroughWalls(item,use);
    internal static bool ThroughWalls(Item item,CompanionNativeUse use){
        if(item.IsAir||item.sentry||CompanionWeapons.Support(item))return false;
        int type=item.shoot;
        // A gun's default projectile is not necessarily its actual ammunition.
        // Share the non-consuming native selection used by ballistic aiming.
        if(item.useAmmo>0&&(use==null||!use.PreviewShot(item,out type,out _)))return false;
        if(!ContentSamples.ProjectilesByType.TryGetValue(type,out var shot)||
            !shot.friendly||shot.hostile||shot.ownerHitCheck||shot.minion||shot.sentry||shot.hide||
            ProjectileID.Sets.IsAWhip[type])return false;
        // A held weapon/controller being intangible says nothing about its
        // damaging shots. Spears and short slashes still require an open path.
        if(shot.aiStyle is ProjAIStyleID.HeldProjectile or ProjAIStyleID.Spear or ProjAIStyleID.ShortSword or
            ProjAIStyleID.Flail or ProjAIStyleID.Yoyo or ProjAIStyleID.Harpoon or ProjAIStyleID.NightsEdge or
            ProjAIStyleID.LightsBane or ProjAIStyleID.SleepyOctopod or ProjAIStyleID.ForwardStab or
            ProjAIStyleID.ThickLaser)return false;
        // Native thorn chains grow segments without integrating position, so
        // tileCollide is not their travel rule (Projectile.UpdatePosition).
        return !shot.tileCollide||shot.aiStyle==ProjAIStyleID.Vilethorn;
    }
}
