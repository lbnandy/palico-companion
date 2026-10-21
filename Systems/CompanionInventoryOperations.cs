using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Gameplay returns a result; only the owner's presentation layer plays it.
// In multiplayer the result travels with the transaction acknowledgement.
internal enum InventoryCue : byte { None, Grab, Tick, Delivery, BankDelivery, DeliveryReturn }
internal readonly record struct InventoryOutcome(InventoryCue Cue, int Moved=0) {
    internal void Write(BinaryWriter w){w.Write((byte)Cue);w.Write(Moved);}
    internal static InventoryOutcome Read(BinaryReader r)=>new((InventoryCue)r.ReadByte(),r.ReadInt32());
    internal void Present(CompanionPlayer s, bool report=false){
        if(Main.dedServ||CompanionNetwork.Server||s.Player.whoAmI!=Main.myPlayer)return;
        if(report)s.Report(CompanionText.MessageFull("Mods.PalicoCompanion.UI.Transferred",Moved));
        if(Cue==InventoryCue.None)return;
        SoundEngine.PlaySound(Cue==InventoryCue.Tick?SoundID.MenuTick:SoundID.Grab);
        Recipe.FindRecipes();
    }
    internal static InventoryOutcome Transfer(int count)=>new(count>0?InventoryCue.Grab:InventoryCue.None,count);
}

internal static class CompanionInventoryOperations {
    static bool SameKind(Item a,Item b)=>a.netID==b.netID&&a.type==b.type;
    internal static Item[] Slots(CompanionPlayer s,byte group)=>group switch {0=>s.Bag,1=>s.Gear,2=>s.Dyes,3=>s.Training.Slots,_=>null};
    internal static byte Group(CompanionPlayer s,Item[] items)=>ReferenceEquals(items,s.Bag)?(byte)0:ReferenceEquals(items,s.Gear)?(byte)1:ReferenceEquals(items,s.Dyes)?(byte)2:(byte)3;
    internal static bool Accepts(CompanionPlayer s,Item item,byte group,int index)=>group switch {
        0=>true,1=>CompanionVitals.Accepts(item,index),2=>CompanionDye.Accepts(item),3=>s.Training.Accepts(s,item,index),_=>false
    };
    internal static bool CanSplit(Item item,byte group,int index)=>item.maxStack>1&&(group==0||group==2||group==1&&index==0&&item.consumable);

    // Server adapter for BankItem's cursor transaction. Do not run client-only
    // ItemSlot on a server by swapping Main.LocalPlayer/mouse/input globals.
    // Conformance tests compare these rules to the installed native ItemSlot.
    internal static InventoryOutcome Click(Item[] items,int index,ref Item cursor,bool right,bool favorite,byte group){
        PickupCredit.Clear(cursor);var item=items[index];
        if(!right&&favorite&&cursor.IsAir&&!item.IsAir){item.favorited=!item.favorited;return new(InventoryCue.Tick);}
        if(right){
            if(item.IsAir||!CanSplit(item,group,index)||!cursor.IsAir&&(!SameKind(cursor,item)||!ItemLoader.CanStack(cursor,item)||cursor.stack>=cursor.maxStack))return default;
            int before=item.stack;
            if(cursor.IsAir)cursor=ItemLoader.TransferWithLimit(item,1);else ItemLoader.StackItems(cursor,item,out _,false,1);
            if(item.stack<=0)item.TurnToAir();
            return before>item.stack?new(InventoryCue.Tick,1):default;
        }
        // Native BankItem swaps full stacks, then stacks compatible partials.
        if(cursor.maxStack<=1||item.type!=cursor.type||item.stack==item.maxStack||cursor.stack==cursor.maxStack){items[index]=cursor;cursor=item;item=items[index];}
        if(item.IsAir)items[index]=item=new Item();
        if(SameKind(cursor,item)&&item.stack!=item.maxStack&&cursor.stack!=cursor.maxStack)ItemLoader.TryStackItems(item,cursor,out _);
        if(cursor.IsAir)cursor=new Item();
        return cursor.IsAir&&item.IsAir?default:new(InventoryCue.Grab);
    }
    internal static InventoryOutcome Deposit(CompanionPlayer s,Item[] offered,bool equip=false,bool matchingOnly=false,bool preserveFresh=false){
        int moved=0;
        foreach(var item in offered){
            if(item.IsAir||item.favorited||matchingOnly&&!s.Bag.Any(b=>!b.IsAir&&b.type==item.type))continue;
            if(!preserveFresh)PickupCredit.Clear(item);int before=item.stack;
            if(equip)s.Training.TryEquip(s,item);
            InventoryTransfer.Move(item,s.Bag);
            moved+=before-(item.IsAir?0:item.stack);
        }
        return InventoryOutcome.Transfer(moved);
    }
    internal static InventoryOutcome Withdraw(Item[] source,Item[] capacity,int selected,out Item[] returned){
        var result=new List<Item>();int moved=0;
        for(int i=0;i<source.Length;i++){
            var item=source[i];if(item.IsAir||item.favorited||selected>=0&&selected!=i)continue;
            var copy=item.Clone();int n=InventoryTransfer.Move(item,capacity);
            if(n>0){copy.stack=n;result.Add(copy);moved+=n;}
        }
        returned=result.ToArray();return InventoryOutcome.Transfer(moved);
    }
    internal static InventoryOutcome Take(CompanionPlayer s,Item[] source,int selected=-1){
        var inventory=s.Player.inventory.Take(50).ToArray();
        var outcome=Withdraw(source,inventory,selected,out _);
        Array.Copy(inventory,s.Player.inventory,50);return outcome;
    }
    internal static void GiveConfirmed(Item[] items){
        // Same fifty-slot destination as the server's capacity check and solo
        // transfer. Avoid GetItem's per-stack sounds and special ammo routing.
        var p=Main.LocalPlayer;var inventory=p.inventory.Take(50).ToArray();
        foreach(var item in items)if(!item.IsAir){InventoryTransfer.Move(item,inventory);if(!item.IsAir)p.QuickSpawnItem(p.GetSource_Misc("PalicoTransferRemainder"),item,item.stack);}
        Array.Copy(inventory,p.inventory,50);
        for(int i=0;i<50;i++)NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,p.whoAmI,i);
    }
}
