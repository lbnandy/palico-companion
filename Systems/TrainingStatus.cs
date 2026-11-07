using System;
namespace PalicoCompanion.Systems;

internal static class TrainingStatus {
    internal static float Recharge(CompanionPlayer s,int slot){
        int kind=TrainingCatalog.Id(s.Training.Slots[slot]);if(!TrainingCatalog.Support(kind))return 1;
        int seconds=CompanionTraining.CooldownSeconds(kind,s.Training.Tier);
        int total=s.Training.SlotCooldownTotals[slot];
        // Older saves predate totals. Their skill timer is exact when it still
        // belongs to this item; transferred locks conservatively use 60 seconds.
        if(total<=0)total=s.Training.SlotCooldowns[slot]==s.Training.Cooldowns[kind]?60*seconds:3600;
        // An upgrade must not reset the visible progress of an existing cast.
        int familyTotal=s.Training.SlotCooldowns[slot]==s.Training.Cooldowns[kind]?total:60*seconds;
        float family=s.Training.Cooldowns[kind]/(float)Math.Max(1,familyTotal);
        float slotLock=s.Training.SlotCooldowns[slot]/(float)Math.Max(1,total);
        return Math.Clamp(1-Math.Max(family,slotLock),0,1);
    }
    internal static int Remaining(CompanionPlayer s,int slot){int kind=TrainingCatalog.Id(s.Training.Slots[slot]);return TrainingCatalog.Support(kind)?Math.Max(s.Training.Cooldowns[kind],s.Training.SlotCooldowns[slot]):0;}
    internal static int Effect(CompanionPlayer s,int kind)=>kind switch {0=>s.Training.SlowHealing,2=>s.Training.CleanseTime,3=>s.Training.ManaDance,4=>s.Training.Drum,_=>0};
    internal static string Describe(CompanionPlayer s,int slot){
        int kind=TrainingCatalog.Id(s.Training.Slots[slot]);if(kind<0)return "";
        if(!s.Training.Active(s)) {
            string reason=s.Stowed?CompanionText.Get("TrainingStatus.Stowed"):s.Vitals.Downed?CompanionText.Get("TrainingStatus.Recovering"):s.Paused?CompanionText.Get("TrainingStatus.Paused"):s.Player.dead?CompanionText.Get("TrainingStatus.OwnerDead"):!s.Enabled?CompanionText.Get("TrainingStatus.Disabled"):s.Cat?.ModProjectile is CompanionCat kiteCat&&kiteCat.Kite.Active?CompanionKite.Text("Status"):CompanionText.Get("TrainingStatus.Waiting");
            return CompanionText.Get("TrainingStatus.Inactive")+reason;
        }
        if(!TrainingCatalog.Support(kind))return CompanionText.Get("TrainingStatus.Passive");
        if(kind==TrainingCatalog.Barrel&&s.Cat?.ModProjectile is CompanionCat cat&&cat.Barrel.Active)return CompanionText.Get("CompanionBarrelCharge.Busy");
        int duration=Effect(s,kind),cooldown=Remaining(s,slot);
        string effect=kind switch {0=>CompanionText.Get("TrainingStatus.Healing"),2=>CompanionText.Get("TrainingStatus.DebuffImmunity"),3=>s.Training.ManaTarget==0?CompanionText.Get("TrainingStatus.PlayerMana"):CompanionText.Get("TrainingStatus.PalicoMana"),4=>CompanionText.Get("TrainingStatus.DrumBuff"),_=>""};
        if(duration>0)return CompanionText.Get("TrainingStatus.Duration" ,effect, (duration+59)/60)+(cooldown>0?CompanionText.Get("TrainingStatus.CooldownSuffix" ,(cooldown+59)/60):"");
        if(cooldown>0)return CompanionText.Get("TrainingStatus.CooldownRemaining" ,(cooldown+59)/60);
        if(s.CombatMode==CompanionCombatMode.Passive&&kind is 5 or 7 or TrainingCatalog.Barrel)return Terraria.Localization.Language.GetTextValue("Mods.PalicoCompanion.UI.CombatPassiveSkill");
        if(!s.Combat&&kind is 4 or 5 or 7 or TrainingCatalog.Barrel)return CompanionText.Get("TrainingStatus.CombatDisabled");
        return CompanionText.Get("TrainingStatus.Ready");
    }
}
