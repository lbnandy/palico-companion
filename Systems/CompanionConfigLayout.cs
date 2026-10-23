using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace PalicoCompanion.Systems;

// Add inline icons to our native config rows without changing the editor layout.
public sealed class CompanionConfigLayout : ModSystem {
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Dictionary<string,string> OptionIcons=new(){
        ["Enabled"]="head",["Combat"]="GoldBroadsword",["AutoHeal"]="HealingPotion",
        ["DiggingAssist"]="GoldPickaxe",["MiningAssist"]="IronOre",["Forestry"]="GoldAxe",["Gardening"]="StaffofRegrowth",
        ["Pickup"]="TreasureMagnet",["CatchCritters"]="BugNet",["Fishing"]="WoodFishingPole",["MiningBlacklist"]="StoneBlock",["PickupBlacklist"]="TrashCan",
        ["Torches"]="Torch",["Bridges"]="WoodPlatform",["Ropes"]="Rope",
        ["DeliverRecoveryDrops"]="Heart",["ShareNewPickups"]="TreasureMagnet",["CoinDestination"]="GoldCoin",
        ["AutoLogistics"]="Chest",["StockTorches"]="Torch",["StockGlowsticks"]="Glowstick",["StockPlatforms"]="WoodPlatform",["StockRopes"]="Rope",
        ["StockHealing"]="HealingPotion",["StockMana"]="ManaPotion",["StockAmmo"]="WoodenArrow",["StockAcorns"]="Acorn",["StockBait"]="Worm",
        ["CompanionHud"]="head",["ShowActivityStatus"]="Stopwatch",["CompanionMarker"]="Compass",["Discoveries"]="MetalDetector",
        ["Chatter"]="Sign",["Emotes"]="WhoopieCushion",["CatSounds"]="Bell",["TrainingHints"]="Book",
        ["RunInBackground"]="Stopwatch",["AdvancedOrders"]="Ruler",
        ["Preset"]="head",["DamagePercent"]="GoldBroadsword",["HealthPercent"]="LifeCrystal",["DefensePercent"]="IronChainmail",
        ["CombatMovementPercent"]="HermesBoots",["TravelMovementPercent"]="Aglet",["Evasion"]="MasterNinjaGear",["WorkSpeedPercent"]="GoldPickaxe",["FishingSpeedPercent"]="WoodFishingPole"
    };
    readonly Dictionary<string, FieldInfo> fields = new();
    readonly List<Action> restore = new();
    T Get<T>(object instance, string name) => (T)fields[name].GetValue(instance);

    public override void Load() {
        if (Main.dedServ) return;
        var editorType = typeof(ModConfig).Assembly.GetType("Terraria.ModLoader.Config.UI.UIModConfig", true);
        foreach (string name in new[] { "modConfig", "pendingConfig", "mainConfigItems", "filterTextField", "headerTextPanel", "backButton", "saveConfigButton", "revertConfigButton", "restoreDefaultsConfigButton" })
            fields[name] = editorType.GetField(name, Private);
        MonoModHooks.Add(editorType.GetMethod("OnActivate"), (Action<Action<UIState>, UIState>)Activate);
    }

    public override void Unload() { Restore(); fields.Clear(); }

    void Restore() {
        foreach (var reset in restore) reset();
        restore.Clear();
    }

    void Activate(Action<UIState> orig, UIState self) {
        Restore();
        orig(self);
        var config=Get<ModConfig>(self,"modConfig");
        if (config is not CompanionConfig && config is not CompanionBalanceConfig) return;
        Get<UITextPanel<string>>(self, "headerTextPanel").SetText(config.DisplayName.Value);
        // These controls belong to a shared singleton. Refresh our captions when
        // the language changes, then restore them before opening another config.
        foreach (var button in new[] { ("backButton", "Back"), ("saveConfigButton", "SaveConfig"), ("revertConfigButton", "RevertChanges"), ("restoreDefaultsConfigButton", "RestoreDefaults") }) {
            var control = Get<UITextPanel<string>>(self, button.Item1);
            string old = control.Text;
            restore.Add(() => control.SetText(old));
            control.SetText(Language.GetTextValue("tModLoader.ModConfig" + button.Item2));
        }
        var filter = Get<object>(self, "filterTextField");
        var hint = filter.GetType().GetField("_hintText", Private);
        string oldHint = (string)hint.GetValue(filter);
        restore.Add(() => hint.SetValue(filter, oldHint));
        hint.SetValue(filter, Language.GetTextValue("tModLoader.ModConfigFilterOptions"));

        var member = typeof(ConfigElement).GetProperty("MemberInfo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var display = typeof(ConfigElement).GetProperty("TextDisplayFunction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var pair in Get<List<Tuple<UIElement, UIElement>>>(self, "mainConfigItems")) {
            if (pair.Item2 is not ConfigElement control) continue;
            var field = (PropertyFieldWrapper)member.GetValue(control);
            if(config is CompanionBalanceConfig && field.Name==nameof(CompanionBalanceConfig.Evasion))
                SyncBalanceChoice(control,field,Get<ModConfig>(self,"pendingConfig"));
            if (!OptionIcons.TryGetValue(field.Name, out string icon)) continue;
            var original = (Func<string>)display.GetValue(control);
            string tag = icon == "head" ? "[palicohead:icon] " : $"[palicoitem:{icon}] ";
            display.SetValue(control, (Func<string>)(() => tag + original()));
        }
    }

    static void SyncBalanceChoice(ConfigElement control,PropertyFieldWrapper field,ModConfig pending) {
        var choice=control.Children.OfType<UIAutoScaleTextTextPanel<string>>().FirstOrDefault();
        if(choice==null)return;
        object previous=null;
        // A preset can change evasion without clicking its native dropdown.
        control.OnUpdate+=_=>{
            object current=field.GetValue(pending);
            if(!Equals(current,previous)){
                previous=current;
                string key="Mods.PalicoCompanion.EvasionChoices."+current;
                choice.SetText(Language.GetTextValue(Language.Exists(key)?key:"tModLoader.ModConfigUnknownEnum"));
            }
        };
    }
}
