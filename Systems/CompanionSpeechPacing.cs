using System;
namespace PalicoCompanion.Systems;

// One conversation, even when several independent behaviours finish together.
// Ticks are simulation updates; rendering FPS never advances this budget.
internal sealed class CompanionSpeechPacing {
    internal enum Priority { Routine, Equipment, Support, Notice, Interaction, Life }
    int ordinaryAt,noticeAt,equipmentAt,supportAt,interactionAt;
    internal static Priority For(string key,string variant=null) {
        if(key is "FirstMeet" or "Downed" or "Recovered" || key=="OwnerHurt"&&variant=="Death" || key=="Welcome"&&variant=="Revived")return Priority.Life;
        if(key is "Pet" or "Recall")return Priority.Interaction;
        if(variant is "GearHoney" or "GearGuard" or "GearCleanse" || key=="Delivered"&&variant=="Life")return Priority.Support;
        if(variant!=null&&variant.StartsWith("Gear",StringComparison.Ordinal))return Priority.Equipment;
        if(key is "Boss" or "BossVictory" or "NoAmmo" or "NoMana" || key=="Working"&&variant=="BagFull")return Priority.Notice;
        return Priority.Routine;
    }
    internal bool Ready(int tick,Priority priority)=>priority switch {
        Priority.Life=>true,
        Priority.Interaction=>tick>=interactionAt,
        Priority.Notice=>tick>=noticeAt,
        Priority.Support=>tick>=noticeAt&&tick>=supportAt,
        Priority.Equipment=>tick>=ordinaryAt&&tick>=equipmentAt,
        _=>tick>=ordinaryAt
    };
    internal void Spoken(int tick,Priority priority) {
        ordinaryAt=tick+720;noticeAt=tick+180;
        if(priority==Priority.Interaction)interactionAt=tick+120;
        if(priority is Priority.Equipment or Priority.Support)equipmentAt=tick+2700;
        if(priority==Priority.Support)supportAt=tick+1200;
    }
}
