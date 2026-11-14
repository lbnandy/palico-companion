using Terraria.Localization;

namespace PalicoCompanion.Systems;

public enum CompanionWorkMode:byte { Automatic, Assist, Off }

internal static class CompanionWorkModes {
    internal static CompanionWorkMode Valid(int value)=>value is >=0 and <=2?(CompanionWorkMode)value:CompanionWorkMode.Automatic;
    internal static CompanionWorkMode Next(CompanionWorkMode mode)=>(CompanionWorkMode)(((int)mode+1)%3);
    internal static string Label(string kind,CompanionWorkMode mode)=>Language.GetTextValue("Mods.PalicoCompanion.UI."+kind+mode);
    internal static string Hint(string kind,CompanionWorkMode mode)=>Label(kind,mode)+"\n"+
        Language.GetTextValue("Mods.PalicoCompanion.UI."+kind+mode+"Hint")+"\n"+
        Language.GetTextValue("Mods.PalicoCompanion.UI.WorkModeNext",Label(kind,Next(mode)));
}
