using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Reuse the equipment actor already evaluated by the companion tick. Reading
// work attributes must not replay equipment hooks or spawn passive effects.
internal static class CompanionWorkAttributes {
    internal static CompanionNativeUse Context(CompanionPlayer state)=>
        CompanionEquipment.Independent(state)&&state.Cat?.ModProjectile is CompanionCat cat&&
        cat.Weapons.State==state&&cat.Weapons.AttributesReady?cat.Weapons:null;
    internal static Player Source(CompanionPlayer state)=>Context(state)?.Actor??state.Player;
    internal static float UseTime(Player owner,Item tool,bool mining=false){
        var state=owner.GetModPlayer<CompanionPlayer>();var use=Context(state);
        var actor=use?.Actor??owner;
        using var scope=use==null?null:new CompanionNativeUse.Scope(use,trackHealing:false);
        return CombinedHooks.TotalUseTime(tool.useTime*(mining?actor.pickSpeed:1),actor,tool);
    }
}
