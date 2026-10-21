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
public sealed class CompanionConfig : ModConfig {
    public override ConfigScope Mode => ConfigScope.ClientSide;
    [Header("Companion")]
    [DefaultValue(true)] public bool Enabled = true;
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
    public List<ItemDefinition> PickupBlacklist = new();

    [Header("Building")]
    [DefaultValue(true)] public bool Torches = true;
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

    [Header("Display")]
    [DefaultValue(true)] public bool CompanionHud = true;
    [DefaultValue(false)] public bool ShowActivityStatus;
    [DefaultValue(true)] public bool CompanionMarker = true;
    [DefaultValue(true)] public bool Discoveries = true;
    [DefaultValue(true)] public bool Chatter = true;
    [DefaultValue(true)] public bool Emotes = true;
    [DefaultValue(true)] public bool CatSounds = true;
    [DefaultValue(true)] public bool TrainingHints = true;

    [Header("Advanced")]
    [DefaultValue(false)] public bool RunInBackground;
    [DefaultValue(false)] public bool AdvancedOrders;

    public void Apply(CompanionPlayer s) {
        Systems.CompanionExclusions.Apply(this,s);
        s.Enabled=Enabled&&!s.Stowed;s.Pickup=Pickup;s.Combat=Combat;s.Torches=Torches;s.Bridges=Bridges;s.Ropes=Ropes;s.AutoLogistics=AutoLogistics;Systems.CompanionNetwork.Configure(s);
    }
    public override void OnChanged() {
        if(!Main.gameMenu && Main.LocalPlayer is { active:true } p) Apply(p.GetModPlayer<CompanionPlayer>());
    }
}
