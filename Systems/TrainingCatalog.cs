using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Localization;
using Terraria.ObjectData;

namespace PalicoCompanion.Systems;

internal record TrainingDefinition(string Key,int Icon,params int[] Materials) {
    internal string Name=>Language.GetTextValue("Mods.PalicoCompanion.TrainingNames."+Key);
    internal string Detail=>CompanionText.Get("Catalog."+Key);
}
internal static class TrainingCatalog {
    internal static readonly TrainingDefinition[] Entries = [
        new("HoneyCage",ItemID.HoneyComb,ItemID.BottledHoney,3,ItemID.JungleSpores,6,ItemID.Silk,4),
        new("GuardShield",ItemID.PaladinsShield,-1,8,ItemID.Wood,20,ItemID.Chain,2),
        new("CleansingPot",ItemID.BottledWater,ItemID.ClayBlock,15,ItemID.Daybloom,3,ItemID.BottledWater,3,ItemID.PurificationPowder,10),
        new("CheerBell",ItemID.Bell,-2,4,ItemID.FallenStar,3,ItemID.Silk,4),
        new("BattleDrum",ItemID.DrumSet,ItemID.DemoniteBar,4,ItemID.ShadowScale,3,ItemID.Wood,20),
        new("PiercingBoomerang",ItemID.BoneGlove,ItemID.Bone,20,-2,6),
        new("UpdraftBag",ItemID.CloudinaBottle,ItemID.Feather,5,ItemID.Cloud,20,ItemID.Silk,5),
        new("BombPot",ItemID.Grenade,ItemID.Grenade,10,-1,6,ItemID.Gel,20),
        new("PlunderKnife",ItemID.ThrowingKnife,ItemID.Bone,20,-3,4,ItemID.Silk,4),
        new("MiningKit",ItemID.MiningHelmet,-1,8,ItemID.Rope,50,ItemID.Torch,20),
        new("GardenKit",ItemID.StaffofRegrowth,ItemID.Wood,20,ItemID.Silk,6,ItemID.Daybloom,3),
        null, // Retired catalog position; not registered as content.
        null, // Retired catalog position; not registered as content.
        new("ExpandedBelt",ItemID.Toolbelt,ItemID.Silk,8,-1,5,ItemID.ShadowScale,6),
        new("ImprovedTools",ItemID.Toolbox,ItemID.HallowedBar,6,ItemID.SoulofLight,3,ItemID.SoulofNight,3),
        new("MasterTools",ItemID.TinkerersWorkshop,ItemID.ChlorophyteBar,4,ItemID.Ectoplasm,3,ItemID.Cog,10),
        null, // Retired catalog position; not registered as content.
        null, // Retired catalog position; not registered as content.
        null, // Retired catalog position; not registered as content.
        new("CatWorkbench",ItemID.WorkBench,ItemID.Wood,20,-1,3)
    ];
    internal static bool Current(int id)=>id>=0&&id<Entries.Length&&Entries[id]!=null;
    internal static int Type(int id)=>ModContent.GetInstance<PalicoCompanion>().Find<ModItem>(Entries[id].Key).Type;
    internal static string TierName(int tier)=>new[]{CompanionText.Get("Catalog.Basic"),CompanionText.Get("Catalog.Improved"),CompanionText.Get("Catalog.Masterwork")}[Math.Clamp(tier,0,2)];
    internal static int Id(Item item)=>item?.ModItem is TrainingItem t?t.Kind:-1;
    internal static string Group(int n)=>n switch {-1=>"IronBar",-2=>"PalicoCompanion:Silver",-3=>"PalicoCompanion:Gold",_=>""};
    internal static bool Matches(int key,Item i)=>key switch {-1=>i.type is ItemID.IronBar or ItemID.LeadBar,-2=>i.type is ItemID.SilverBar or ItemID.TungstenBar,-3=>i.type is ItemID.GoldBar or ItemID.PlatinumBar,_=>i.type==key};
    internal static string MaterialName(int key)=>key switch {-1=>CompanionText.Get("Catalog.IronBars"),-2=>CompanionText.Get("Catalog.SilverBars"),-3=>CompanionText.Get("Catalog.GoldBars"),_=>Lang.GetItemNameValue(key)};
}

