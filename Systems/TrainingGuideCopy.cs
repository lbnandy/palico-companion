using System;
using System.Linq;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

internal static class TrainingGuideCopy {
    internal static string Detail(int kind,int tier,bool compare=false)=>WithNotes(
        CooldownText(CompanionText.Get("Catalog."+TrainingCatalog.Entries[kind].Key,compare?TrainingTiers.CompareValues(kind):TrainingTiers.Values(kind,tier)),kind,tier,compare),kind,tier,compare);
    internal static string Tooltip(int kind,int tier,bool compare=false)=>WithNotes(
        CooldownText(CompanionText.Get("ItemTooltips."+TrainingCatalog.Entries[kind].Key,compare?TrainingTiers.CompareValues(kind):TrainingTiers.Values(kind,tier)),kind,tier,compare),kind,tier,compare);
    static string WithNotes(string text,int kind,int tier,bool compare){
        if(tier!=TrainingTiers.Last&&!compare)return text;
        string key=kind switch {0=>"CelestialHealing",1=>"CelestialShield",3=>"CelestialMana",7 or TrainingCatalog.Barrel=>"CelestialDamage",_=>null};
        return key==null?text:text+"\n"+CompanionText.Get("Catalog."+key);
    }
    static string CooldownText(string text,int kind,int tier,bool compare){
        var values=Enumerable.Range(0,TrainingTiers.Last+1).Select(t=>CompanionTraining.CooldownSeconds(kind,t)).ToArray();
        string value=compare&&values.Distinct().Count()>1?string.Join(" / ",values):CompanionTraining.CooldownSeconds(kind,tier).ToString();
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
