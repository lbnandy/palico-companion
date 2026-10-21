using Terraria;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Reuses tML's native payment hook, including channelled projectile payments.
// The human player's missing-mana path is deliberately untouched.
public sealed class CompanionManaHooks : ModPlayer {
    public override void OnMissingMana(Item item,int neededMana) {
        var use=CompanionNativeUse.Current;
        if(use!=null&&ReferenceEquals(Player,use.Actor)){
            use.RestoreMana(neededMana);
            if(Player.statMana<neededMana)use.WaitForMana();
        }
    }
}
