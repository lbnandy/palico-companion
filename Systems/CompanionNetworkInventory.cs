using System;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
namespace PalicoCompanion.Systems;

// Transport adapter only: shared operations decide item movement and feedback.
internal static partial class CompanionNetwork {
    const byte SortRequest=28,SortResult=29;
    sealed record InventoryReceipt(byte Kind,int Id,bool Accepted,Item[] Returned,InventoryOutcome Outcome,string Message="");
    static ModPacket ReceiptPacket(CompanionPlayer s,InventoryReceipt receipt){
        var packet=Packet(receipt.Kind);packet.Write(receipt.Id);
        if(receipt.Kind==SlotResult)packet.Write(receipt.Accepted);
        WriteState(s,packet);
        if(receipt.Kind is SlotResult or QuickTrashResult)ItemIO.Send(receipt.Returned[0],packet,true,true);
        else if(receipt.Kind==BulkResult)Items(packet,receipt.Returned);
        if(receipt.Kind!=QuickTrashResult)receipt.Outcome.Write(packet);
        if(receipt.Kind==ControlResult)packet.Write(receipt.Message);
        return packet;
    }
    static void CommitInventoryReply(CompanionPlayer s,byte kind,int id,InventoryOutcome outcome=default,bool accepted=true,params Item[] returned){
        var receipt=new InventoryReceipt(kind,id,accepted,returned.Select(i=>i.Clone()).ToArray(),outcome);
        RememberReply(s,receipt);
        using var packet=ReceiptPacket(s,receipt);
        packet.Send(s.Player.whoAmI);
        Send(s);
    }
    internal static bool MatchesPending(int id)=>id>0&&id==pending;
    // ItemSlot.LeftClick observes both clocks. In particular ItemCheck writes
    // inventory[58] back to mouseItem while a cursor-held item is being used.
    internal static bool CanMoveInventory(Player player)=>player.itemAnimation==0&&player.itemTime==0;
    internal static void Click(CompanionPlayer s,Item[] items,int index,bool right,bool favorite=false) {
        if(!Client||Pending||CompanionSession.Closing||!s.NetReady||!CanMoveInventory(s.Player))return;
        trainingApplication=false;byte group=(byte)(ReferenceEquals(items,s.Bag)?0:ReferenceEquals(items,s.Gear)?1:ReferenceEquals(items,s.Dyes)?2:3);
        // Like native dye slots, classify dyes on the rendering client. A
        // dedicated server has no armor shader registrations (Item.dye is 0).
        if(group==2&&!CompanionDye.Accepts(Main.mouseItem))return;
        if(group==1&&!CompanionVitals.Accepts(Main.mouseItem,index))return;
        var p=Packet(Slot);pending=++sequence;p.Write(pending);p.Write(group);p.Write((ushort)index);p.Write(right);p.Write(favorite);
        ItemIO.Send(items[index],p,true,true);escrow=Main.mouseItem;Main.mouseItem=new Item();ItemIO.Send(escrow,p,true,true);RememberRequest(p);p.Send();
    }
    // Same reliable escrow/acknowledgement as equipment transfers; the native use
    // animation starts this transaction without borrowing or replacing mouseItem.
    internal static void UseTrainingItem(CompanionPlayer s,Item material){
        int kind=TrainingCatalog.Id(material);
        if(!Client||Pending||CompanionSession.Closing||!s.NetReady||!s.Training.CanUpgrade(kind))return;
        trainingApplication=true;trainingReturnToBag=true;trainingUpgradeKind=kind;
        var p=Packet(Slot);pending=++sequence;p.Write(pending);p.Write((byte)4);p.Write((ushort)0);p.Write(false);p.Write(false);
        ItemIO.Send(s.Training.Slots[0],p,true,true);escrow=material.Clone();escrow.stack=1;
        ItemIO.Send(escrow,p,true,true);if(--material.stack<=0)material.TurnToAir();RememberRequest(p);p.Send();
        for(int i=0;i<58;i++)if(ReferenceEquals(s.Player.inventory[i],material))NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,Main.myPlayer,i);
    }
    internal static void Transfer(CompanionPlayer state,bool deposit,int slot=-1,byte group=0){
        if(!Client||Pending||CompanionSession.Closing||!state.NetReady||!Main.mouseItem.IsAir||!CanMoveInventory(state.Player))return;
        var offered=InventoryTransfer.Empty(50);var p=Packet(Bulk);pending=++sequence;p.Write(pending);p.Write(deposit);p.Write(slot);p.Write(group);
        if(deposit){for(int i=0;i<50;i++){var item=state.Player.inventory[i];if(item.IsAir||item.favorited||slot>=0&&i!=slot)continue;
            if(slot<0&&!System.Linq.Enumerable.Any(state.Bag,b=>!b.IsAir&&b.type==item.type))continue;
            offered[i]=item;state.Player.inventory[i]=new Item();NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,Main.myPlayer,i);}}
        bulkEscrow=offered;Items(p,offered);Items(p,state.Player.inventory);RememberRequest(p);p.Send();
    }
    static void Give(Item[] items){foreach(var item in items)if(!item.IsAir){var left=Main.LocalPlayer.GetItem(Main.myPlayer,item,GetItemSettings.InventoryEntityToPlayerInventorySettings);if(!left.IsAir)Main.LocalPlayer.QuickSpawnItem(Main.LocalPlayer.GetSource_Misc("PalicoTransfer"),left,left.stack);}for(int i=0;i<50;i++)NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,Main.myPlayer,i);}
    static bool Same(Item a,Item b){using var x=new MemoryStream();using var y=new MemoryStream();using var wx=new BinaryWriter(x);using var wy=new BinaryWriter(y);ItemIO.Send(a,wx,true,true);ItemIO.Send(b,wy,true,true);return x.ToArray().AsSpan().SequenceEqual(y.ToArray());}

    internal static void SortBag(CompanionPlayer s){
        if(!Client||Pending||CompanionSession.Closing||!s.NetReady)return;
        var p=Packet(SortRequest);pending=++sequence;p.Write(pending);RememberRequest(p);p.Send();
    }
    static void FinishInventoryReply(){automaticMaterial=false;pending=0;request=null;escrow=null;bulkEscrow=null;}
    static void ReadSortResult(BinaryReader r){
        int id=r.ReadInt32();ReadState(r,true,apply:MatchesPending(id));var outcome=InventoryOutcome.Read(r);
        if(!MatchesPending(id))return;FinishInventoryReply();outcome.Present(Main.LocalPlayer.GetModPlayer<CompanionPlayer>());
    }
    static void ReadSort(CompanionPlayer s,BinaryReader r,int sender){
        int id=r.ReadInt32();if(id<=s.NetCommand){ReplyAgain(s,id);return;}s.NetCommand=id;
        var outcome=CompanionSorting.Sort(s)?new InventoryOutcome(InventoryCue.Tick):default;
        CommitInventoryReply(s,SortResult,id,outcome);
    }
    static void ReadBulkResult(BinaryReader r){
        int id=r.ReadInt32();ReadState(r,true,apply:MatchesPending(id));var returned=Items(r,256);var outcome=InventoryOutcome.Read(r);
        if(!MatchesPending(id))return;FinishInventoryReply();var s=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();
        if(outcome.Cue==InventoryCue.DeliveryReturn){s.DeliveryRemainder.AddRange(returned.Where(i=>!i.IsAir));return;}
        if(outcome.Cue is InventoryCue.Delivery or InventoryCue.BankDelivery){CompanionDelivery.Receive(s,returned,outcome.Cue==InventoryCue.BankDelivery);return;}
        CompanionInventoryOperations.GiveConfirmed(returned);outcome.Present(s,true);
    }
    static void ReadBulk(CompanionPlayer state,BinaryReader r,int sender){
        int id=r.ReadInt32();bool deposit=r.ReadBoolean();int selected=r.ReadInt32();byte group=r.ReadByte();var offered=Items(r,50);var capacity=Items(r,100);
        if(id<=state.NetCommand){ReplyAgain(state,id);return;}
        if(offered.Length!=50||capacity.Length<50||group>6||group==4&&!deposit||group==5&&deposit||group==6&&!deposit||selected< -1)return;
        state.NetCommand=id;Item[] returned;InventoryOutcome outcome;
        if(group is 5 or 6){
            if(group==6){CompanionInventoryOperations.Deposit(state,offered,preserveFresh:true);returned=offered.Where(i=>!i.IsAir).ToArray();outcome=new(InventoryCue.DeliveryReturn);}
            else {int coins=selected&3;bool share=(selected&4)!=0;returned=capacity.Length==98&&coins<=2&&CompanionDelivery.Near(state)?CompanionDelivery.Plan(state,capacity.Take(58).ToArray(),capacity.Skip(58).ToArray(),share,coins):System.Array.Empty<Item>();outcome=new(coins==1?InventoryCue.BankDelivery:InventoryCue.Delivery);}
            CommitInventoryReply(state,BulkResult,id,outcome,returned:returned);return;
        }
        if(deposit){
            outcome=CompanionInventoryOperations.Deposit(state,offered,equip:group==3,matchingOnly:selected<0);
            if(group==4)outcome=default; // Automatic material handoff is not a UI click.
            returned=offered.Where(i=>!i.IsAir).ToArray();
        }else{
            var inventory=capacity.Take(50).ToArray();
            outcome=CompanionInventoryOperations.Withdraw(CompanionInventoryOperations.Slots(state,group),inventory,selected,out returned);
        }
        CommitInventoryReply(state,BulkResult,id,outcome,returned:returned);
    }
    static void ReadSlotResult(BinaryReader r){
        int id=r.ReadInt32();bool accepted=r.ReadBoolean();ReadState(r,true,apply:MatchesPending(id));var cursor=ItemIO.Receive(r,true,true);var outcome=InventoryOutcome.Read(r);
        if(!MatchesPending(id))return;
        var s=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();
        if(trainingApplication){if(accepted&&trainingUpgradeKind>=13)TrainingItem.UpgradeFeedback(s,trainingUpgradeKind);else s.Report(accepted?CompanionText.Message("CompanionNetworkInventory.UpgradeDone"):CompanionText.Message("CompanionNetworkInventory.UpgradeRejected"));trainingApplication=false;trainingUpgradeKind=-1;}
        var returned=accepted?cursor:escrow;bool toBag=trainingReturnToBag;trainingReturnToBag=false;FinishInventoryReply();
        if(toBag){if(returned!=null&&!returned.IsAir)Give([returned]);return;}
        // Also guard the return boundary, including restored transactions:
        // an ongoing native use still owns temporary slot 58 and mouseItem.
        if(Main.mouseItem.IsAir&&CanMoveInventory(s.Player))Main.mouseItem=returned??new Item();
        else if(returned!=null&&!returned.IsAir)CompanionInventoryOperations.GiveConfirmed([returned]);
        outcome.Present(s);
    }
    static void ReadSlot(CompanionPlayer state,BinaryReader r,int sender){
        int id=r.ReadInt32();byte group=r.ReadByte();int index=r.ReadUInt16();bool right=r.ReadBoolean();bool favorite=r.ReadBoolean();
        var expected=ItemIO.Receive(r,true,true);var cursor=ItemIO.Receive(r,true,true);
        if(id<=state.NetCommand){ReplyAgain(state,id);return;}state.NetCommand=id;
        var items=group==4?state.Training.Slots:CompanionInventoryOperations.Slots(state,group);
        // ModAccessorySlotPlayer.NetHandler likewise stores dye items without
        // requiring a server-side shader. Keep owner/slot/revision validation
        // and the existing item transaction, not a second graphics registry.
        bool ok=items!=null&&index<items.Length&&Same(items[index],expected)&&(group is 2 or 4||CompanionInventoryOperations.Accepts(state,cursor,group,index));
        InventoryOutcome outcome=default;
        if(ok&&group==4)ok=state.Training.Apply(cursor,index);
        else if(ok)outcome=CompanionInventoryOperations.Click(items,index,ref cursor,right,favorite,group);
        CommitInventoryReply(state,SlotResult,id,outcome,ok,cursor);
    }
}
