using System;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Transient owner-only presentation, separate from saved equipment/cooldowns.
// Native actions report real contributions; scanning and failed attempts do not.
public sealed class TrainingWorkFeedback : ModPlayer {
    internal const byte NoticeKind=4;
    const uint Interval=90;
    int kind=-1;
    uint shownAt,sentAt;
    bool sent;
    public override void Initialize(){kind=-1;sent=false;}

    internal static void Trigger(CompanionPlayer s,int equipment){
        if(CompanionNetwork.Client||equipment is <8 or >10||!s.Training.Work(equipment)||!s.Training.Active(s))return;
        var feedback=s.Player.GetModPlayer<TrainingWorkFeedback>();
        uint now=Main.GameUpdateCount;
        // Coalesce bursts (tree drops, chain mining, multi-item boss rewards).
        // This gates feedback only, never the underlying equipment benefit.
        if(feedback.sent&&now-feedback.sentAt<Interval)return;
        feedback.sent=true;feedback.sentAt=now;
        if(CompanionNetwork.Server)CompanionNetwork.TrainingEffect(s,NoticeKind,equipment);
        else Present(s,equipment);
    }
    internal static void Present(CompanionPlayer s,int equipment){
        if(equipment is <8 or >10||!s.Training.Work(equipment))return;
        var feedback=s.Player.GetModPlayer<TrainingWorkFeedback>();
        feedback.kind=equipment;feedback.shownAt=Main.GameUpdateCount;
    }
    internal static float Strength(CompanionPlayer s,int equipment){
        var feedback=s.Player.GetModPlayer<TrainingWorkFeedback>();
        uint elapsed=Main.GameUpdateCount-feedback.shownAt;
        if(feedback.kind!=equipment||!s.Training.Work(equipment)||!s.Training.Active(s))return 0;
        // Quick rise, quiet fade; no repeated sounds, scaling, or timer label.
        return TrainingHudFeedback.Pulse(elapsed);
    }
}
