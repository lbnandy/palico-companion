using System;
using System.Linq;
using System.Text.RegularExpressions;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

internal static class TrainingGuideCopy {
    // Resolve the catalog's three-value expressions, rather than maintaining
    // another set of balance numbers for the guide, tooltips and HUD.
    static readonly Regex TierValues=new(@"(?<v>\d+(?:\.\d+)?%?)\s*/\s*(?<v>\d+(?:\.\d+)?%?)\s*/\s*(?<v>\d+(?:\.\d+)?%?)",RegexOptions.Compiled);
    internal static string Detail(int kind,int tier,bool compare=false){
        string text=TrainingCatalog.Entries[kind].Detail;
        return compare?text:TierValues.Replace(text,m=>m.Groups["v"].Captures[Math.Clamp(tier,0,2)].Value);
    }
    internal static string Tooltip(int kind,int tier,bool compare=false){
        // Short copy reuses the guide's tier values; balance numbers have one
        // localized source, rather than drifting between two descriptions.
        var entry=TrainingCatalog.Entries[kind];
        object[] values=TierValues.Matches(entry.Detail).Select(m=>(object)(compare?m.Value:m.Groups["v"].Captures[Math.Clamp(tier,0,2)].Value)).ToArray();
        return CompanionText.Get("ItemTooltips."+entry.Key,values);
    }
    internal static string DirectInstruction(){
        var keys=PalicoCompanion.DirectKey?.GetAssignedKeys();
        return keys?.Count>0?CompanionText.Get("Guide.HoldKey")+string.Join(" / ",keys)+CompanionText.Get("Guide.AfterKey"):CompanionText.Get("Guide.BindKey");
    }
    internal static string Page(int entry)=>entry switch {
        1=>CompanionText.Get("Guide.Introduction")+DirectInstruction()+CompanionText.Get("Guide.DirectAndRecover"),
        2=>CompanionText.Get("Guide.Combat")+
            (CompanionCrossMod.InfiniteStack!=null?CompanionText.Get("Guide.QotAmmo"):"")+
            CompanionText.Get("Guide.Summons"),
        3=>CompanionText.Get("Guide.Mining")+
            (CompanionCrossMod.MinedOre!=null?CompanionText.Get("Guide.QotMining"):"")+
            CompanionText.Get("Guide.HarvestAndBuild"),
        4=>CompanionText.Get("Guide.Exploring"),
        5=>CompanionText.Get("Guide.Fishing"),
        6=>CompanionText.Get("Guide.Storage")+
            (CompanionCrossMod.Storage!=null?CompanionText.Get("Guide.MagicStorage"):"")+
            CompanionText.Get("Guide.Reserves"),
        7=>CompanionText.Get("Guide.Equipment")+
            (CompanionCrossMod.RecipeCategoryRegistered?CompanionText.Get("Guide.RecipeBrowser"):""),
        8=>CompanionText.Get("Guide.Multiplayer")+
            (ModContent.GetInstance<CompanionConfig>().AdvancedOrders?CompanionText.Get("Guide.AdvancedOrders"):"")+
            CompanionText.Get("Guide.Saves"),
        _=>""
    };
}
