using System;
using Terraria;
using Terraria.ID;
namespace PalicoCompanion.Systems;
public static class CompanionWeapons {
    // Owned fallback only: never inserted into a transferable inventory.
    static Item basicSword;
    public static Item Active(Item[] gear) => gear.Length>0 && Supports(gear[0]) ? gear[0] : basicSword??=new Item(ItemID.CopperShortsword);
    public static bool Thrust(Item i) => i.type is ItemID.CopperShortsword or ItemID.TinShortsword or ItemID.IronShortsword or ItemID.LeadShortsword or ItemID.SilverShortsword or ItemID.TungstenShortsword or ItemID.GoldShortsword or ItemID.PlatinumShortsword or ItemID.Gladius;
    // Audited native throws: no terrain-changing bombs, dynamite or liquids.
    public static bool SafeThrow(Item i)=>i.type is ItemID.Shuriken or ItemID.ThrowingKnife or ItemID.PoisonedKnife or ItemID.Snowball or ItemID.SpikyBall or ItemID.Grenade or ItemID.StickyGrenade or ItemID.BouncyGrenade or ItemID.Beenade or ItemID.FrostDaggerfish or ItemID.Javelin or ItemID.BoneJavelin or ItemID.MolotovCocktail or ItemID.RottenEgg or ItemID.Bone;
    public static bool Supports(Item i) => !i.IsAir && i.damage>0 && (!i.consumable||SafeThrow(i)) && i.pick==0 && i.axe==0 && i.hammer==0 && i.useStyle!=ItemUseStyleID.None && (i.shoot>ProjectileID.None||!i.noMelee);
    public static float Range(Item i)=>Math.Clamp(Math.Max(i.width,i.height)*i.scale,28,64);
    public static int Duration(Item i) => Math.Clamp(i.useAnimation,12,90);

}
