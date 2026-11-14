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

// Native hammer combinations store another projectile's local array index.
// Send its identity and resolve the receiving array slot before native AI;
// otherwise a replica can write the smash trigger into an unrelated body.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityCompanionHammerLink:GlobalProjectile {
    public override bool InstancePerEntity=>true;
    bool linked;int identity=-1;
    public override bool AppliesToEntity(Projectile entity,bool lateInstantiation)=>entity.ModProjectile is TriactisHammerProj or TriactisHammerFlare;
    static bool UsesLink(Projectile shot)=>shot.ModProjectile is TriactisHammerProj?shot.ai[1]>0&&shot.ai[1]<4:shot.ai[1]==-2;
    public override void SendExtraAI(Projectile shot,Terraria.ModLoader.IO.BitWriter bits,System.IO.BinaryWriter writer){
        var use=CompanionOwnedShot.Of(shot);bool hasLink=use!=null&&UsesLink(shot);bits.WriteBit(hasLink);
        if(!hasLink)return;
        int index=(int)shot.ai[2];
        var target=index>=0&&index<Main.maxProjectiles?Main.projectile[index]:null;
        writer.Write(target is {active:true}&&target.ModProjectile is TriactisHammerProj&&CompanionOwnedShot.Of(target)==use?target.identity:-1);
    }
    public override void ReceiveExtraAI(Projectile shot,Terraria.ModLoader.IO.BitReader bits,System.IO.BinaryReader reader){
        linked=bits.ReadBit();identity=linked?reader.ReadInt32():-1;
    }
    public override bool PreAI(Projectile shot){
        if(!CompanionNetwork.Client||!linked||!UsesLink(shot))return true;
        var use=CompanionOwnedShot.Of(shot);
        if(use==null||identity<0)return false;
        foreach(var target in Main.ActiveProjectiles)
            if(target.identity==identity&&target.owner==shot.owner&&target.ModProjectile is TriactisHammerProj&&
                target.ai[1]==4&&CompanionOwnedShot.Of(target)==use){shot.ai[2]=target.whoAmI;return true;}
        // Packets can arrive before their referenced projectile; wait for the
        // matching native entity instead of treating a server slot as local.
        return false;
    }
}

