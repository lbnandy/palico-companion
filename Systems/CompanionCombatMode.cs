namespace PalicoCompanion.Systems;

public enum CompanionCombatMode:byte { Proactive, Assist, Passive }

internal static class CompanionCombatModes {
    internal static CompanionCombatMode Valid(int value)=>value is >=0 and <=2?(CompanionCombatMode)value:CompanionCombatMode.Proactive;
    internal static CompanionCombatMode Next(CompanionCombatMode mode)=>(CompanionCombatMode)(((int)mode+1)%3);
    internal static string Key(CompanionCombatMode mode)=>mode switch {
        CompanionCombatMode.Assist=>"CombatAssist",
        CompanionCombatMode.Passive=>"CombatPassive",
        _=>"CombatProactive"
    };
}
