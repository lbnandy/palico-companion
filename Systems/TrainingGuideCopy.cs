using System;
using System.Linq;
using System.Text.RegularExpressions;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

internal static class TrainingGuideCopy {
    // Resolve the catalog's three-value expressions, rather than maintaining
    // another set of balance numbers for the guide, tooltips and HUD.
    static readonly Regex TierValues=new(@"(?<v>\d+(?:\.\d+)?%?)\s*/\s*(?<v>\d+(?:\.\d+)?%?)\s*/\s*(?<v>\d+(?:\.\d+)?%?)",RegexOptions.Compiled);
    // Explosive damage also follows the world's Hardmode state, so current
    // values use the same resolver as the server-authoritative projectile.
    static string TierValue(Match match,int kind,int tier)=>kind switch {
        7=>CompanionTraining.BombDamage(tier).ToString(),
        TrainingCatalog.Barrel=>CompanionBarrelCharge.Damage(tier).ToString(),
        _=>match.Groups["v"].Captures[Math.Clamp(tier,0,2)].Value
    };
    internal static string Detail(int kind,int tier,bool compare=false){
        string text=TrainingCatalog.Entries[kind].Detail;
        return CooldownText(compare?text:TierValues.Replace(text,m=>TierValue(m,kind,tier)),kind,tier,compare);
    }
    internal static string Tooltip(int kind,int tier,bool compare=false){
        // Short copy reuses the guide's tier values; balance numbers have one
        // localized source, rather than drifting between two descriptions.
        var entry=TrainingCatalog.Entries[kind];
        object[] values=TierValues.Matches(entry.Detail).Select(m=>(object)(compare?m.Value:TierValue(m,kind,tier))).ToArray();
        return CooldownText(CompanionText.Get("ItemTooltips."+entry.Key,values),kind,tier,compare);
    }
    static string CooldownText(string text,int kind,int tier,bool compare){
        int first=CompanionTraining.CooldownSeconds(kind,0),last=CompanionTraining.CooldownSeconds(kind,2);
        string value=compare&&first!=last?$"{first} / {CompanionTraining.CooldownSeconds(kind,1)} / {last}":CompanionTraining.CooldownSeconds(kind,tier).ToString();
        return text.Replace("<cooldown>",value);
    }
    internal static string DirectInstruction(){
        var keys=PalicoCompanion.DirectKey?.GetAssignedKeys();
        return keys?.Count>0?CompanionText.Get("Guide.HoldKey")+string.Join(" / ",keys)+CompanionText.Get("Guide.AfterKey"):CompanionText.Get("Guide.BindKey");
    }
    internal static string Page(int entry)=>entry switch {
        1=>CompanionText.Get("Guide.Introduction")+DirectInstruction()+CompanionText.Get("Guide.DirectAndRecover"),
        2=>CompanionText.Get("Guide.Combat")+
            (CompanionCrossMod.SupportTool!=null?CompanionText.Get("Guide.Thorium"):"")+
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
