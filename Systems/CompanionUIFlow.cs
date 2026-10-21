using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Inventory and chest remain available while inspecting the companion.
internal static class CompanionUIFlow {
    internal static bool Visible=>!Main.gameMenu&&!Main.dedServ&&!Main.inFancyUI&&!Main.mapFullscreen;
    internal static void Bag(CompanionPlayer state){TrainingPanel.Open=false;Main.playerInventory=true;state.PanelOpen=true;}
    internal static void Toggle(CompanionPlayer state){if(state.PanelOpen){state.PanelOpen=false;return;}Bag(state);}
    internal static bool Covers(Rectangle area)=>Visible&&
        (TrainingPanel.Open&&TrainingPanel.HitBounds.Intersects(area)||Main.LocalPlayer.GetModPlayer<CompanionPlayer>().PanelOpen&&ModContent.GetInstance<CompanionPanel>().HitBounds.Intersects(area));
}
