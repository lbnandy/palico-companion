using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.Localization;

namespace PalicoCompanion.Systems;

public sealed class CompanionCrossMod:ModSystem {
    internal static Action<Player,Vector2> PrepareActor;
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
    public override void Unload(){PrepareActor=null;Storage=null;UsesPerMaterial=null;ReleaseChannel=null;InfiniteStack=null;MinedOre=null;RecipeCategoryRegistered=false;CompanionRecovery.Unload();}
}
