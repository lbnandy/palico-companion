using Terraria.ModLoader;
namespace PalicoCompanion.Systems;
internal static partial class CompanionNetwork {
    const byte DiscoveryBubble=30;
    internal static void RequestDiscoveryBubble(int emote){
        if(!Client||CompanionSession.Closing)return;
        using var p=Packet(DiscoveryBubble);p.Write((byte)emote);p.Send();
    }
}