// Optional typed adapter: tML omits this class when Calamity is absent.
// Only the isolated companion actor is modified; human controls stay intact.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityCompanionBridge:ModSystem {
    Func<ModProjectile,int> viperChargeFrames;
    int sealedSingularityHoldout=-1,supernovaHoldout=-1;
    public override void Load(){CompanionCrossMod.SupportProgression+=SupportProgression;CompanionCrossMod.PeacefulRespawnLimit+=PeacefulRespawn; CompanionCrossMod.InheritStats+=Stats;CompanionCrossMod.UpdateResources+=Resources;CompanionCrossMod.PrepareActor+=Aim;CompanionCrossMod.SafeThrow+=Throw;CompanionCrossMod.UsesPerMaterial=Material;CompanionCrossMod.ChargeComplete.Add(Charge);CompanionCrossMod.WeaponReach+=Range;CompanionCrossMod.ProjectileFlight+=Flight;}
    public override void Unload(){CompanionCrossMod.SupportProgression-=SupportProgression;CompanionCrossMod.PeacefulRespawnLimit-=PeacefulRespawn; CompanionCrossMod.InheritStats-=Stats;CompanionCrossMod.UpdateResources-=Resources;CompanionCrossMod.PrepareActor-=Aim;CompanionCrossMod.SafeThrow-=Throw;CompanionCrossMod.UsesPerMaterial=null;CompanionCrossMod.ChargeComplete.Remove(Charge);CompanionCrossMod.WeaponReach-=Range;CompanionCrossMod.ProjectileFlight-=Flight;viperChargeFrames=null;sealedSingularityHoldout=supernovaHoldout=-1;}
    // CalamityPlayer.UpdateDead caps non-boss respawns at 180 ticks.
    static int PeacefulRespawn(int normal)=>Math.Min(normal,180);
    static float SupportProgression()=>CalamityMod.DownedBossSystem.downedExoMechs||CalamityMod.DownedBossSystem.downedCalamitas?6:
        CalamityMod.DownedBossSystem.downedYharon?4:CalamityMod.DownedBossSystem.downedDoG?3:
        CalamityMod.DownedBossSystem.downedPolterghast?2:CalamityMod.DownedBossSystem.downedProvidence?1.5f:1;
    static void Resources(CompanionNativeUse use){
        if(CompanionNetwork.Client)return;
        // Native order: rebuild equipment, update stealth, then ItemCheck.
        // This authority-only stage owns recovery and normal attack costs;
        // held weapons retain their native, projectile-driven consumption.
        using(new CompanionNativeUse.Scope(use)){
            // Calamity previews stealth damage using the hovered weapon. The
            // human's tooltip must not change the companion's combat stats.
            var hover=Main.HoverItem;
            try{
                Main.HoverItem=use.Actor.HeldItem;
                var player=use.Actor.GetModPlayer<CalamityPlayer>();
                player.UpdateRogueStealth();
                // PostUpdateMiscEffects applies the native computed bonus after
                // its passive stages, except for Elephant Killer's own resource.
                bool elephant=use.Actor.HeldItem.ModItem is ElephantKiller;
                if(elephant&&!player.heldElephantKillerLastFrame)player.rogueStealth=0f;
                player.heldElephantKillerLastFrame=elephant;
                if(!elephant)use.Actor.GetDamage<CalamityMod.RogueDamageClass>()+=player.stealthDamage;
            }
            finally{Main.HoverItem=hover;}
        }
    }
    static void Stats(Player actor,Player owner,float ratio){
        var target=actor.GetModPlayer<CalamityPlayer>();var source=owner.GetModPlayer<CalamityPlayer>();
        target.critDamage=source.critDamage*ratio;
        target.rogueVelocity=CompanionCombatInheritance.Blend(1f,source.rogueVelocity,ratio);
        // Rage, adrenaline, stealth and equipment flags stay actor-owned.
    }
    // Flight metadata only; aiming and release stay in the shared native path.
    static CompanionBallisticProfile? Flight(Projectile shot,Player actor){
        if(shot.ModProjectile is TriactisHammerProj)return new(.2f,Drag:.99f,MaxFallSpeed:20);
        float speed=actor.GetModPlayer<CalamityPlayer>().rogueVelocity;
        if(shot.ModProjectile is IronFranciscaProj or LeadTomahawkProj)return new(.2f,SpeedScale:speed);
        if(shot.ModProjectile is Brick)return new(.6f,Drag:.98f,GravityRamp:.025f,SpeedScale:speed);
        // These stealth attacks fly straight or become a different projectile.
        // Read the isolated actor's native state, never the human's equipment.
        if(shot.ModProjectile is AdamantiteThrowingAxeProjectile or CobaltKunaiProjectile){
            if(actor.GetModPlayer<CalamityPlayer>().StealthStrikeAvailable())return null;
            return shot.ModProjectile is AdamantiteThrowingAxeProjectile?new(.35f,31,.96f,SpeedScale:speed):new(.25f,26,MaxFallSpeed:float.PositiveInfinity,SpeedScale:speed);
        }
        return null;
    }
    public override void PostSetupContent(){
        // Use Calamity's own sheathed sprite on the back, not its animated
        // inventory blade. The common renderer still owns anchoring and size.
        if(!Main.dedServ)CompanionBackWeapon.RegisterStowedTexture(ModContent.ItemType<Murasama>(),
            ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(ModContent.GetInstance<Murasama>().Texture+"Sheathed"));
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
    static float? Range(Item weapon,Player actor){
        // MurasamaSlash.Colliding uses velocity * 8.6 for the shorter cuts.
        // Reuse its melee scale and leave the collision thickness as margin.
        if(weapon.ModItem is Murasama){
            float scale=actor==null?weapon.scale:CalamityMod.CalamityUtils.GetMeleeScale(actor,false)+weapon.scale-1;
            return weapon.shootSpeed*8.6f*scale*(Main.zenithWorld?2:1);
        }
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
