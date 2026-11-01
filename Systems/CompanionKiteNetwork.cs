using System.IO;
using Terraria;
using Terraria.ID;

namespace PalicoCompanion.Systems;

internal static partial class CompanionNetwork {
    const byte KiteControl=45,KiteResult=46;
    internal static void KiteRequest(CompanionPlayer s,byte action,sbyte direction=0){
        if(!Client||!s.NetReady||s.NetLeaving||s.Cat?.ModProjectile is not CompanionCat c)return;
        if(action==0){NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,Main.myPlayer,s.Player.selectedItem);NetMessage.SendData(MessageID.PlayerControls,-1,-1,null,Main.myPlayer);}
        var p=Packet(KiteControl);p.Write(action);p.Write(c.NetworkInstance);p.Write(c.Kite.Session);p.Write(s.Player.HeldItem.type);p.Write(direction);p.Send();
    }
    static void ReadKite(CompanionPlayer s,BinaryReader r){
        byte action=r.ReadByte();ulong instance=r.ReadUInt64();uint session=r.ReadUInt32();int item=r.ReadInt32();sbyte direction=r.ReadSByte();
        // Resolve the sender's own companion; the request contains no target player.
        if(s.Cat?.ModProjectile is not CompanionCat c||c.NetworkInstance!=instance)return;
        if(action==0){
            string reason="Unavailable";
            if(item==s.Player.HeldItem.type)CompanionKite.Start(s,out reason);
            var reply=Packet(KiteResult);reply.Write(reason??"");reply.Send(s.Player.whoAmI);return;
        }
        if(!c.Kite.Active||session!=c.Kite.Session)return;
        if(action==2)c.Kite.Stop(c);
        else if(action==1&&direction is >=-1 and <=1&&item==c.Kite.ItemType)c.Kite.SetInput(direction);
    }
}
