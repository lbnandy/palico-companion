using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using Terraria.Localization;
namespace PalicoCompanion.Systems;
internal static class CompanionEnvironmentButton {
    internal const int Size=CompanionIconToggle.Size;
    static readonly CompanionIconToggle button=new(ItemID.CopperPickaxe);
    internal static void Unload()=>button.Unload();
    internal static void Draw(SpriteBatch sb,CompanionPlayer s,Rectangle bounds,Point mouse,bool clicked){
        string action=Language.GetTextValue("Mods.PalicoCompanion.UI."+(s.EnvironmentPaused?"EnvironmentResume":"EnvironmentPause"));
        button.Draw(sb,s,bounds,mouse,clicked,!s.EnvironmentPaused,action+"\n"+Language.GetTextValue("Mods.PalicoCompanion.UI.EnvironmentHint"),()=>CompanionEnvironment.RequestToggle(s));
    }
}
