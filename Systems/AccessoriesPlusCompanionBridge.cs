using System;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class AccessoriesPlusCompanionBridge:ModSystem {
    Hook placement;
    public override void Load(){
        if(!ModLoader.TryGetMod("AccessoriesPlus",out var mod))return;
        var method=mod.Code.GetType("AccessoriesPlus.Content.AccessorySlots.CustomSlotSystem")?
            .GetMethod(nameof(GlobalItem.CanEquipAccessory),BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly,
                null,new[]{typeof(Item),typeof(Player),typeof(int),typeof(bool)},null);
        if(method?.ReturnType==typeof(bool))placement=new Hook(method,(PlacementHook)CanEquip);
    }
    delegate bool Placement(GlobalItem self,Item item,Player player,int slot,bool modded);
    delegate bool PlacementHook(Placement original,GlobalItem self,Item item,Player player,int slot,bool modded);
    static bool CanEquip(Placement original,GlobalItem self,Item item,Player player,int slot,bool modded){
        // This hook only forces wings/boots/shields into the player's dedicated
        // slots, which the companion does not have. Keep the rest of ItemSlot's
        // validation, including other mods' restrictions and accessory conflicts.
        if(ReferenceEquals(player,CompanionEquipment.ValidatingActor))return true;
        return original(self,item,player,slot,modded);
    }
    public override void Unload(){placement?.Dispose();placement=null;}
}
