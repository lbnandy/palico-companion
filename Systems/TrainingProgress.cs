using System;
using System.Linq;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// A view over registered recipes, not a second set of unlock/crafting rules.
internal static class TrainingProgress {
    static readonly Dictionary<int,Recipe[]> recipesById=new();
    internal static void Clear()=>recipesById.Clear();
    internal static Recipe[] Recipes(int id){if(!recipesById.TryGetValue(id,out var recipes))recipesById[id]=recipes=Main.recipe.Where(r=>r!=null&&r.createItem.type==TrainingCatalog.Type(id)).ToArray();return recipes;}
    internal static int Owned(Player p,Recipe recipe,Item ingredient){
        var group=recipe.acceptedGroups.Select(g=>RecipeGroup.recipeGroups[g]).FirstOrDefault(g=>g.ValidItems.Contains(ingredient.type));
        return p.inventory.Take(58).Where(i=>!i.IsAir&&(group==null?i.type==ingredient.type:group.ValidItems.Contains(i.type))).Sum(i=>i.stack);
    }
    internal static bool Materials(Player p,Recipe recipe)=>recipe.requiredItem.Where(i=>!i.IsAir).All(i=>Owned(p,recipe,i)>=i.stack);
    internal static bool Available(Recipe recipe)=>Enumerable.Range(0,Main.numAvailableRecipes).Any(i=>ReferenceEquals(Main.recipe[Main.availableRecipe[i]],recipe));
    internal static bool Complete(CompanionPlayer s,int id)=>id==13&&s.Training.Expanded||id==14&&s.Training.Tier>=1||id==15&&s.Training.Tier>=2;
    internal static bool Has(CompanionPlayer s,int id)=>s.Player.inventory.Concat(s.Bag).Concat(s.Training.Slots)
        .Concat(s.Loadouts.Sets.SelectMany(set=>set)).Concat(s.DeliveryRemainder).Any(item=>TrainingCatalog.Id(item)==id);
    internal static string Status(CompanionPlayer s,int id,Recipe selected=null){
        if(Complete(s,id))return CompanionText.Get("TrainingProgress.Used");
        if(id==15&&s.Training.Tier==0)return CompanionText.Get("TrainingProgress.NeedsImproved");
        if(id is >=13 and <=15&&Has(s,id))return CompanionText.Get("TrainingProgress.ReadyToUse");
        var recipes=selected==null?Recipes(id):new[]{selected};
        if(recipes.Any(Available))return CompanionText.Get("TrainingProgress.Craftable");
        if(recipes.Any(r=>Materials(s.Player,r)))return CompanionText.Get("TrainingProgress.NeedsStation");
        return CompanionText.Get("TrainingProgress.MissingMaterials");
    }
    internal static string Next(CompanionPlayer s)=>s.Training.Tier switch {
        0=>CompanionText.Get("TrainingProgress.NextImproved"),
        1=>CompanionText.Get("TrainingProgress.NextMasterwork"),
        _=>CompanionText.Get("TrainingProgress.MaxTier")
    };
    internal static IEnumerable<int> Suggestions(CompanionPlayer s){
        if(!s.Player.GetModPlayer<TrainingGuide>().KnowsBench&&!Has(s,19)&&!s.Player.adjTile[ModContent.TileType<CatWorkbenchTile>()])yield return 19;
        var candidates=Enumerable.Range(0,16).Where(TrainingCatalog.Current).Where(id=>!Complete(s,id)&&(!Has(s,id)||id is >=13 and <=15)&&!(id==15&&s.Training.Tier==0));
        double Prepared(int id)=>Recipes(id).Select(r=>r.requiredItem.Where(i=>!i.IsAir).Average(i=>Math.Min(1,Owned(s.Player,r,i)/(double)i.stack))).DefaultIfEmpty(0).Max();
        int[] order=[1,9,10,0,7,3,2,6,4,5,8,13,14,15];
        foreach(int id in candidates.OrderByDescending(id=>id is >=13 and <=15&&Has(s,id))
            .ThenByDescending(id=>Recipes(id).Any(Available)).ThenByDescending(id=>Recipes(id).Any(r=>Materials(s.Player,r)))
            .ThenByDescending(id=>id is >=13 and <=15&&Recipes(id).Any(r=>Available(r)||Materials(s.Player,r)))
            .ThenByDescending(Prepared).ThenBy(id=>Array.IndexOf(order,id)))yield return id;
    }
}

public sealed class TrainingProgressCache:ModSystem {public override void PostAddRecipes()=>TrainingProgress.Clear();public override void Unload()=>TrainingProgress.Clear();}
