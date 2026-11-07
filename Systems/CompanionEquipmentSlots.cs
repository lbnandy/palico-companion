using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Keep native slot identities: the Demon Heart and Master slots need not be
// unlocked together. Owner queries also include installed mods' native hooks.
public sealed class CompanionEquipmentSlots : ModSystem {
    internal static bool Unlocked(CompanionPlayer state,int slot)=>
        slot<CompanionEquipment.AccessoryStart||state.Player.IsItemSlotUnlockedAndUsable(CompanionEquipment.ArmorIndex(slot));
    internal static int Visible(CompanionPlayer state){
        for(int i=11;i>=10;i--)if(Unlocked(state,i)||!state.Gear[i].IsAir)return i-4;
        return 5;
    }
    public override void Load()=>On_Player.IsItemSlotUnlockedAndUsable+=ForCompanion;
    public override void Unload()=>On_Player.IsItemSlotUnlockedAndUsable-=ForCompanion;
    static bool ForCompanion(On_Player.orig_IsItemSlotUnlockedAndUsable orig,Player player,int slot){
        var use=CompanionNativeUse.Current;
        if(use?.Actor==player&&CompanionEquipment.Independent(use.State))
            return use.State.Player.IsItemSlotUnlockedAndUsable(slot);
        return orig(player,slot);
    }
}
