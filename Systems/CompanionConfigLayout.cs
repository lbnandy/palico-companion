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
        ["Chatter"]="Sign",["Emotes"]="WhoopieCushion",["CatSounds"]="Bell",["TeleportSounds"]="MagicMirror",["CatCart"]="Minecart",["TrainingHints"]="Book",
        ["DyeClothesOnly"]="BlueDye",["DyeKite"]="KiteBlue",
        ["RunInBackground"]="Stopwatch",["AdvancedOrders"]="Ruler",
        ["AttributeMode"]="IronChainmail",["Preset"]="head",["CombatInheritancePercent"]="AvengerEmblem",["DamagePercent"]="GoldBroadsword",["HealthPercent"]="LifeCrystal",["DefensePercent"]="IronChainmail",["RegenInheritancePercent"]="BandofRegeneration",["TargetingRangePercent"]="Binoculars",
        ["CombatMovementPercent"]="HermesBoots",["TravelMovementPercent"]="Aglet",["Evasion"]="MasterNinjaGear",["WorkSpeedPercent"]="GoldPickaxe",["FishingSpeedPercent"]="WoodFishingPole",
        ["BossHealthBoost"]="SuspiciousLookingEye",["BossHealthPercent"]="LifeCrystal"
    };
    readonly Dictionary<string, FieldInfo> fields = new();
    readonly List<Action> restore = new();
    T Get<T>(object instance, string name) => (T)fields[name].GetValue(instance);

    public override void Load() {
        if (Main.dedServ) return;
        var editorType = typeof(ModConfig).Assembly.GetType("Terraria.ModLoader.Config.UI.UIModConfig", true);
        foreach (string name in new[] { "modConfig", "pendingConfig", "mainConfigItems", "filterTextField", "headerTextPanel", "updateNeeded" })
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
        // Native buttons own their LocalizedText and refresh with the language.
        // Only the shared filter still holds a plain string to refresh/restore.
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
            var pending=Get<ModConfig>(self,"pendingConfig");
            Func<string> unavailable=field.Name switch {
                "StockTorches" or "StockGlowsticks" or "StockPlatforms" or "StockRopes" or "StockHealing" or "StockMana" or "StockAmmo" or "StockAcorns" or "StockBait"
                    when pending is CompanionConfig personal=>()=>personal.AutoLogistics?null:"EnableRestocking",
                "BossHealthPercent" when pending is CompanionBalanceConfig balance=>()=>balance.BossHealthBoost?null:"EnableBossHealth",
                _=>null
            };
            if(unavailable!=null)pair.Item1.Append(new CompanionSettingDependency(control,unavailable));
            if(config is CompanionBalanceConfig && field.Name==nameof(CompanionBalanceConfig.Evasion))
                SyncBalanceChoice(control,field,Get<ModConfig>(self,"pendingConfig"));
            if (!OptionIcons.TryGetValue(field.Name, out string icon)) continue;
            var original = (Func<string>)display.GetValue(control);
            string tag = icon == "head" ? "[palicohead:icon] " : $"[palicoitem:{icon}] ";
            display.SetValue(control, (Func<string>)(() => tag + original()));
        }
        if(config is CompanionBalanceConfig) BindInheritanceVisibility(self,member);
    }

    void BindInheritanceVisibility(UIState editor,PropertyInfo member) {
        var items=Get<List<Tuple<UIElement,UIElement>>>(editor,"mainConfigItems");
        var all=items.ToArray();
        var inheritance=new HashSet<Tuple<UIElement,UIElement>>(all.Where(pair=>
            pair.Item2 is ConfigElement control && ((PropertyFieldWrapper)member.GetValue(control)).Name is
                nameof(CompanionBalanceConfig.CombatInheritancePercent) or nameof(CompanionBalanceConfig.DefensePercent) or nameof(CompanionBalanceConfig.RegenInheritancePercent)));
        int first=Array.FindIndex(all,inheritance.Contains);
        if(first>0 && all[first-1].Item2.GetType().Name=="HeaderElement") {
            var pair=all[first-1];
            if(Main.netMode==Terraria.ID.NetmodeID.MultiplayerClient) {
                // Recreate only the native header, whose text is immutable.
                var header=(UIElement)Activator.CreateInstance(pair.Item2.GetType(),Language.GetTextValue("Mods.PalicoCompanion.Settings.SharedInheritance"));
                pair.Item1.RemoveChild(pair.Item2);pair.Item1.Append(header);
                header.Recalculate();
                pair.Item1.Height.Set(header.GetOuterDimensions().Height,0);
                all[first-1]=Tuple.Create(pair.Item1,header);
                var tooltip=typeof(ConfigElement).GetProperty("TooltipFunction",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                foreach(var row in inheritance) {
                    var original=(Func<string>)tooltip.GetValue(row.Item2);
                    tooltip.SetValue(row.Item2,(Func<string>)(()=>Language.GetTextValue("Mods.PalicoCompanion.Settings.SharedInheritanceHelp")+"\n"+original()));
                }
            }
            inheritance.Add(all[first-1]);
        }
        bool? previous=null;
        void Refresh(UIElement _) {
            // Multiplayer values belong to the whole server, including players
            // using the other mode. Only personal single-player settings disappear.
            bool visible=Main.netMode==Terraria.ID.NetmodeID.MultiplayerClient ||
                ModContent.GetInstance<CompanionConfig>().AttributeMode==CompanionAttributeMode.Inherited;
            if(previous==visible)return;
            previous=visible;
            items.Clear();
            items.AddRange(all.Where(pair=>visible||!inheritance.Contains(pair)));
            // Reuse the native filter and layout, keeping the same pending controls
            // so switching modes cannot discard unsaved slider values.
            fields["updateNeeded"].SetValue(editor,true);
        }
        editor.OnUpdate+=Refresh;
        restore.Add(()=>editor.OnUpdate-=Refresh);
        Refresh(editor);
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
