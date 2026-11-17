using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// These are inventory slots, not equipment: the native ammo picker and recovery
// hooks still decide whether and how an item is used.
internal static class CompanionSupplySlots {
    internal const byte Group=7;
    internal const int AmmoCount=4, Healing=4, Mana=5, Count=6;
    internal static bool Accepts(Item item,int slot)=>slot>=0&&slot<Count&&(item.IsAir||
        (slot<AmmoCount?item.FitsAmmoSlot():slot==Healing?CompanionRecovery.Supported(item):CompanionRecovery.ManaPotion(item)));
    static (int Start,int Count) Range(Item item)=>item.FitsAmmoSlot()?(0,AmmoCount):
        CompanionRecovery.Supported(item)?(Healing,1):CompanionRecovery.ManaPotion(item)?(Mana,1):(0,0);
    internal static IEnumerable<Item> Carried(CompanionPlayer state)=>state.SupplySlots.Concat(state.Bag);
    internal static int Move(Item item,CompanionPlayer state,int limit=int.MaxValue){
        var range=Range(item);
        int moved=0;
        // Match Player.FillAmmo: replenish existing stacks, but only use empty
        // ammo slots when Terraria permits automatic placement there.
        for(int i=range.Start;i<range.Start+range.Count&&!item.IsAir;i++)
            if(!state.SupplySlots[i].IsAir)moved+=InventoryTransfer.Move(item,state.SupplySlots.AsSpan(i,1),limit-moved);
        if(!item.IsAir&&(range.Start>=AmmoCount||item.CanFillEmptyAmmoSlot()))
            moved+=InventoryTransfer.Move(item,state.SupplySlots.AsSpan(range.Start,range.Count),limit-moved);
        return moved+InventoryTransfer.Move(item,state.Bag,limit-moved);
    }
    internal static int Capacity(Item item,CompanionPlayer state){
        var range=Range(item);
        long room=0;
        void Add(Item target){if(target.IsAir)room+=item.maxStack;else if(target.type==item.type&&ItemLoader.CanStack(target,item))room+=Math.Max(0,target.maxStack-target.stack);}
        for(int i=range.Start;i<range.Start+range.Count;i++)
            if(!state.SupplySlots[i].IsAir||i>=AmmoCount||item.CanFillEmptyAmmoSlot())Add(state.SupplySlots[i]);
        foreach(var target in state.Bag)Add(target);
        return (int)Math.Min(int.MaxValue,room);
    }
    internal static bool HasRoom(Item item,CompanionPlayer state)=>Capacity(item,state)>0;
}
