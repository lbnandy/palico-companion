using System;
using System.Linq.Expressions;
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
using CalamityMod.Projectiles.BaseProjectiles;

namespace PalicoCompanion.Systems;

// Optional typed adapter: tML omits this class when Calamity is absent.
// Only the isolated companion actor is modified; human controls stay intact.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityCompanionBridge:ModSystem {
    Func<ModProjectile,int> viperChargeFrames;
    int sealedSingularityHoldout=-1,supernovaHoldout=-1;
    public override void Load(){CompanionCrossMod.InheritStats+=Stats;CompanionCrossMod.PrepareActor+=Aim;CompanionCrossMod.SafeThrow+=Throw;CompanionCrossMod.UsesPerMaterial=Material;CompanionCrossMod.ChargeComplete.Add(Charge);CompanionCrossMod.WeaponReach+=Range;CompanionCrossMod.ProjectileFlight+=Flight;}
    public override void Unload(){CompanionCrossMod.InheritStats-=Stats;CompanionCrossMod.PrepareActor-=Aim;CompanionCrossMod.SafeThrow-=Throw;CompanionCrossMod.UsesPerMaterial=null;CompanionCrossMod.ChargeComplete.Remove(Charge);CompanionCrossMod.WeaponReach-=Range;CompanionCrossMod.ProjectileFlight-=Flight;viperChargeFrames=null;sealedSingularityHoldout=supernovaHoldout=-1;}
    static void Stats(Player actor,Player owner,float ratio){
        var target=actor.GetModPlayer<CalamityPlayer>();var source=owner.GetModPlayer<CalamityPlayer>();
        target.critDamage=source.critDamage*ratio;
        target.rogueVelocity=CompanionCombatInheritance.Blend(1f,source.rogueVelocity,ratio);
        // Rage, adrenaline, stealth and equipment flags stay actor-owned.
    }
    // Flight metadata only; aiming and release stay in the shared native path.
    static CompanionBallisticProfile? Flight(Projectile shot,Player actor){
        if(shot.ModProjectile is IronFranciscaProj or LeadTomahawkProj)return new(.2f);
        if(shot.ModProjectile is Brick)return new(.6f,Drag:.98f,GravityRamp:.025f);
        // These stealth attacks fly straight or become a different projectile.
        // Read the isolated actor's native state, never the human's equipment.
        if(shot.ModProjectile is AdamantiteThrowingAxeProjectile or CobaltKunaiProjectile){
            if(actor.GetModPlayer<CalamityPlayer>().StealthStrikeAvailable())return null;
            return shot.ModProjectile is AdamantiteThrowingAxeProjectile?new(.35f,31,.96f):new(.25f,26,MaxFallSpeed:float.PositiveInfinity);
        }
        return null;
    }
    public override void PostSetupContent(){
        // Older Calamity builds use a thrown projectile instead of this newer
        // holdout. Resolve the optional content without a hard CLR type reference.
        sealedSingularityHoldout=ModContent.TryFind<ModProjectile>("CalamityMod/SealedSingularityHoldout",out var singularity)?singularity.Type:-1;
        supernovaHoldout=ModContent.TryFind<ModProjectile>("CalamityMod/SupernovaHoldout",out var supernova)?supernova.Type:-1;
        // The holdout type is internal, but exposes its charge cap as a public
        // instance field. Bind once; do not duplicate its timing or reflect per tick.
        if(!ModContent.TryFind<ModProjectile>("CalamityMod/VitriolicViperHoldout",out var viper))return;
        var field=viper.GetType().GetField("FullyChargedFrames");
        if(field?.FieldType!=typeof(int))return;
        var shot=Expression.Parameter(typeof(ModProjectile));
        viperChargeFrames=Expression.Lambda<Func<ModProjectile,int>>(
            Expression.Field(Expression.Convert(shot,viper.GetType()),field),shot).Compile();
    }
    static bool Throw(Item item)=>item.ModItem is RogueWeapon&&item.damage>0&&item.shoot>0&&
        item.createTile<0&&item.createWall<0&&item.ammo==0;
    static int Material(Item weapon,Item material)=>weapon.type==ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.WulfrumBlunderbuss>()&&material.type==ModContent.ItemType<CalamityMod.Items.Materials.WulfrumMetalScrap>()?CalamityMod.Items.Weapons.Ranged.WulfrumBlunderbuss.ShotsPerScrap:0;
    // Primary use is a held slash, not the alternate thrown attack. Approach
    // within its native hitbox instead of treating its holdout as a long shot.
    static float? Range(Item weapon){
        // Its custom AI is a hand-bound thrust, with a native 78px collision
        // line. Leave the extra thrust offset as margin, not ranged spacing.
        if(weapon.ModItem is WulfrumScrewdriver)return 78f*weapon.scale;
        // Primary attacks remain attached to the hand. Derive spacing from
        // their native swing boxes / collision line, not the alternate throw.
        if(weapon.ModItem is Omniblade&&CompanionCombatReach.Sample(weapon) is {} blade)return blade.width*.5f;
        if(weapon.ModItem is DeathsAscension)return Terraria.ID.ContentSamples.ProjectilesByType[ModContent.ProjectileType<DeathsAscensionSwing>()].width-26f;
        if(weapon.ModItem is DevilsSunrise)return weapon.shootSpeed*5.5f;
        if(weapon.ModItem is TheFinalDawn)return Terraria.ID.ContentSamples.ProjectilesByType[ModContent.ProjectileType<FinalDawnFireSlash>()].width*.5f;
        // Held melee projectiles describe their own collision geometry. Use
        // that reach for the whole native family, including Earth and Collider.
        if(weapon.CountsAsClass(DamageClass.Melee)&&CompanionCombatReach.Sample(weapon) is {} shot&&shot.ModProjectile is BaseCustomUseStyleProjectile held)
            return (held.HitboxOutset+held.HitboxSize.X*.5f)*shot.scale*.85f;
        return null;
    }
    // Only report native completion. Shared input policy owns release latching
    // and interruption for every damage class; no charge timers are simulated.
    bool? Charge(Item weapon,Projectile shot,CompanionNativeUse use){
        if(weapon.ModItem is Roxcalibur&&shot.ModProjectile is RoxcaliburProj)return shot.ai[0]>=1f;
        if(weapon.ModItem is StygianShield&&shot.ModProjectile is StygianShieldAttack shield)return shield.Charge>=StygianShieldAttack.MaxChargeTime;
        if(weapon.ModItem is TauCannon&&shot.ModProjectile is TauCannonHoldout tau)return tau.hasReachedLV3;
        if(weapon.ModItem is ArcNovaDiffuser&&shot.ModProjectile is ArcNovaDiffuserHoldout nova)return nova.ChargeLV2;
        if(weapon.ModItem is TitaniumRailgun&&shot.ModProjectile is TitaniumRailgunScope rail)return rail.ChargePercent>=1f;
        if(weapon.ModItem is OpalStriker&&shot.ModProjectile is OpalStrikerHoldout)return shot.ai[0]>=OpalStriker.FullChargeFrames;
        if(weapon.ModItem is TheHive&&shot.ModProjectile is TheHiveHoldout hive)return hive.FireNuke;
        if(weapon.ModItem is OntologicalDespoiler&&shot.ModProjectile is OntologicalDespoilerHoldout despoiler)return despoiler.ChargeLV2;
        if(weapon.ModItem is Buzzkill&&shot.ModProjectile is BuzzkillHoldout saw)return saw.Time>=BuzzkillHoldout.ChargeupTime;
        if(weapon.ModItem is SuperradiantSlaughterer&&shot.ModProjectile is SuperradiantSlaughtererHoldout radiant)return radiant.Time>=SuperradiantSlaughtererHoldout.ChargeupTime;
        if(weapon.ModItem is VitriolicViper&&shot.type==weapon.shoot&&viperChargeFrames!=null)return shot.ai[2]>=viperChargeFrames(shot.ModProjectile);
        if(weapon.ModItem is Condemnation&&shot.ModProjectile is CondemnationHoldout bow)return bow.homing;
        if(weapon.ModItem is ClockworkBow&&shot.ModProjectile is ClockworkBowHoldout)return shot.ai[1]>=ClockworkBow.MaxBolts;
        if(weapon.ModItem is MagnaCannon&&shot.ModProjectile is MagnaCannonHoldout)return shot.ai[0]>=MagnaCannon.FullChargeFrames;
        if(weapon.ModItem is TheFinalDawn&&shot.ModProjectile is FinalDawnProjectile)return shot.ai[0]>=FinalDawnProjectile.MaxChargeTime;
        if(weapon.ModItem is HolyCollider&&shot.ModProjectile is HolyColliderHoldout hammer)return hammer.chargedSwing;
        // Helium's holdout class is internal; its item declares the projectile
        // type and public charge cap. Read its native counter without reflection.
        if(weapon.ModItem is HeliumFlash&&shot.type==weapon.shoot)return shot.ai[0]>=HeliumFlash.FullChargeFrames;
        if(weapon.ModItem is Supernova&&shot.type==supernovaHoldout||
            weapon.ModItem is SealedSingularity&&shot.type==sealedSingularityHoldout){
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
        if(CompanionOwnedShot.Of(shot) is not {Available:true})return;
        // The native release is synchronized in ai[1], but its local drawing
        // flag is not. A replica receiving that transition must show the same
        // empty holder without executing the owner's firing branch again.
        if(CompanionNetwork.Client&&shot.ModProjectile is BuzzkillHoldout saw){
            saw.NoSawOnHoldout=shot.ai[1]>=1;
            if(saw.NoSawOnHoldout)shot.frame=4;
        }
        // The shield enables tile collision only when releasing its charge.
        // Clear that initial position using its terrain hitbox, not its much
        // larger damage hitbox; subsequent dash collision stays native.
        if(shot.ModProjectile is StygianShieldAttack shield&&shot.tileCollide&&
            shield.DashTime==0&&shield.DashDestination!=Vector2.Zero){
            int width=shot.width,height=shot.height;bool fallThrough=true;var center=new Vector2(.5f);
            shield.TileCollideStyle(ref width,ref height,ref fallThrough,ref center);
            var start=shot.Center;var shieldOrigin=start;
            CompanionWeaponAim.ClearLaunchFloor(ref shieldOrigin,width,height);
            if(shieldOrigin!=start){shot.Center=shieldOrigin;if(!CompanionNetwork.Client)shot.netUpdate=true;}
            return;
        }
        // Fishbone creates a non-colliding holdout, then relocates it at the
        // actual throw. Initial Shoot clearance cannot cover that later origin.
        // Run once at the native transition, before terrain collision, retaining
        // its velocity, bounce/return AI and all subsequent terrain collisions.
        if(shot.ModProjectile is not FishboneBoomerangProjectile||
            shot.timeLeft!=FishboneBoomerangProjectile.Lifetime||!shot.tileCollide)return;
        var origin=shot.Center;
        CompanionWeaponAim.ClearLaunchFloor(ref origin,shot);
        if(origin==shot.Center)return;
        shot.Center=origin;
        if(!CompanionNetwork.Client)shot.netUpdate=true;
    }
}
