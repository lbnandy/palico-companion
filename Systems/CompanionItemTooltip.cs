using System;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// ItemSlot only selects the hovered item; native tooltip stats are calculated
// later by Main. Substitute their receiver here, not Main.LocalPlayer or the
// mouse/input globals. Inventory interactions and other tooltips stay human-owned.
public sealed class CompanionItemTooltip : ModSystem {
    static Item hovered;
    static Player subject;
    static bool sharedSlot,backpackSlot;
    static Player inactive;
    internal static void Clear(){hovered=null;subject=null;sharedSlot=backpackSlot=false;}
    internal static void Hover(CompanionPlayer owner,bool sharing=false,bool wornArmor=false,bool backpack=false){
        if(Main.HoverItem.IsAir)return;
        hovered=Main.HoverItem;sharedSlot=sharing;backpackSlot=backpack;
        if(wornArmor){
            // BankItem remains the interaction context. Only its hover clone
            // needs vanilla's equipped-armor flag and the cat's evaluated set.
            hovered.wornArmor=true;
            if(CompanionEquipment.Independent(owner)&&owner.Cat?.ModProjectile is CompanionCat equipped&&equipped.Weapons.Present){subject=equipped.Weapons.Actor;return;}
            inactive??=new Player {whoAmI=255};inactive.setBonus="";subject=inactive;return;
        }
        if(hovered.damage<=0)return;
        if(owner.Cat?.ModProjectile is CompanionCat cat&&cat.Weapons.Available&&
            cat.Weapons.Actor.HeldItem.type==hovered.type&&cat.Weapons.Actor.HeldItem.prefix==hovered.prefix){subject=cat.Weapons.Actor;return;}
        // An inactive companion or a different backpack weapon needs a preview.
        // Do not borrow weapon-specific live effects (e.g. the drum's different
        // summon multiplier), advance buffs, or mutate the real attack actor.
        inactive??=new Player {whoAmI=255};
        inactive.inventory[0]=hovered;inactive.selectedItem=0;
        CompanionCombatInheritance.ResetPreview(inactive);
        CompanionNativeUse.ApplyCombatModifiers(inactive,owner,hovered);
        subject=inactive;
    }
    internal static bool Shared(Item item)=>sharedSlot&&ReferenceEquals(item,hovered)&&ReferenceEquals(item,Main.HoverItem);
    internal static bool PickupStone(Item item)=>backpackSlot&&ReferenceEquals(item,hovered)&&ReferenceEquals(item,Main.HoverItem)&&CompanionInventoryOperations.CanTogglePickupStone(item,0);
    public override void Load()=>IL_Main.MouseText_DrawItemTooltip_GetLinesInfo+=UseCompanion;
    public override void Unload(){IL_Main.MouseText_DrawItemTooltip_GetLinesInfo-=UseCompanion;Clear();inactive=null;}
    public override void OnWorldUnload(){Clear();inactive=null;}
    static Player ForItem(Player human,Item item){
        return subject!=null&&ReferenceEquals(item,hovered)&&ReferenceEquals(item,Main.HoverItem)?subject:human;
    }
    static void UseCompanion(ILContext il){
        var c=new ILCursor(il);int reads=0;
        while(c.TryGotoNext(MoveType.After,
            i=>i.MatchLdsfld<Main>(nameof(Main.player)),
            i=>i.MatchLdsfld<Main>(nameof(Main.myPlayer)),
            i=>i.OpCode==OpCodes.Ldelem_Ref)){
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<Player,Item,Player>>(ForItem);reads++;
        }
        if(reads==0)throw new InvalidOperationException("Native item tooltip player reads not found");
    }
}

public sealed class CompanionSharingTooltip:GlobalItem {
    public override void ModifyTooltips(Item item,List<TooltipLine> lines){
        if(CompanionItemTooltip.PickupStone(item))lines.Add(new(Mod,"PickupStone",CompanionText.Get("CompanionPickup."+(item.type==Terraria.ID.ItemID.EncumberingStone?"StoneLocked":"StoneUnlocked"))));
        if(!CompanionItemTooltip.Shared(item))return;
        // Keep vanilla's favorite marker, but explain its equipment meaning.
        lines.RemoveAll(line=>line.Mod=="Terraria"&&line.Name=="FavoriteDesc");
        lines.Add(new(Mod,"LoadoutSharing",CompanionText.Get("CompanionLoadouts."+(item.favorited?"SharedHint":"ShareHint"))));
    }
}
