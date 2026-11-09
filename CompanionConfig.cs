using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
namespace PalicoCompanion;
// Preserve the strings already stored in player config files while using the
// native tModLoader enum dropdown and independently localized option labels.
[JsonConverter(typeof(StringEnumConverter))]
public enum CoinDestinationMode {
    [EnumMember(Value="给主人"),LabelKey("$Mods.PalicoCompanion.CoinChoices.Owner")] Owner,
    [EnumMember(Value="存入小猪存钱罐"),LabelKey("$Mods.PalicoCompanion.CoinChoices.Bank")] Bank,
    [EnumMember(Value="留在呆猫背包"),LabelKey("$Mods.PalicoCompanion.CoinChoices.Bag")] Bag
}
[JsonConverter(typeof(StringEnumConverter))]
public enum CatCartMode {
    [LabelKey("$Mods.PalicoCompanion.CartChoices.Occasional")] Occasional,
    [LabelKey("$Mods.PalicoCompanion.CartChoices.EveryTime")] EveryTime,
    [LabelKey("$Mods.PalicoCompanion.CartChoices.Off")] Off
}
[JsonConverter(typeof(StringEnumConverter))]
public enum CompanionAttributeMode {
    [LabelKey("$Mods.PalicoCompanion.Equipment.Inherited")] Inherited,
    [LabelKey("$Mods.PalicoCompanion.Equipment.Independent")] Independent
}
[JsonConverter(typeof(StringEnumConverter))]
public enum PickupFilterMode {
    [LabelKey("$Mods.PalicoCompanion.PickupFilters.Blacklist")] Blacklist,
    [LabelKey("$Mods.PalicoCompanion.PickupFilters.Whitelist")] Whitelist,
    [LabelKey("$Mods.PalicoCompanion.PickupFilters.Coins")] Coins
}
[JsonConverter(typeof(StringEnumConverter))]
public enum CompanionLightingMode {
    [LabelKey("$Mods.PalicoCompanion.LightingChoices.Off")] Off,
    [LabelKey("$Mods.PalicoCompanion.LightingChoices.Held")] Held,
    [LabelKey("$Mods.PalicoCompanion.LightingChoices.Automatic")] Automatic
}
public sealed class CompanionConfig : ModConfig {
    public override ConfigScope Mode => ConfigScope.ClientSide;
    [Header("Companion")]
    [DefaultValue(true)] public bool Enabled = true;
    // Stored locally; the abilities page presents this personal choice.
    [JsonProperty("AttributeMode"),DefaultValue(CompanionAttributeMode.Inherited)]
    CompanionAttributeMode attributeMode=CompanionAttributeMode.Inherited;
    [JsonIgnore] public CompanionAttributeMode AttributeMode { get=>attributeMode; set=>attributeMode=value; }
    [DefaultValue(true)] public bool Combat = true;
    [DefaultValue(true)] public bool AutoHeal = true;

    [Header("Work")]
    [DefaultValue(true)] public bool DiggingAssist = true;
    [DefaultValue(true)] public bool MiningAssist = true;
    [DefaultValue(true)] public bool Forestry = true;
    [DefaultValue(true)] public bool Gardening = true;
    [DefaultValue(true)] public bool Pickup = true;
    [DefaultValue(true)] public bool CatchCritters = true;
    [DefaultValue(true)] public bool Fishing = true;
    public List<ItemDefinition> MiningBlacklist = new();
    [DefaultValue(PickupFilterMode.Blacklist),Dropdown] public PickupFilterMode PickupFilter=PickupFilterMode.Blacklist;
    public List<ItemDefinition> PickupBlacklist = new();
    public List<ItemDefinition> PickupWhitelist = new();

    [Header("Building")]
    [DefaultValue(CompanionLightingMode.Automatic),Dropdown] public CompanionLightingMode Lighting=CompanionLightingMode.Automatic;
    // Read the former switch without writing it back or showing a second control.
    bool? legacyTorches;
    [JsonProperty("Torches",DefaultValueHandling=DefaultValueHandling.Ignore)] bool? LegacyTorches { set=>legacyTorches=value; }
    [OnDeserialized] void RestoreLighting(StreamingContext context){
        if(legacyTorches is bool enabled)Lighting=enabled?CompanionLightingMode.Automatic:CompanionLightingMode.Off;
        legacyTorches=null;
    }
    [DefaultValue(false)] public bool Bridges;
    [DefaultValue(false)] public bool Ropes;

