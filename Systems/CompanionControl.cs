using System.IO;
using Microsoft.Xna.Framework;
using Terraria;

namespace PalicoCompanion.Systems;

internal enum CompanionControlOp:byte { Direct, Skill, SwitchLoadout }

internal static class CompanionControl {
    internal static bool Execute(CompanionPlayer s,CompanionControlOp op,int index,Point cell,string name,out string message) {
        message=CompanionText.Message("CompanionControl.Unavailable");
        if(CompanionNetwork.Client||s.Player.dead||s.NetLeaving)return false;
        switch(op) {
            case CompanionControlOp.Direct:return s.Directive.Request(s,(DirectiveKind)index,cell,out message);
            case CompanionControlOp.Skill:return s.Training.Activate(s,index,true,out message);
            case CompanionControlOp.SwitchLoadout:return s.Loadouts.Switch(s,index,out message);
        }
        return false;
    }
    internal static void Request(CompanionPlayer s,CompanionControlOp op,int index,Point cell=default,string name="") {
        if(CompanionNetwork.Pending){s.Report(CompanionText.Message("CompanionControl.Pending"));CompanionControlUI.Notify(s.Notice);return;}
        if(CompanionNetwork.Client){CompanionNetwork.Control(op,index,cell,name);return;}
        bool accepted=Execute(s,op,index,cell,name,out string message);s.Report(message);CompanionControlUI.Notify(message);
        if(accepted)new InventoryOutcome(InventoryCue.Tick).Present(s);
    }
}

internal static partial class CompanionNetwork {
    const byte ControlRequest=33,ControlResult=34;
    internal static void Control(CompanionControlOp op,int index,Point cell,string name) {
        if(!Client||Pending||CompanionSession.Closing||!Main.LocalPlayer.GetModPlayer<CompanionPlayer>().NetReady)return;
        var p=Packet(ControlRequest);pending=++sequence;p.Write(pending);p.Write((byte)op);p.Write(index);p.Write(cell.X);p.Write(cell.Y);p.Write(name??"");
        RememberRequest(p);p.Send();
    }
    static void ReadControl(CompanionPlayer s,BinaryReader r) {
        int id=r.ReadInt32();var op=(CompanionControlOp)r.ReadByte();int index=r.ReadInt32();var cell=new Point(r.ReadInt32(),r.ReadInt32());string name=r.ReadString();
        if(id<=s.NetCommand){ReplyAgain(s,id);return;}s.NetCommand=id;
        bool accepted=CompanionControl.Execute(s,op,index,cell,name,out var message);
        var receipt=new InventoryReceipt(ControlResult,id,accepted,System.Array.Empty<Item>(),new InventoryOutcome(accepted?InventoryCue.Tick:InventoryCue.None),message);
        RememberReply(s,receipt);using var reply=ReceiptPacket(s,receipt);
        bool deliver=true;
        if(deliver)reply.Send(s.Player.whoAmI);Send(s);
    }
    static void ReadControlResult(BinaryReader r) {
        int id=r.ReadInt32();bool apply=MatchesPending(id);
        ReadState(r,true,apply:apply);var outcome=InventoryOutcome.Read(r);string message=r.ReadString();
        if(!apply)return;
        FinishInventoryReply();var s=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();s.Report(message);CompanionControlUI.Notify(message);outcome.Present(s);
    }
}
