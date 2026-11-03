using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using CalamityMod.CalPlayer;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Items.Weapons.Rogue;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Projectiles.Rogue;

namespace PalicoCompanion.Systems;

// Optional typed adapter: tML omits this class when Calamity is absent.
// Only the isolated companion actor is modified; human controls stay intact.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityCompanionBridge:ModSystem {
    public override void Load(){CompanionCrossMod.PrepareActor+=Aim;CompanionCrossMod.SafeThrow+=Throw;CompanionCrossMod.UsesPerMaterial=Material;CompanionCrossMod.ReleaseChannel=Release;CompanionCrossMod.WeaponReach+=Range;}
    public override void Unload(){CompanionCrossMod.PrepareActor-=Aim;CompanionCrossMod.SafeThrow-=Throw;CompanionCrossMod.UsesPerMaterial=null;CompanionCrossMod.ReleaseChannel=null;CompanionCrossMod.WeaponReach-=Range;}
    static bool Throw(Item item)=>item.ModItem is RogueWeapon&&item.damage>0&&item.shoot>0&&
        item.createTile<0&&item.createWall<0&&item.ammo==0;
    static int Material(Item weapon,Item material)=>weapon.type==ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.WulfrumBlunderbuss>()&&material.type==ModContent.ItemType<CalamityMod.Items.Materials.WulfrumMetalScrap>()?CalamityMod.Items.Weapons.Ranged.WulfrumBlunderbuss.ShotsPerScrap:0;
    // Primary use is a held slash, not the alternate thrown attack. Approach
    // within its native hitbox instead of treating its holdout as a long shot.
    static float? Range(Item weapon)=>weapon.ModItem is TheFinalDawn?
        Terraria.ID.ContentSamples.ProjectilesByType[ModContent.ProjectileType<FinalDawnFireSlash>()].width*.5f:null;
    static bool Release(Item weapon,CompanionNativeUse use){
        if(weapon.ModItem is not (Condemnation or ClockworkBow or MagnaCannon or Supernova or TheFinalDawn or SealedSingularity))return false;
        foreach(var p in Main.projectile){
            if(!p.active||CompanionOwnedShot.Of(p)!=use)continue;
            // Read the native holdout's charge state, not elapsed wall time.
            // Once released, keep the input up until its entire volley finishes.
            // Calamity still owns projectile spawning, ammo and charge bonuses.
            bool full;
            if(weapon.ModItem is Condemnation&&p.ModProjectile is CondemnationHoldout bow)full=bow.homing;
            else if(weapon.ModItem is ClockworkBow&&p.ModProjectile is ClockworkBowHoldout)full=p.ai[1]>=ClockworkBow.MaxBolts;
            else if(weapon.ModItem is MagnaCannon&&p.ModProjectile is MagnaCannonHoldout)full=p.ai[0]>=MagnaCannon.FullChargeFrames;
            else if(weapon.ModItem is TheFinalDawn&&p.ModProjectile is FinalDawnProjectile)full=p.ai[0]>=FinalDawnProjectile.MaxChargeTime;
            else if(weapon.ModItem is Supernova&&p.ModProjectile is SupernovaHoldout||
                weapon.ModItem is SealedSingularity&&p.ModProjectile is SealedSingularityHoldout){
                // These holdouts fill their own temporary stealth meter even
                // without rogue armor. Release at that native charge cap,
                // before the grenade's overcooking branch; never fill it here.
                var player=use.Actor.GetModPlayer<CalamityPlayer>();
                full=player.temporaryStealthMax>0&&player.rogueStealth>=System.Math.Max(player.temporaryStealthMax,player.rogueStealthMax);
            }
            else continue;
            return full||!use.Actor.channel||weapon.useAmmo>0&&!use.Actor.HasAmmo(weapon);
        }
        return false;
    }
    static void Aim(Player actor,Vector2 target){
        var player=actor.GetModPlayer<CalamityPlayer>();
        player.mouseWorldDeltaFromPlayer=target-actor.MountedCenter;
        player.mouseRotationFromPlayer=player.mouseWorldDeltaFromPlayer.ToRotation();
        player.mouseRight=false;
    }
}

[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityCompanionLaunch:GlobalProjectile {
    public override void PostAI(Projectile shot){
        // Fishbone creates a non-colliding holdout, then relocates it at the
        // actual throw. Initial Shoot clearance cannot cover that later origin.
        // Run once at the native transition, before terrain collision, retaining
        // its velocity, bounce/return AI and all subsequent terrain collisions.
        if(shot.ModProjectile is not FishboneBoomerangProjectile||
            shot.timeLeft!=FishboneBoomerangProjectile.Lifetime||!shot.tileCollide||
            CompanionOwnedShot.Of(shot) is not {Available:true})return;
        var origin=shot.Center;
        CompanionWeaponAim.ClearLaunchFloor(ref origin,shot);
        if(origin==shot.Center)return;
        shot.Center=origin;
        if(!CompanionNetwork.Client)shot.netUpdate=true;
    }
}
