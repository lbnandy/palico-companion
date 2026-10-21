using System;
using Terraria;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

public sealed partial class CompanionNativeUse {
    // Work/potion effects are applied by their existing systems; only the
    // consumption decision belongs here. Never use the human's HeldItem/banks.
    internal bool ShouldConsume(CompanionPlayer state,Item item){
        Cat=state.Cat;State=state;
        if(!EnsureActorSlot())return true;
        if(!initialized){Actor.inventory=InventoryTransfer.Empty(59);Actor.ResetEffects();initialized=true;}
        var inventory=Actor.inventory;int selected=Actor.selectedItem;
        try{
            Actor.inventory=InventoryTransfer.Empty(59);Actor.inventory[0]=item;Actor.selectedItem=0;
            Actor.statLife=state.Vitals.Health;Actor.statLifeMax2=state.Vitals.MaxHealth;
            Actor.statMana=state.Vitals.Mana;Actor.statManaMax2=state.Vitals.MaxMana;
            using(new Scope(this,false))return ItemLoader.ConsumeItem(item,Actor);
        }finally{Actor.inventory=inventory;Actor.selectedItem=selected;}
    }
}
internal static class CompanionConsumption {
    internal static bool Consume(CompanionPlayer state,Item item){
        if(CompanionNetwork.Client||item.IsAir||!item.consumable)return false;
        if(state.Cat?.ModProjectile is CompanionCat cat&&!cat.Weapons.ShouldConsume(state,item))return false;
        if(--item.stack<=0)item.TurnToAir();return true;
    }
}