    [Header("Deliveries")]
    [DefaultValue(true)] public bool DeliverRecoveryDrops = true;
    [DefaultValue(true)] public bool ShareNewPickups=true;
    [DefaultValue(CoinDestinationMode.Owner),Dropdown] public CoinDestinationMode CoinDestination=CoinDestinationMode.Owner;

    [Header("Restocking")]
    [DefaultValue(true)] public bool AutoLogistics = true;
    [DefaultValue(30),Range(0,999)] public int StockTorches=30;
    [DefaultValue(20),Range(0,999)] public int StockGlowsticks=20;
    [DefaultValue(30),Range(0,999)] public int StockPlatforms=30;
    [DefaultValue(30),Range(0,999)] public int StockRopes=30;
    [DefaultValue(10),Range(0,99)] public int StockHealing=10;
    [DefaultValue(10),Range(0,99)] public int StockMana=10;
    [DefaultValue(200),Range(0,9999)] public int StockAmmo=200;
    [DefaultValue(20),Range(0,999)] public int StockAcorns=20;
    [DefaultValue(20),Range(0,999)] public int StockBait=20;

    [Header("Appearance")]
    [DefaultValue(true)] public bool BackWeapon = true;
    [DefaultValue(false)] public bool DyeClothesOnly;
    [DefaultValue(true)] public bool DyeKite = true;

    [Header("Display")]
    [DefaultValue(true)] public bool CompanionHud = true;
    // Fraction of the available UI area; null retains automatic buff avoidance.
    [CustomModConfigItem(typeof(Systems.CompanionHudPositionElement))]
    public Microsoft.Xna.Framework.Vector2? HudPosition;
    [DefaultValue(false)] public bool ShowActivityStatus;
    [DefaultValue(true)] public bool CompanionMarker = true;
    [DefaultValue(true)] public bool Discoveries = true;
    [DefaultValue(true)] public bool Chatter = true;
    [DefaultValue(true)] public bool Emotes = true;
    [DefaultValue(true)] public bool CatSounds = true;
    [DefaultValue(true)] public bool TeleportSounds = true;
    [DefaultValue(CatCartMode.EveryTime),Dropdown] public CatCartMode CatCart = CatCartMode.EveryTime;
    [DefaultValue(true)] public bool TrainingHints = true;

    [Header("Advanced")]
    [DefaultValue(false)] public bool RunInBackground;
    [DefaultValue(false)] public bool AdvancedOrders;

    public void Apply(CompanionPlayer s) {
        Systems.CompanionExclusions.Apply(this,s);
        if(!Systems.CompanionNetwork.Client)Systems.CompanionEquipment.SetMode(s,AttributeMode==CompanionAttributeMode.Independent);
        s.Enabled=Enabled&&!s.Stowed;s.Pickup=Pickup;s.Combat=Combat;s.Lighting=Systems.CompanionLighting.Valid((byte)Lighting);s.Bridges=Bridges;s.Ropes=Ropes;s.AutoLogistics=AutoLogistics;Systems.CompanionNetwork.Configure(s);
    }
    bool savingDisplay;
    internal void SelectAttributeMode(CompanionAttributeMode mode) {
        if(AttributeMode==mode)return;
        var pending=(CompanionConfig)Clone();pending.AttributeMode=mode;
        SaveChanges(pending);
    }
    internal void SaveDisplayChanges() {
        // HUD shortcuts save locally without reapplying gameplay preferences.
        try { savingDisplay=true;SaveChanges(); }
        finally { savingDisplay=false; }
    }
    public override void OnChanged() {
        if(!savingDisplay && !Main.gameMenu && Main.LocalPlayer is { active:true } p) Apply(p.GetModPlayer<CompanionPlayer>());
    }
}
