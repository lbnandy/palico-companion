using Terraria.Localization;

namespace PalicoCompanion.Systems;

// Both panels read the same authoritative snapshot; drawing never updates resources.
internal readonly record struct CompanionResourceDisplay(string Label,string Value,string Hint,bool Low) {
    internal static CompanionResourceDisplay Read(CompanionPlayer state){
        string Text(string key)=>Language.GetTextValue("Mods.PalicoCompanion.UI."+key);
        if(CompanionCrossMod.UsesInspiration?.Invoke(state.Gear[0])==true){
            bool ready=state.Stats.Available(state)&&state.Stats.InspirationMax>0;
            return new(Language.GetTextValue("Mods.PalicoCompanion.Attributes.Inspiration"),
                ready?$"{state.Stats.Inspiration} / {state.Stats.InspirationMax}":"—",
                Language.GetTextValue("Mods.PalicoCompanion.Attributes.InspirationHint"),
                ready&&state.Stats.Inspiration<=state.Stats.InspirationMax/4);
        }
        return new(Text("ManaLabel"),$"{state.Vitals.Mana} / {state.Vitals.MaxMana}",
            CompanionEquipment.Independent(state)?Language.GetTextValue("Mods.PalicoCompanion.Equipment.ManaHint"):Text("ManaHint"),
            state.Vitals.Mana<=state.Vitals.MaxMana/4);
    }
}
