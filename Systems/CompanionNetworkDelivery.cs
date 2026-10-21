using System.Linq;
using Terraria;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

internal static partial class CompanionNetwork {
    internal static void RetryAutomatic(){if(Client&&Pending&&automaticMaterial)RetryRequest();}
    internal static void Deliver(CompanionPlayer s){
        if(!Client||!s.NetReady||CompanionSession.Closing)return;
        if(Pending){if(automaticMaterial)RetryRequest();return;}
        bool returning=s.DeliveryRemainder.Count>0;
        if(!returning&&!CompanionDelivery.CanOffer(s))return;
        var offered=InventoryTransfer.Empty(50);if(returning){int n=System.Math.Min(50,s.DeliveryRemainder.Count);for(int i=0;i<n;i++)offered[i]=s.DeliveryRemainder[i];s.DeliveryRemainder.RemoveRange(0,n);}
        var p=Packet(Bulk);automaticMaterial=true;pending=++sequence;p.Write(pending);p.Write(returning);
        p.Write(CompanionDelivery.Preference|(ModContent.GetInstance<CompanionConfig>().ShareNewPickups?4:0));p.Write((byte)(returning?6:5));
        bulkEscrow=offered;Items(p,offered);Items(p,s.Player.inventory.Take(58).Concat(s.Player.bank.item).ToArray());RememberRequest(p);p.Send();
    }
}
