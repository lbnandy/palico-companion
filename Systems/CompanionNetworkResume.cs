using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader.IO;
namespace PalicoCompanion.Systems;

internal static partial class CompanionNetwork {
    sealed class ResumeRecord {internal byte[] State;internal InventoryReceipt Reply,SavedReply;internal int Command;internal TagCompound Dialogue;}
    static readonly Dictionary<string,ResumeRecord> sessions=new();
    static CompanionPlayer localState;static byte[] request;
    internal static void Cache(CompanionPlayer s){if(!Server||!s.NetReady||string.IsNullOrEmpty(s.NetSession))return;if(!sessions.TryGetValue(s.NetSession,out var record))sessions[s.NetSession]=record=new();using var stream=new MemoryStream();using var w=new BinaryWriter(stream);WriteState(s,w);record.State=stream.ToArray();record.Command=s.NetCommand;record.Dialogue=s.Dialogue.Save();}
    static byte[] Body(Terraria.ModLoader.ModPacket packet){packet.Flush();using var blank=Terraria.ModLoader.ModContent.GetInstance<PalicoCompanion>().GetPacket();return ((MemoryStream)packet.BaseStream).ToArray().Skip((int)blank.BaseStream.Position).ToArray();}
    const byte KeepSavedReply=204;
    static int savedPending;
    static void RememberRequest(Terraria.ModLoader.ModPacket packet){
        request=Body(packet);
        // A character save can outlive many later commands. Before replacing its
        // receipt, keep that one result alongside the normal latest result.
        if(savedPending<=0)return;
        using var bookmark=Packet(KeepSavedReply);bookmark.Write(savedPending);bookmark.Send();
        savedPending=0;
    }
    static void KeepReceipt(CompanionPlayer s,int id){
        if(sessions.TryGetValue(s.NetSession,out var record)&&record.Reply?.Id==id)
            record.SavedReply=record.Reply;
    }
    static InventoryReceipt FindReceipt(CompanionPlayer s,int id){
        if(id<=0||!sessions.TryGetValue(s.NetSession,out var record))return null;
        return record.Reply?.Id==id?record.Reply:record.SavedReply?.Id==id?record.SavedReply:null;
    }
    static void RememberReply(CompanionPlayer s,InventoryReceipt receipt){Cache(s);if(sessions.TryGetValue(s.NetSession,out var record))record.Reply=receipt;}
    static bool ReplyAgain(CompanionPlayer s,int id){
        if(FindReceipt(s,id) is not {} receipt)return false;
        // Preserve the committed transfer result, but use today's owner index and
        // state. Cached packet bytes would roll back work completed after the ack.
        using var packet=ReceiptPacket(s,receipt);packet.Send(s.Player.whoAmI);return true;
    }
    static bool Restore(CompanionPlayer s){if(!sessions.TryGetValue(s.NetSession,out var record)||record.State==null)return false;using var stream=new MemoryStream(record.State);using var r=new BinaryReader(stream);ReadState(r,true,s);s.NetCommand=record.Command;if(record.Dialogue!=null)s.Dialogue.Load(record.Dialogue);s.NetLeaving=false;s.NetReady=true;return true;}
    static void SendResume(CompanionPlayer s,int? savedRequest,int sender){
        var receipt=FindReceipt(s,savedRequest.GetValueOrDefault());
        using var ready=Packet(Resume);WriteState(s,ready);
        if(savedRequest.HasValue){ready.Write(s.NetCommand);ready.Write(receipt!=null);}
        ready.Send(sender);
        if(receipt!=null){using var reply=ReceiptPacket(s,receipt);reply.Send(sender);}
        Send(s);
    }
    static void ReadResume(BinaryReader r){
        ReadState(r,true);
        if(r.BaseStream.Position==r.BaseStream.Length){RetryRequest();return;}
        int committed=r.ReadInt32();bool replaying=r.ReadBoolean();
        sequence=Math.Max(sequence,committed);
        if(!Pending||replaying)return;
        if(pending>committed){
            if(request is {Length:>0}){RetryRequest();return;}
            // No serialized request and the server never processed it. Restore
            // only the items that were actually reserved on this character.
            if(escrow is {IsAir:false})CompanionInventoryOperations.GiveConfirmed([escrow]);
            if(bulkEscrow!=null)CompanionInventoryOperations.GiveConfirmed(bulkEscrow);
        }
        // An older save can predate receipt retention. Its committed transfer
        // cannot safely be issued or refunded again.
        FinishInventoryReply();trainingApplication=trainingReturnToBag=false;trainingUpgradeKind=-1;
    }
    static void RetryRequest(){if(!Pending||request==null)return;using var packet=Terraria.ModLoader.ModContent.GetInstance<PalicoCompanion>().GetPacket();packet.Write(request);packet.Send();}
    internal static void RetryClosingRequest()=>RetryRequest();
    internal static TagCompound SaveResume(CompanionPlayer s){if(!ReferenceEquals(s,localState))return s.NetResume;savedPending=pending;var tag=new TagCompound{{"sequence",sequence},{"pending",pending},{"automaticMaterial",automaticMaterial},{"trainingReturn",trainingReturnToBag},{"upgradeKind",trainingUpgradeKind}};if(request!=null)tag["request"]=request;if(escrow!=null)tag["cursor"]=ItemIO.Save(escrow);if(bulkEscrow!=null)tag["bulk"]=bulkEscrow.Select(ItemIO.Save).ToList();return tag;}
    static void EnterResume(CompanionPlayer s){automaticMaterial=s.NetResume.GetBool("automaticMaterial");trainingReturnToBag=s.NetResume.GetBool("trainingReturn");trainingUpgradeKind=s.NetResume.ContainsKey("upgradeKind")?s.NetResume.GetInt("upgradeKind"):-1;trainingApplication=trainingReturnToBag;localState=s;sequence=s.NetResume.GetInt("sequence");pending=s.NetResume.GetInt("pending");savedPending=pending;request=s.NetResume.ContainsKey("request")?s.NetResume.GetByteArray("request"):null;escrow=s.NetResume.ContainsKey("cursor")?ItemIO.Load(s.NetResume.GetCompound("cursor")):null;bulkEscrow=s.NetResume.ContainsKey("bulk")?s.NetResume.GetList<TagCompound>("bulk").Select(ItemIO.Load).ToArray():null;if(string.IsNullOrEmpty(s.NetSession))s.NetSession=Guid.NewGuid().ToString("N");}
    static void EndResume(){if(localState!=null){localState.NetSession="";localState.NetResume=new();}request=null;}
    internal static void ClearSessions()=>sessions.Clear();
}
public sealed class CompanionSessionCache:Terraria.ModLoader.ModSystem {
    public override void OnWorldUnload(){if(CompanionNetwork.Server)CompanionNetwork.ClearSessions();}
}
