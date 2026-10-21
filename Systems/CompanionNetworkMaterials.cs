using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;
namespace PalicoCompanion.Systems;

internal static partial class CompanionNetwork {
    const byte MaterialRequest=31;
    static bool automaticMaterial;
    internal static bool BlocksItemUse=>Pending&&!automaticMaterial;
    internal static bool WorkMaterial(Item i)=>TreeSpecies.IsSeed(i.type)||CompanionLighting.Available(i)||ConstructionAssist.Supported(i);
    // Request one real consumable. Never debit the server's stale copy of a
    // human inventory; the existing escrow/receipt/resume path owns the transfer.
    internal static void RequestMaterial(CompanionPlayer s,int type){
        if(!Server||!s.NetReady||s.NetLeaving||!s.Enabled||s.Paused||s.Vitals.Downed||Main.GameUpdateCount<s.NetMaterialAt)return;
        var sample=Array.Find(s.Player.inventory,i=>!i.IsAir&&!i.favorited&&(type==0?CompanionLighting.Torch(i):i.type==type));
        if(sample==null||!WorkMaterial(sample))return;
        s.NetMaterialAt=Main.GameUpdateCount+60;using var p=Packet(MaterialRequest);p.Write(type);p.Send(s.Player.whoAmI);
    }
    static void SupplyMaterial(int type){
        var s=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();
        if(!Client||Pending||CompanionSession.Closing||!s.NetReady||!s.Enabled||s.Paused||s.Vitals.Downed)return;
        int slot=Array.FindIndex(s.Player.inventory,0,50,i=>!i.IsAir&&!i.favorited&&(type==0?CompanionLighting.Available(i):i.type==type&&WorkMaterial(i)));if(slot<0)return;
        var source=s.Player.inventory[slot];var offered=InventoryTransfer.Empty(50);offered[slot]=source.Clone();offered[slot].stack=1;
        var p=Packet(Bulk);automaticMaterial=true;pending=++sequence;p.Write(pending);p.Write(true);p.Write(slot);p.Write((byte)4);
        if(--source.stack==0)source.TurnToAir();NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,Main.myPlayer,slot);
        bulkEscrow=offered;Items(p,offered);Items(p,s.Player.inventory);RememberRequest(p);p.Send();
    }
}
