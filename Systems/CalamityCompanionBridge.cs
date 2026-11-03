using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using CalamityMod.CalPlayer;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Items.Weapons.Rogue;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Projectiles.Rogue;

namespace PalicoCompanion.Systems;

// Optional typed adapter: tML omits this class when Calamity is absent.
// Only the isolated companion actor is modified; human controls stay intact.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityCompanionBridge:ModSystem {
    public override void Load(){CompanionCrossMod.PrepareActor+=Aim;CompanionCrossMod.SafeThrow+=Throw;CompanionCrossMod.UsesPerMaterial=Material;CompanionCrossMod.ChargeComplete.Add(Charge);CompanionCrossMod.WeaponReach+=Range;}
    public override void Unload(){CompanionCrossMod.PrepareActor-=Aim;CompanionCrossMod.SafeThrow-=Throw;CompanionCrossMod.UsesPerMaterial=null;CompanionCrossMod.ChargeComplete.Remove(Charge);CompanionCrossMod.WeaponReach-=Range;}
    static bool Throw(Item item)=>item.ModItem is RogueWeapon&&item.damage>0&&item.shoot>0&&
        item.createTile<0&&item.createWall<0&&item.ammo==0;
    static int Material(Item weapon,Item material)=>weapon.type==ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.WulfrumBlunderbuss>()&&material.type==ModContent.ItemType<CalamityMod.Items.Materials.WulfrumMetalScrap>()?CalamityMod.Items.Weapons.Ranged.WulfrumBlunderbuss.ShotsPerScrap:0;
    // Primary use is a held slash, not the alternate thrown attack. Approach
    // within its native hitbox instead of treating its holdout as a long shot.
    static float? Range(Item weapon){
        if(weapon.ModItem is TheFinalDawn)return Terraria.ID.ContentSamples.ProjectilesByType[ModContent.ProjectileType<FinalDawnFireSlash>()].width*.5f;
        if(weapon.ModItem is HolyCollider&&CompanionCombatReach.Sample(weapon) is {} shot&&shot.ModProjectile is HolyColliderHoldout hammer)
            return (hammer.HitboxOutset+hammer.HitboxSize.X*.5f)*shot.scale*.85f;
        return null;
    }
    // Only report native completion. Shared input policy owns release latching
    // and interruption for every damage class; no charge timers are simulated.
    static bool? Charge(Item weapon,Projectile shot,CompanionNativeUse use){
        if(weapon.ModItem is Condemnation&&shot.ModProjectile is CondemnationHoldout bow)return bow.homing;
        if(weapon.ModItem is ClockworkBow&&shot.ModProjectile is ClockworkBowHoldout)return shot.ai[1]>=ClockworkBow.MaxBolts;
        if(weapon.ModItem is MagnaCannon&&shot.ModProjectile is MagnaCannonHoldout)return shot.ai[0]>=MagnaCannon.FullChargeFrames;
        if(weapon.ModItem is TheFinalDawn&&shot.ModProjectile is FinalDawnProjectile)return shot.ai[0]>=FinalDawnProjectile.MaxChargeTime;
        if(weapon.ModItem is HolyCollider&&shot.ModProjectile is HolyColliderHoldout hammer)return hammer.chargedSwing;
        // Helium's holdout class is internal; its item declares the projectile
        // type and public charge cap. Read its native counter without reflection.
        if(weapon.ModItem is HeliumFlash&&shot.type==weapon.shoot)return shot.ai[0]>=HeliumFlash.FullChargeFrames;
        if(weapon.ModItem is Supernova&&shot.ModProjectile is SupernovaHoldout||
            weapon.ModItem is SealedSingularity&&shot.ModProjectile is SealedSingularityHoldout){
            var player=use.Actor.GetModPlayer<CalamityPlayer>();
            return player.temporaryStealthMax>0&&player.rogueStealth>=System.Math.Max(player.temporaryStealthMax,player.rogueStealthMax);
        }
        return null;
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
