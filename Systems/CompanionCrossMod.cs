using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.Localization;

namespace PalicoCompanion.Systems;

public sealed class CompanionCrossMod:ModSystem {
    internal static Action<Player,Vector2> PrepareActor;
    // Optional non-combat respawn limits, queried without running death hooks.
    internal static Func<int,int> PeacefulRespawnLimit;
    internal static int PeacefulRespawnTicks(int normal){
        int limit=normal;
        if(PeacefulRespawnLimit!=null)foreach(Func<int,int> cap in PeacefulRespawnLimit.GetInvocationList())
            limit=Math.Min(limit,Math.Clamp(cap(normal),1,normal));
        return limit;
    }
    internal static Func<Item,bool> SupportTool,SafeThrow;
    internal static bool AllowsThrow(Item item){
        if(SafeThrow==null)return false;
        foreach(Func<Item,bool> accepts in SafeThrow.GetInvocationList())if(accepts(item))return true;
        return false;
    }
    internal static Action<CompanionNativeUse> UpdateResources;
    internal static Action<CompanionNativeUse,CompanionStats> CaptureResources;
    internal static Func<Item,bool> UsesInspiration;
    internal static Action<CompanionNativeUse> UpdateEquipment;
    internal static Action<CompanionNativeUse> UpdateInheritedStatus;
    internal static Action<Player,Player,float> InheritStats;
    internal static Action<CompanionNativeUse> BeforeReset;
    internal static Func<CompanionNativeUse,Player> SupportTarget;
    internal static Func<CompanionNativeUse,bool> ResourceReady;
    internal static bool ResourcesReady(CompanionNativeUse use){
        if(ResourceReady==null)return true;
        foreach(Func<CompanionNativeUse,bool> ready in ResourceReady.GetInvocationList())if(!ready(use))return false;
        return true;
    }
    internal static Action<CompanionNativeUse> PlanUse;
    internal static Action<CompanionNativeUse> MaintainHeldWeapon;
    internal static Func<Item,float?> WeaponReach;
    internal static Func<Projectile,Player,CompanionBallisticProfile?> ProjectileFlight;
    internal static CompanionBallisticProfile? Flight(Projectile projectile,Player actor){
        if(ProjectileFlight!=null)foreach(Func<Projectile,Player,CompanionBallisticProfile?> profile in ProjectileFlight.GetInvocationList())
            if(profile(projectile,actor) is {} flight)return flight;
        return null;
    }
    internal static float? Reach(Item item){
        if(WeaponReach!=null)foreach(Func<Item,float?> range in WeaponReach.GetInvocationList())
            if(range(item) is float value)return value;
        return null;
    }
    internal static Action<CompanionNativeUse,int,byte,byte,float> WeaponEffect;
    internal static ICompanionStorageProvider Storage;
    internal static Func<Item,Item,int> UsesPerMaterial;
    internal static readonly System.Collections.Generic.List<Func<Item,Projectile,CompanionNativeUse,bool?>> ChargeComplete=new();
    internal static bool? Charged(Item weapon,Projectile shot,CompanionNativeUse use){
        foreach(var charge in ChargeComplete)
            if(charge(weapon,shot,use) is bool complete)return complete;
        return null;
    }
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
    public override void Unload(){PeacefulRespawnLimit=null;PrepareActor=null;CaptureResources=null;UsesInspiration=null;SupportTool=null;SafeThrow=null;UpdateResources=null;UpdateEquipment=null;UpdateInheritedStatus=null;InheritStats=null;BeforeReset=null;SupportTarget=null;ResourceReady=null;PlanUse=null;MaintainHeldWeapon=null;WeaponReach=null;ProjectileFlight=null;WeaponEffect=null;Storage=null;UsesPerMaterial=null;ChargeComplete.Clear();InfiniteStack=null;MinedOre=null;RecipeCategoryRegistered=false;CompanionRecovery.Unload();}
}
