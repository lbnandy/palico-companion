using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.Localization;

namespace PalicoCompanion.Systems;

public sealed class CompanionCrossMod:ModSystem {
    internal static Action<Player,Vector2> PrepareActor;
    internal static Func<Item,bool> SupportTool,SafeThrow;
    internal static Action<CompanionNativeUse> UpdateResources;
    internal static Func<CompanionNativeUse,Player> SupportTarget;
    internal static Func<CompanionNativeUse,bool> ResourceReady;
    internal static bool ResourcesReady(CompanionNativeUse use){
        if(ResourceReady==null)return true;
        foreach(Func<CompanionNativeUse,bool> ready in ResourceReady.GetInvocationList())if(!ready(use))return false;
        return true;
    }
    internal static Action<CompanionNativeUse> PlanUse;
    internal static Func<Item,float?> WeaponReach;
    internal static Action<CompanionNativeUse,int,byte,byte,float> WeaponEffect;
    internal static ICompanionStorageProvider Storage;
    internal static Func<Item,Item,int> UsesPerMaterial;
    internal static Func<Item,CompanionNativeUse,bool> ReleaseChannel;
    // Optional metadata only. Actual consumption remains in tML's item hooks.
    internal static Func<Item,int> InfiniteStack;
    internal static Action<Player,Point,int> MinedOre;
    internal static bool RecipeCategoryRegistered {get;private set;}
    public override void PostSetupContent(){
        if(Main.dedServ||!ModLoader.TryGetMod("RecipeBrowser",out var recipes))return;
        // Public Recipe Browser API; no reflection into its UI or forced keys.
        var icon=ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>("PalicoCompanion/Assets/Training/HoneyCage");
        var result=recipes.Call("AddItemCategory",Language.ActiveCulture.Name=="zh-Hans"?"呆猫装备":"Palico equipment","",icon,(Predicate<Item>)(item=>item.ModItem is TrainingItem));
        RecipeCategoryRegistered=result is string status&&status=="Success";
    }
    public override void Unload(){PrepareActor=null;SupportTool=null;SafeThrow=null;UpdateResources=null;SupportTarget=null;ResourceReady=null;PlanUse=null;WeaponReach=null;WeaponEffect=null;Storage=null;UsesPerMaterial=null;ReleaseChannel=null;InfiniteStack=null;MinedOre=null;RecipeCategoryRegistered=false;CompanionRecovery.Unload();}
}
