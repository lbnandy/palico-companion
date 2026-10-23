using System;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace PalicoCompanion;

[JsonConverter(typeof(StringEnumConverter))]
public enum CompanionEvasionMode {
    [LabelKey("$Mods.PalicoCompanion.EvasionChoices.Off")] Off,
    [LabelKey("$Mods.PalicoCompanion.EvasionChoices.Standard")] Standard,
    [LabelKey("$Mods.PalicoCompanion.EvasionChoices.Agile")] Agile
}

public enum CompanionStrengthPreset {
    [LabelKey("$Mods.PalicoCompanion.StrengthPresets.Assist.Label"),TooltipKey("$Mods.PalicoCompanion.StrengthPresets.Assist.Tooltip")] Assist,
    [LabelKey("$Mods.PalicoCompanion.StrengthPresets.Standard.Label"),TooltipKey("$Mods.PalicoCompanion.StrengthPresets.Standard.Tooltip")] Standard,
    [LabelKey("$Mods.PalicoCompanion.StrengthPresets.Strong.Label"),TooltipKey("$Mods.PalicoCompanion.StrengthPresets.Strong.Tooltip")] Strong,
    [LabelKey("$Mods.PalicoCompanion.StrengthPresets.Custom.Label"),TooltipKey("$Mods.PalicoCompanion.StrengthPresets.Custom.Tooltip")] Custom
}

public sealed class CompanionBalanceConfig : ModConfig {
    public override ConfigScope Mode => ConfigScope.ServerSide;

    // Only the underlying values are saved and synchronized. Loading a config
    // must never reapply a preset over the player's individual adjustments.
    [Header("Presets"),JsonIgnore,ShowDespiteJsonIgnore,CustomModConfigItem(typeof(Systems.CompanionPresetElement))]
    public CompanionStrengthPreset Preset {
        get => (DamagePercent,HealthPercent,DefensePercent,CombatMovementPercent,TravelMovementPercent,Evasion) switch {
            (50,75,75,75,100,CompanionEvasionMode.Standard) => CompanionStrengthPreset.Assist,
            (75,75,75,75,100,CompanionEvasionMode.Standard) => CompanionStrengthPreset.Standard,
            (100,100,100,100,100,CompanionEvasionMode.Agile) => CompanionStrengthPreset.Strong,
            _ => CompanionStrengthPreset.Custom
        };
        set {
            if(value is not (CompanionStrengthPreset.Assist or CompanionStrengthPreset.Standard or CompanionStrengthPreset.Strong))return;
            bool strong=value==CompanionStrengthPreset.Strong;
            DamagePercent=value==CompanionStrengthPreset.Assist?50:strong?100:75;
            HealthPercent=DefensePercent=CombatMovementPercent=strong?100:75;
            TravelMovementPercent=100;
            Evasion=strong?CompanionEvasionMode.Agile:CompanionEvasionMode.Standard;
        }
    }

    [Header("Strength")]
    [DefaultValue(75),Range(10,200),Increment(5),Slider] public int DamagePercent { get; set; } = 75;
    [DefaultValue(75),Range(10,200),Increment(5),Slider] public int HealthPercent { get; set; } = 75;
    [DefaultValue(75),Range(0,200),Increment(5),Slider] public int DefensePercent { get; set; } = 75;

    [Header("Movement")]
    [DefaultValue(75),Range(25,150),Increment(5),Slider] public int CombatMovementPercent { get; set; } = 75;
    [DefaultValue(100),Range(25,150),Increment(5),Slider] public int TravelMovementPercent { get; set; } = 100;
    [DefaultValue(CompanionEvasionMode.Standard),Dropdown] public CompanionEvasionMode Evasion { get; set; } = CompanionEvasionMode.Standard;

    internal static CompanionBalanceConfig Current => ModContent.GetInstance<CompanionBalanceConfig>();
    internal float DamageScale => Math.Clamp(DamagePercent,10,200)/100f;
    internal int HealthRatio => Math.Clamp(HealthPercent,10,200);
    internal int DefenseRatio => Math.Clamp(DefensePercent,0,200);
    internal float MovementScale(bool combat) => Math.Clamp(combat?CombatMovementPercent:TravelMovementPercent,25,150)/100f;
    internal CompanionEvasionMode EvasionStyle => Enum.IsDefined(Evasion)?Evasion:CompanionEvasionMode.Standard;

    public override bool AcceptClientChanges(ModConfig pendingConfig,int whoAmI,ref NetworkText message) {
        if(Main.netMode==Terraria.ID.NetmodeID.SinglePlayer)return true;
        if(whoAmI>=0&&whoAmI<Main.maxPlayers&&NetMessage.DoesPlayerSlotCountAsAHost(whoAmI))return true;
        message=NetworkText.FromKey("Mods.PalicoCompanion.BalanceHostOnly");
        return false;
    }
}
