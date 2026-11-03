using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using CalamityMod.CalPlayer;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Items.Weapons.Rogue;
using CalamityMod.Projectiles.Ranged;

namespace PalicoCompanion.Systems;

// Optional typed adapter: tML omits this class when Calamity is absent.
// Only the isolated companion actor is modified; human controls stay intact.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityCompanionBridge:ModSystem {
    public override void Load(){CompanionCrossMod.PrepareActor+=Aim;CompanionCrossMod.SafeThrow+=Throw;CompanionCrossMod.UsesPerMaterial=Material;CompanionCrossMod.ReleaseChannel=Release;}
    public override void Unload(){CompanionCrossMod.PrepareActor-=Aim;CompanionCrossMod.SafeThrow-=Throw;CompanionCrossMod.UsesPerMaterial=null;CompanionCrossMod.ReleaseChannel=null;}
    static bool Throw(Item item)=>item.ModItem is RogueWeapon&&item.damage>0&&item.shoot>0&&
        item.createTile<0&&item.createWall<0&&item.ammo==0;
    static int Material(Item weapon,Item material)=>weapon.type==ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.WulfrumBlunderbuss>()&&material.type==ModContent.ItemType<CalamityMod.Items.Materials.WulfrumMetalScrap>()?CalamityMod.Items.Weapons.Ranged.WulfrumBlunderbuss.ShotsPerScrap:0;
    static bool Release(Item weapon,CompanionNativeUse use){
        if(weapon.ModItem is not (Condemnation or ClockworkBow or MagnaCannon))return false;
        foreach(var p in Main.projectile){
            if(!p.active||CompanionOwnedShot.Of(p)!=use)continue;
            // Read the native holdout's charge state, not elapsed wall time.
            // Once released, keep the input up until its entire volley finishes.
            // Calamity still owns projectile spawning, ammo and charge bonuses.
            bool full;
            if(weapon.ModItem is Condemnation&&p.ModProjectile is CondemnationHoldout bow)full=bow.homing;
            else if(weapon.ModItem is ClockworkBow&&p.ModProjectile is ClockworkBowHoldout)full=p.ai[1]>=ClockworkBow.MaxBolts;
            else if(weapon.ModItem is MagnaCannon&&p.ModProjectile is MagnaCannonHoldout)full=p.ai[0]>=MagnaCannon.FullChargeFrames;
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