public sealed class TrainingContent:ModSystem {
    public override void Load(){for(int i=0;i<TrainingCatalog.Entries.Length;i++)if(TrainingCatalog.Current(i))Mod.AddContent(new TrainingItem(i));}
    public override void AddRecipeGroups(){RecipeGroup.RegisterGroup("PalicoCompanion:Silver",new RecipeGroup(()=>CompanionText.Get("Catalog.SilverBars"),ItemID.SilverBar,ItemID.TungstenBar));RecipeGroup.RegisterGroup("PalicoCompanion:Gold",new RecipeGroup(()=>CompanionText.Get("Catalog.GoldBars"),ItemID.GoldBar,ItemID.PlatinumBar));}
}

[Autoload(false)]
public sealed class TrainingItem:ModItem {
    protected override bool CloneNewInstances=>true;
    public readonly int Kind;
    // Legacy save fields: only used to refund an already purchased component.
    public bool Installed,Alternate;
    public TrainingItem(int kind){Kind=kind;}
    public override string Name=>TrainingCatalog.Entries[Kind].Key;
    public override string Texture=>Kind < 11 || Kind is >=13 and <=15 or 19 ? "PalicoCompanion/Assets/Training/"+Name : "Terraria/Images/Item_"+TrainingCatalog.Entries[Kind].Icon;
    public override LocalizedText DisplayName=>Language.GetOrRegister("Mods.PalicoCompanion.TrainingNames."+Name);
    public override void SetDefaults(){Item.width=24;Item.height=24;Item.maxStack=Kind<13?1:9999;Item.rare=ItemRarityID.Blue;Item.value=Item.buyPrice(silver:10);if(Kind is >=13 and <=15){Item.useStyle=ItemUseStyleID.HoldUp;Item.useTime=Item.useAnimation=30;Item.UseSound=SoundID.Item4;Item.consumable=true;}if(Kind==19){Item.DefaultToPlaceableTile(ModContent.TileType<CatWorkbenchTile>());Item.maxStack=9999;}}
    public override void ModifyTooltips(List<TooltipLine> lines){
        int tier=Main.gameMenu?0:Main.LocalPlayer.GetModPlayer<CompanionPlayer>().Training.Tier;
        if(Kind<11)lines.Add(new(Mod,"Kind",CompanionText.Get(Kind<8?"Catalog.SupportGear":"Catalog.WorkGear")+(Main.gameMenu?"":" · "+TrainingCatalog.TierName(tier))));
        foreach(var line in TooltipLines(TrainingGuideCopy.Tooltip(Kind,tier,Main.gameMenu)))lines.Add(new(Mod,"Effect",line));
        if(Kind<11&&!Main.gameMenu){var state=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();for(int i=0;i<4;i++)if(IdAt(state,i)==Kind){lines.Add(new(Mod,"SkillStatus",TrainingStatus.Describe(state,i)));break;}}
        if(Installed)lines.Add(new(Mod,"Legacy",CompanionText.Get("Catalog.Legacy")));
    }
    internal static IEnumerable<string> TooltipLines(string text){
        foreach(string sentence in text.Split(new[]{'。','\n'},StringSplitOptions.RemoveEmptyEntries)){
            foreach(var row in CompanionCopy.Wrap(sentence,Math.Clamp(Main.screenWidth-100,300,640)))if(!string.IsNullOrWhiteSpace(row))yield return row;
        }
    }
    static int IdAt(CompanionPlayer s,int slot)=>TrainingCatalog.Id(s.Training.Slots[slot]);
    public override bool CanUseItem(Player player)=>Kind is <13 or >15 || player.GetModPlayer<CompanionPlayer>().Training.CanUpgrade(Kind)&&!CompanionNetwork.Pending&&(!CompanionNetwork.Client||player.GetModPlayer<CompanionPlayer>().NetReady);
    public override bool ConsumeItem(Player player)=>Kind is <13 or >15;
    public override bool? UseItem(Player player){
        if(Kind is <13 or >15)return null;
        if(player.whoAmI!=Main.myPlayer)return false;
        var s=player.GetModPlayer<CompanionPlayer>();if(!CanUseItem(player))return false;
        if(CompanionNetwork.Client)CompanionNetwork.UseTrainingItem(s,Item);
        else if(s.Training.Apply(Item,0))UpgradeFeedback(s,Kind);
        return true;
    }
    internal static void UpgradeFeedback(CompanionPlayer s,int kind){
        string text=kind==13?CompanionText.Get("Catalog.SupportSlotAdded"):kind==14?CompanionText.Get("Catalog.ImprovedUpgrade"):CompanionText.Get("Catalog.MasterworkUpgrade");
        CombatText.NewText(s.Player.Hitbox,Color.LightGreen,text,true);
        s.Report(text);
    }
    public override void SaveData(TagCompound tag){tag["installed"]=Installed;tag["alternate"]=Alternate;}
    public override void LoadData(TagCompound tag){Installed=tag.GetBool("installed");Alternate=Installed&&tag.GetBool("alternate");}
    public override void NetSend(BinaryWriter w){w.Write(Installed);w.Write(Alternate);}
    public override void NetReceive(BinaryReader r){Installed=r.ReadBoolean();bool alternate=r.ReadBoolean();Alternate=Installed&&alternate;}
    public override void AddRecipes(){
        Register(TrainingCatalog.Entries[Kind].Materials);
        if(Kind==4)Register([ItemID.CrimtaneBar,4,ItemID.TissueSample,3,ItemID.Wood,20]);
        if(Kind==13){Register([ItemID.Silk,8,-1,5,ItemID.TissueSample,6]);Register([ItemID.Silk,8,-1,5,ItemID.Bone,25]);}
    }
    void Register(int[] costs){var r=CreateRecipe();for(int i=0;i<costs.Length;i+=2){if(costs[i]<0)r.AddRecipeGroup(TrainingCatalog.Group(costs[i]),costs[i+1]);else r.AddIngredient(costs[i],costs[i+1]);}r.AddTile(Kind==19?TileID.WorkBenches:ModContent.TileType<CatWorkbenchTile>());r.Register();}
}

