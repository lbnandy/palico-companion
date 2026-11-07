using System;
using Terraria;
using Terraria.ID;
namespace PalicoCompanion.Systems;
public static class CompanionWeapons {
    // Retain armor indices 1..3 and summon index 4 for existing saves.
    internal const int SummonSlot=4,EquipmentCount=12;
    internal static bool SummonWeapon(Item item)=>!item.IsAir&&Supports(item)&&(CompanionNativeUse.Summoning(item)||CompanionWeaponPolicy.Sentry(item));
    internal static Item Summon(Item[] gear)=>gear.Length>SummonSlot?gear[SummonSlot]:empty;
    static readonly Item empty=new();
    internal static void Migrate(Item[] gear){
        if(gear.Length>SummonSlot&&gear[SummonSlot].IsAir&&SummonWeapon(gear[0])){gear[SummonSlot]=gear[0];gear[0]=new Item();}
    }
    internal static bool UsesMana(CompanionPlayer s)=>s.Gear[0].mana>0||Summon(s.Gear).mana>0;
    // Owned fallback only: never inserted into a transferable inventory.
    static Item basicSword;
    public static Item Active(Item[] gear) => gear.Length>0 && Supports(gear[0]) ? gear[0] : basicSword??=new Item(ItemID.CopperShortsword);
    public static bool Thrust(Item i) => i.type is ItemID.CopperShortsword or ItemID.TinShortsword or ItemID.IronShortsword or ItemID.LeadShortsword or ItemID.SilverShortsword or ItemID.TungstenShortsword or ItemID.GoldShortsword or ItemID.PlatinumShortsword or ItemID.Gladius;
    // Audited native throws: no terrain-changing bombs, dynamite or liquids.
    public static bool SafeThrow(Item i)=>i.type is ItemID.Shuriken or ItemID.ThrowingKnife or ItemID.PoisonedKnife or ItemID.Snowball or ItemID.SpikyBall or ItemID.Grenade or ItemID.StickyGrenade or ItemID.BouncyGrenade or ItemID.Beenade or ItemID.FrostDaggerfish or ItemID.Javelin or ItemID.BoneJavelin or ItemID.MolotovCocktail or ItemID.RottenEgg or ItemID.Bone||CompanionCrossMod.AllowsThrow(i);
    internal static bool Support(Item i)=>CompanionCrossMod.SupportTool?.Invoke(i)==true;
    public static bool Supports(Item i) => !i.IsAir && (i.damage>0||Support(i)) && (!i.consumable||SafeThrow(i)) && i.pick==0 && i.axe==0 && i.hammer==0 && i.useStyle!=ItemUseStyleID.None && (i.shoot>ProjectileID.None||!i.noMelee||Support(i));
    public static float Range(Item i)=>Math.Clamp(Math.Max(i.width,i.height)*i.scale,28,64);
    public static int Duration(Item i) => Math.Clamp(i.useAnimation,12,90);

}
