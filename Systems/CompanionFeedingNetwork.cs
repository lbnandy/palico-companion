using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;
namespace PalicoCompanion.Systems;

internal static partial class CompanionNetwork {
    const byte FeedRequest=36,FeedResult=37;
    internal static void Feed(CompanionPlayer s){
        int slot=s.Player.selectedItem;
        if(!Client||Pending||CompanionSession.Closing||!s.NetReady||!Main.mouseItem.IsAir||!CanMoveInventory(s.Player)||slot is <0 or >=10||!CompanionFeeding.Food(s.Player.HeldItem))return;
        var item=s.Player.HeldItem;escrow=item.Clone();escrow.stack=1;
        if(--item.stack==0)item.TurnToAir();
        var p=Packet(FeedRequest);pending=++sequence;p.Write(pending);p.Write((byte)slot);ItemIO.Send(item,p,true,true);ItemIO.Send(escrow,p,true,true);
        RememberRequest(p);
        // Native player inventory is client-owned. Reserve one unit first;
        // validation sees the ordered native slot/control updates before Feed.
        NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,Main.myPlayer,slot);
        NetMessage.SendData(MessageID.PlayerControls,-1,-1,null,Main.myPlayer);
        p.Send();
    }
    static void ReadFeed(CompanionPlayer s,BinaryReader r){
        int id=r.ReadInt32(),slot=r.ReadByte();var expected=ItemIO.Receive(r,true,true);var offered=ItemIO.Receive(r,true,true);
        if(id<=s.NetCommand){ReplyAgain(s,id);return;}s.NetCommand=id;
        string message=CompanionFeeding.Message("Changed");
        // Native slot synchronization carries type, stack and prefix, but not
        // the local favorite flag. Food is vanilla-only, so these are sufficient.
        bool slotMatches=slot<10&&(expected.IsAir?s.Player.inventory[slot].IsAir:
            s.Player.inventory[slot].type==expected.type&&s.Player.inventory[slot].stack==expected.stack&&s.Player.inventory[slot].prefix==expected.prefix);
        bool valid=slotMatches&&s.Player.selectedItem==slot&&offered.stack==1&&CompanionFeeding.Food(offered)&&
            (expected.IsAir||expected.type==offered.type&&expected.prefix==offered.prefix);
        bool accepted=valid&&CompanionFeeding.Accept(s,offered,out message);
        var receipt=new InventoryReceipt(FeedResult,id,accepted,[offered.Clone()],default,message);
        RememberReply(s,receipt);using var packet=ReceiptPacket(s,receipt);packet.Send(s.Player.whoAmI);Send(s);
    }
    static void ReadFeedResult(BinaryReader r){
        int id=r.ReadInt32();bool apply=MatchesPending(id);ReadState(r,true,apply:apply);
        var returned=ItemIO.Receive(r,true,true);_ = InventoryOutcome.Read(r);string message=r.ReadString();
        if(!apply)return;FinishInventoryReply();
        if(!returned.IsAir)CompanionInventoryOperations.GiveConfirmed([returned]);
        Main.LocalPlayer.GetModPlayer<CompanionPlayer>().Report(message);Recipe.FindRecipes();
    }
}
