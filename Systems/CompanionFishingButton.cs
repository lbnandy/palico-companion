using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;

namespace PalicoCompanion.Systems;

internal static class CompanionFishingButton {
    internal const int Size=CompanionIconToggle.Size;
    static readonly CompanionIconToggle button=new(ItemID.WoodFishingPole,.565f);
    internal static void Unload()=>button.Unload();
    internal static void Draw(SpriteBatch sb,CompanionPlayer s,Rectangle bounds,Point mouse,bool clicked){
        button.Draw(sb,s,bounds,mouse,clicked,s.FishingMode!=CompanionWorkMode.Off,
            CompanionWorkModes.Hint("Fishing",s.FishingMode),
            ()=>CompanionControl.Request(s,CompanionControlOp.FishingMode,(int)CompanionWorkModes.Next(s.FishingMode)),
            s.FishingMode==CompanionWorkMode.Assist);
    }
}
