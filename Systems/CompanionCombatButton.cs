using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using Terraria.Localization;
namespace PalicoCompanion.Systems;
internal static class CompanionCombatButton {
    internal const int Size=CompanionIconToggle.Size;
    static readonly CompanionIconToggle button=new(ItemID.CopperBroadsword,.565f);
    internal static void Unload()=>button.Unload();
    internal static void Draw(SpriteBatch sb,CompanionPlayer s,Rectangle bounds,Point mouse,bool clicked){
        var next=CompanionCombatModes.Next(s.CombatMode);
        string key="Mods.PalicoCompanion.UI."+CompanionCombatModes.Key(s.CombatMode);
        string hint=Language.GetTextValue(key)+"\n"+Language.GetTextValue(key+"Hint")+"\n"+
            Language.GetTextValue("Mods.PalicoCompanion.UI.CombatModeHint",Language.GetTextValue("Mods.PalicoCompanion.UI."+CompanionCombatModes.Key(next)));
        button.Draw(sb,s,bounds,mouse,clicked,s.CombatMode!=CompanionCombatMode.Passive,hint,
            ()=>CompanionControl.Request(s,CompanionControlOp.CombatMode,(int)next),s.CooperativeCombat);
    }
}
