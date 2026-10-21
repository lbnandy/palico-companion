using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Reuse vanilla treasure dust: its texture, additive colour, growth/fade and
// drifting are all handled by Terraria. No box, arrow or extra interface layer.
internal static class DiscoveryIndicator {
    internal static int Emitted {get;private set;}
    internal static void Emit(CompanionPlayer s,CompanionDiscovery.Marker marker,int count=1){
        if(Main.gameMenu||Main.mapFullscreen||Main.inFancyUI||Main.netMode==NetmodeID.Server||
            !s.Enabled||s.Stowed||s.Vitals.Downed||!ModContent.GetInstance<CompanionConfig>().Discoveries||
            !s.Discovery.MarkerBounds(marker,s.Player,Main.GameUpdateCount,out var bounds))return;
        bounds.Inflate(3,3);
        for(int i=0;i<count;i++){
            // SpelunkerProjectileHelper uses this dust, scale, fade-in and slow drift.
            var dust=Dust.NewDustDirect(bounds.TopLeft(),bounds.Width,bounds.Height,DustID.TreasureSparkle,0,0,150,default,.3f);
            dust.fadeIn=.75f;dust.velocity*=.1f;dust.noLight=true;
            if(dust.active)Emitted++;
        }
    }
}
