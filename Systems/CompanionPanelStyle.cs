using Microsoft.Xna.Framework;
using Terraria.GameContent.UI.Elements;

namespace PalicoCompanion.Systems;

internal static class CompanionPanelStyle {
    // Fade backgrounds only, never text, icons, borders or skill overlays.
    internal const float Opacity=.80f;
    internal const float PageOpacity=.88f;
    internal static readonly Color Background=new Color(63,82,151)*Opacity;
    internal static Color Tint(Color opaque)=>opaque*Opacity;
}
