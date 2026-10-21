using System;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Presentation follows the existing synchronized effect/cooldown state. It
// never delays an effect, changes its timer, or adds another network message.
public sealed class TrainingHudFeedback : ModPlayer {
    internal const uint PulseDuration=42;
    const float RiseTicks=5, FadeTicks=27;
    struct Cell {
        internal int Kind,Cooldown;
        internal float Glow;
        internal uint PulseAt;
        internal bool Pulsed;
    }
    Cell[] cells;
    public override void Initialize(){cells=new Cell[3];for(int i=0;i<3;i++)cells[i].Kind=-1;}
    internal static float Pulse(uint elapsed)=>elapsed>=PulseDuration?0:
        Math.Min(1,elapsed/RiseTicks)*Math.Clamp((PulseDuration-elapsed)/FadeTicks,0,1);
    internal void Update(CompanionPlayer s){
        bool available=s.Training.Active(s);
        for(int i=0;i<3;i++){
            int kind=TrainingCatalog.Id(s.Training.Slots[i]);
            if(!available||i>=s.Training.Count(s.Player)||kind is <0 or >7){cells[i]=new(){Kind=-1};continue;}
            ref var cell=ref cells[i];int cooldown=s.Training.Cooldowns[kind];
            bool changed=cell.Kind!=kind;
            if(changed)cell=new(){Kind=kind,Cooldown=cooldown};
            bool sustained=s.Training.EffectDuration(kind)>0;
            // A fresh cast jumps back by almost a whole cooldown. Small server
            // corrections (including 0 -> a few ticks) are not new casts.
            // Initial/equipped snapshots establish a baseline without flashing.
            if(!sustained&&!changed&&cooldown-cell.Cooldown>CompanionTraining.CooldownSeconds(kind)*30){cell.Pulsed=true;cell.PulseAt=Main.GameUpdateCount;}
            bool active=TrainingStatus.Effect(s,kind)>0;
            cell.Glow=Math.Clamp(cell.Glow+(active?1/RiseTicks:-1/FadeTicks),0,1);
            cell.Cooldown=cooldown;
        }
    }
    internal static float Strength(CompanionPlayer s,int slot){
        if(slot is <0 or >2||!s.Training.Active(s))return 0;
        var feedback=s.Player.GetModPlayer<TrainingHudFeedback>();var cell=feedback.cells[slot];
        if(cell.Kind!=TrainingCatalog.Id(s.Training.Slots[slot])||slot>=s.Training.Count(s.Player))return 0;
        return s.Training.EffectDuration(cell.Kind)>0?cell.Glow:cell.Pulsed?Pulse(Main.GameUpdateCount-cell.PulseAt):0;
    }
}

public sealed class TrainingHudFeedbackSystem : ModSystem {
    public override void PostUpdateEverything(){
        if(Main.dedServ||Main.gameMenu)return;
        var s=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();
        s.Player.GetModPlayer<TrainingHudFeedback>().Update(s);
    }
}
