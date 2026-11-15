using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class CompanionAttackSpeed:ModPlayer {
    // The native hook keeps useTime/useAnimation in proportion, including
    // weapons that intentionally ignore melee stat bonuses. No projectile AI
    // or work timing is rewritten; those retain their original mechanics.
    public override float UseSpeedMultiplier(Item item)=>
        CompanionNativeUse.Current is {} use&&ReferenceEquals(Player,use.Actor)&&
        item.damage>0&&item.pick==0&&item.axe==0&&item.hammer==0&&item.fishingPole==0
            ?CompanionBalanceConfig.Current.AttackSpeedScale:1f;
}
