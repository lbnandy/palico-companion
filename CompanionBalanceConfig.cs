using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
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

    // Presentation only: personal mode must never enter the server config.
    [Header("Attributes"),JsonIgnore,ShowDespiteJsonIgnore,CustomModConfigItem(typeof(Systems.CompanionAttributeModeElement))]
    public bool PersonalMode { get; set; }

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
    [DefaultValue(100),Range(50,150),Increment(5),Slider] public int TargetingRangePercent { get; set; } = 100;

    [Header("SharedBuffs")]
    [DefaultValue(true)] public bool SharePotionBuffs { get; set; } = true;

    [Header("Inheritance")]
    [DefaultValue(100),Range(0,100),Increment(5),Slider]
    [JsonProperty(DefaultValueHandling=DefaultValueHandling.Include)]
    public int CombatInheritancePercent { get=>combatInheritancePercent; set {combatInheritancePercent=value;inheritanceSpecified=true;} }
    [DefaultValue(75),Range(0,200),Increment(5),Slider] public int DefensePercent { get; set; } = 75;
    [DefaultValue(100),Range(0,100),Increment(5),Slider] public int DamageReductionInheritancePercent { get; set; } = 100;
    [DefaultValue(50),Range(0,100),Increment(5),Slider] public int RegenInheritancePercent { get; set; } = 50;

    [Header("Movement")]
    [DefaultValue(75),Range(25,150),Increment(5),Slider] public int CombatMovementPercent { get; set; } = 75;
    [DefaultValue(100),Range(25,150),Increment(5),Slider] public int TravelMovementPercent { get; set; } = 100;
    [DefaultValue(CompanionEvasionMode.Standard),Dropdown] public CompanionEvasionMode Evasion { get; set; } = CompanionEvasionMode.Standard;

    [Header("WorkEfficiency")]
    [DefaultValue(100),Range(25,200),Increment(5),Slider] public int WorkSpeedPercent { get; set; } = 100;
    [DefaultValue(100),Range(25,200),Increment(5),Slider] public int FishingSpeedPercent { get; set; } = 100;

    [Header("BossHealth")]
    [DefaultValue(false)] public bool BossHealthBoost { get; set; }
    [DefaultValue(35),Range(0,100),Increment(5),Slider] public int BossHealthPercent { get; set; } = 35;

    internal float WorkScale => Math.Clamp(WorkSpeedPercent,25,200)/100f;
    internal float CombatInheritanceScale => Math.Clamp(CombatInheritancePercent,0,100)/100f;
    internal float FishingScale => Math.Clamp(FishingSpeedPercent,25,200)/100f;
    internal float TargetingRangeScale => Math.Clamp(TargetingRangePercent,50,150)/100f;

    internal static CompanionBalanceConfig Current => ModContent.GetInstance<CompanionBalanceConfig>();
    internal float DamageScale => Math.Clamp(DamagePercent,10,200)/100f;
    internal int HealthRatio => Math.Clamp(HealthPercent,10,200);
    internal int DefenseRatio => Math.Clamp(DefensePercent,0,200);
    internal float DamageReductionInheritanceScale => Math.Clamp(DamageReductionInheritancePercent,0,100)/100f;
    internal int RegenInheritanceRatio => Math.Clamp(RegenInheritancePercent,0,100);
    internal float MovementScale(bool combat) => Math.Clamp(combat?CombatMovementPercent:TravelMovementPercent,25,150)/100f;
    internal CompanionEvasionMode EvasionStyle => Enum.IsDefined(Evasion)?Evasion:CompanionEvasionMode.Standard;

    int combatInheritancePercent=100;
    bool inheritanceSpecified;
    bool? legacyInheritance;
    // Read the former checkbox without exposing or writing an obsolete setting.
    [JsonProperty("InheritCombatStats",DefaultValueHandling=DefaultValueHandling.Include)]
    bool? LegacyInheritance { set=>legacyInheritance=value; }
    [OnDeserializing] void BeginRead(StreamingContext context){combatInheritancePercent=100;inheritanceSpecified=false;legacyInheritance=null;}
    [OnDeserialized] void EndRead(StreamingContext context){
        if(!inheritanceSpecified&&legacyInheritance.HasValue)CombatInheritancePercent=legacyInheritance.Value?100:0;
        legacyInheritance=null;
    }
    public override void OnLoaded(){
        // tML omitted false/default values in old files. Only migrate a local
        // existing file here; resetting defaults and server config sync must
        // not consult or be overridden by the client's old local choices.
        if(inheritanceSpecified)return;
        string path=Path.Combine(ConfigManager.ModConfigPath,$"{Mod.Name}_{Name}.json");
        if(File.Exists(path)&&JObject.Parse(File.ReadAllText(path))[nameof(CombatInheritancePercent)]==null)CombatInheritancePercent=0;
    }

    public override bool AcceptClientChanges(ModConfig pendingConfig,int whoAmI,ref NetworkText message) {
        if(Main.netMode==Terraria.ID.NetmodeID.SinglePlayer)return true;
        if(whoAmI>=0&&whoAmI<Main.maxPlayers&&NetMessage.DoesPlayerSlotCountAsAHost(whoAmI))return true;
        message=NetworkText.FromKey("Mods.PalicoCompanion.BalanceHostOnly");
        return false;
    }
}
