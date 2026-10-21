using System.IO;
using Terraria;
using Terraria.UI;
using Terraria.ModLoader.IO;
namespace PalicoCompanion.Systems;

internal static partial class CompanionNetwork {
    const byte QuickTrash=26,QuickTrashResult=27;
    internal static bool QuickTrashHeld=>ItemSlot.ControlInUse&&!ItemSlot.Options.DisableQuickTrash&&ItemSlot.Options.DisableLeftShiftTrashCan;
    internal static void Trash(CompanionPlayer s,Item[] items,int index){
        if(!Client||Pending||CompanionSession.Closing||!s.NetReady||items[index].IsAir||items[index].favorited)return;
        byte group=(byte)(ReferenceEquals(items,s.Bag)?0:ReferenceEquals(items,s.Gear)?1:ReferenceEquals(items,s.Dyes)?2:3);
        var p=Packet(QuickTrash);pending=++sequence;p.Write(pending);p.Write(group);p.Write((ushort)index);ItemIO.Send(items[index],p,true,true);RememberRequest(p);p.Send();
    }
    static void ReadTrashResult(BinaryReader r){
        int id=r.ReadInt32();ReadState(r,true,apply:MatchesPending(id));var item=ItemIO.Receive(r,true,true);
        if(!MatchesPending(id))return;pending=0;request=null;
        if(item.IsAir)return;
        // The server withdraws exactly once; the owner's native trash/shop path
        // supplies its sounds, recoverable trash slot and mod selling hooks.
        var slot=new[]{item};ItemSlot.SellOrTrash(slot,ItemSlot.Context.BankItem,0);
        if(!slot[0].IsAir)Give(slot);
    }
    static void ReadTrash(CompanionPlayer s,BinaryReader r){
        int id=r.ReadInt32();byte group=r.ReadByte();int index=r.ReadUInt16();var expected=ItemIO.Receive(r,true,true);
        if(id<=s.NetCommand){ReplyAgain(s,id);return;}s.NetCommand=id;
        var items=group==0?s.Bag:group==1?s.Gear:group==2?s.Dyes:s.Training.Slots;var returned=new Item();
        if(group<=3&&index<items.Length&&!items[index].favorited&&Same(items[index],expected)){returned=items[index];items[index]=new Item();}
        CommitInventoryReply(s,QuickTrashResult,id,returned:[returned]);
    }
}