public sealed class CatWorkbenchTile:ModTile {
    public override string Texture=>"PalicoCompanion/Assets/Training/CatWorkbenchTile";
    public override void SetStaticDefaults(){
        Main.tileTable[Type]=true;Main.tileSolidTop[Type]=true;Main.tileNoAttach[Type]=true;Main.tileFrameImportant[Type]=true;Main.tileLavaDeath[Type]=true;
        TileID.Sets.DisableSmartCursor[Type]=true;TileID.Sets.IgnoredByNpcStepUp[Type]=true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x1);TileObjectData.newTile.CoordinateHeights=[18];TileObjectData.addTile(Type);
        AddToArray(ref TileID.Sets.RoomNeeds.CountsAsTable);AdjTiles=[TileID.WorkBenches];DustType=DustID.WoodFurniture;
        AddMapEntry(new Color(100,115,175),Language.GetOrRegister("Mods.PalicoCompanion.CatWorkbenchName"));
        RegisterItemDrop(TrainingCatalog.Type(19));
    }
    public override bool RightClick(int i,int j){Main.LocalPlayer.GetModPlayer<TrainingGuide>().UsedBench();TrainingPanel.OpenGuide();return true;}
    public override void MouseOver(int i,int j){Main.LocalPlayer.noThrow=2;Main.LocalPlayer.cursorItemIconEnabled=true;Main.LocalPlayer.cursorItemIconID=TrainingCatalog.Type(19);}
}
