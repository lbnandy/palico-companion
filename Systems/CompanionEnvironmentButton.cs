using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
namespace PalicoCompanion.Systems;
internal static class CompanionEnvironmentButton {
    internal const int Size=CompanionIconToggle.Size;
    static readonly CompanionIconToggle button=new(ItemID.CopperPickaxe,.565f);
    internal static void Unload()=>button.Unload();
    internal static void Draw(SpriteBatch sb,CompanionPlayer s,Rectangle bounds,Point mouse,bool clicked){
        button.Draw(sb,s,bounds,mouse,clicked,s.WorkMode!=CompanionWorkMode.Off,
            CompanionWorkModes.Hint("Work",s.WorkMode),()=>CompanionEnvironment.RequestToggle(s),s.WorkMode==CompanionWorkMode.Assist);
    }
}
