using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.Localization;

namespace PalicoCompanion.Systems;

public sealed partial class CompanionExpression {
    (string variant,int emote,int expires)? pendingGear;
    int[] equipmentBefore;
    readonly HashSet<int> equipmentSeen=new();
    int equipmentPending,equipmentReady,equipmentExpires,equipmentAt;
    string equipmentVariant;

    // Called only after an authoritative activation. Flush outside the temporary
    // native weapon owner scope so local speech still reaches the real owner.
    internal void GearUsed(CompanionPlayer s,int kind,bool self=false) {
        if(CompanionNetwork.Client||!ChatEnabled(s)&&!EmotesEnabled(s))return;
        string variant=kind switch {
            0=>"GearHoney",1=>self?"GearGuardSelf":"GearGuard",2=>"GearCleanse",
            3=>self?"GearBellSelf":"GearBell",4=>"GearDrum",5=>"GearDart",
            6=>"GearLift",7=>"GearBomb",_=>null
        };
        if(variant==null)return;
        bool urgent=CompanionSpeechPacing.For("Working",variant)==CompanionSpeechPacing.Priority.Support;
        if(pendingGear is {} pending&&tick<pending.expires&&
           CompanionSpeechPacing.For("Working",pending.variant)==CompanionSpeechPacing.Priority.Support&&!urgent)return;
        pendingGear=(variant,kind==0?EmoteID.ItemLifePotion:kind is 3 or 4?EmoteID.EmoteNote:EmoteID.EmoteHappiness,tick+240);
    }
    void SpeakGear(CompanionPlayer s,Projectile cat) {
        if(pendingGear is not {} pending)return;
        if(tick>=pending.expires||!ChatEnabled(s)&&!EmotesEnabled(s)){pendingGear=null;return;}
        if(TrySay(s,cat,"Working",pending.emote,variant:pending.variant))pendingGear=null;
    }

    internal void ObserveEquipment(CompanionPlayer s,Projectile cat) {
        if(!IsLocalListener(s)||CompanionNativeUse.Current!=null||CompanionFishing.Current!=null)return;
        int[] Current()=>new[]{s.Gear[0].type}.Concat(s.Training.Slots.Select(i=>i.type)).ToArray();
        if(s.PanelOpen) {
            equipmentBefore??=Current();equipmentPending=0;return;
        }
        if(equipmentBefore is {} before) {
            equipmentBefore=null;
            var after=Current();
            // Moving an item between slots, taking it off, or re-equipping an
            // already acknowledged item is not a new equipment conversation.
            int index=Array.FindIndex(after,type=>type>0&&!before.Contains(type)&&!equipmentSeen.Contains(type));
            if(index>=0&&tick>=equipmentAt) {
                equipmentPending=after[index];equipmentVariant=index==0?"GearEquippedWeapon":"GearEquippedSupport";
                equipmentReady=tick+60;equipmentExpires=tick+600;
            }
        }
        if(equipmentPending==0)return;
        if(tick>=equipmentExpires||!s.Enabled||s.Stowed||s.Paused||s.Player.dead||s.Vitals.Downed||!Current().Contains(equipmentPending)){equipmentPending=0;return;}
        if(tick<equipmentReady||!s.Introduced||Main.inFancyUI||!Main.mouseItem.IsAir||CompanionNetwork.Pending||CompanionThreat.WorkDanger(s.Player,cat.Center))return;
        if(TrySay(s,cat,"Working",EmoteID.EmoteHappiness,variant:equipmentVariant)) {
            equipmentSeen.Add(equipmentPending);equipmentPending=0;equipmentAt=tick+7200;
        }
    }

    internal bool TryHint(CompanionPlayer s,string key,string context) {
        if(!IsLocalListener(s)||!ChatEnabled(s)||!s.Introduced||tick<flavorAt||!pacing.Ready(tick,CompanionSpeechPacing.Priority.Routine))return false;
        Main.NewText(s.CatName+"："+Language.GetTextValue("Mods.PalicoCompanion.TrainingSpeech."+key,context),Color.SandyBrown);
        pacing.Spoken(tick,CompanionSpeechPacing.Priority.Routine);flavorAt=tick+1800;return true;
    }

    internal void ReceiveSpeech(CompanionPlayer s,string lineKey,string context) {
        if(!IsLocalListener(s)||!ChatEnabled(s))return;
        const string prefix="Mods.PalicoCompanion.Speech.";
        if(!lineKey.StartsWith(prefix,StringComparison.Ordinal))return;
        int end=lineKey.LastIndexOf('.');if(end<=prefix.Length)return;
        string pool=lineKey[prefix.Length..end],key=CompanionSpeechContext.Event(pool);
        string variant=pool.Length>key.Length?pool[key.Length..]:null;
        var priority=CompanionSpeechPacing.For(key,variant);
        // Server notices remain authoritative. Routine packets also respect
        // local discoveries, equipment changes and guide hints that just spoke.
        if(priority is CompanionSpeechPacing.Priority.Routine or CompanionSpeechPacing.Priority.Equipment && !pacing.Ready(tick,priority))return;
        s.Dialogue.Observe(lineKey);pacing.Spoken(tick,priority);flavorAt=tick+1800;
        string text=Language.GetTextValue(lineKey,CompanionMemories.Context(lineKey,context));
        s.NetSpeechCount++;s.NetLastSpeech=text;Main.NewText(s.CatName+"："+text,Color.SandyBrown);
    }
}
