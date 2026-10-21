using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

public static class CompanionRecovery {
    static readonly System.Collections.Generic.Dictionary<System.Type,bool> ordinary=new();
    internal static void Unload()=>ordinary.Clear();
    static bool OrdinaryModPotion(Item i){
        if(i.ModItem==null||!i.consumable||i.buffType!=0||i.shoot>ProjectileID.None||i.damage>0||i.createTile!=-1||i.createWall>=0)return false;
        var type=i.ModItem.GetType();
        // Bloodfin-like consumables grant a persistent custom buff in these
        // hooks. A vitals-only actor cannot faithfully run every mod's complete
        // player update; do not silently consume those for just the base heal.
        if(!ordinary.TryGetValue(type,out bool supported))ordinary[type]=supported=
            type.GetMethod(nameof(ModItem.OnConsumeItem))?.DeclaringType==typeof(ModItem)&&type.GetMethod(nameof(ModItem.UseItem))?.DeclaringType==typeof(ModItem);
        return supported;
    }
    public static bool ManaPotion(Item i)=>!i.IsAir&&!i.favorited&&i.stack>0&&
        (i.type is ItemID.LesserManaPotion or ItemID.ManaPotion or ItemID.GreaterManaPotion or ItemID.SuperManaPotion||OrdinaryModPotion(i)&&i.healMana>0&&i.healLife==0&&!i.potion);
    // Supported potions use QuickHeal/QuickMana on the isolated actor.
    // Buff foods, mixed potions and projectile consumables are separate behavior.
    public static bool Supported(Item i)=>!i.IsAir&&!i.favorited&&i.stack>0&&
        (i.type is ItemID.LesserHealingPotion or ItemID.HealingPotion or ItemID.GreaterHealingPotion or ItemID.SuperHealingPotion or ItemID.RestorationPotion||OrdinaryModPotion(i)&&i.potion&&i.healLife>0&&i.healMana==0);
}
