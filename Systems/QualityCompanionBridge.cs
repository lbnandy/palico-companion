using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ImproveGame.Common.Configs;
using ImproveGame.Content.Functions.VeinMiner;
using Microsoft.Xna.Framework;
using System.Linq;
using System.Reflection;
namespace PalicoCompanion.Systems;

// QoT 1.8.2 exposes consumption hooks, but no public threshold-query Call.
// Only stock planning needs this adapter; never duplicate the consumption hooks.
[ExtendsFromMod("ImproveGame")]
[JITWhenModsEnabled("ImproveGame")]
public sealed class QualityCompanionBridge:ModSystem {
    public override void Load(){CompanionCrossMod.PeacefulRespawnLimit+=PeacefulRespawn; CompanionCrossMod.InfiniteStack=Threshold;CompanionCrossMod.MinedOre=Chain;CompanionCrossMod.InfernoOpacity=InfernoOpacity;}
    // Read the registered client config live, just as QoT's native ring patch
    // does. Avoid its version-dependent Instance convenience property.
    static float InfernoOpacity()=>ModContent.GetInstance<UIConfigs>().InfernoTransparency;
    ModPlayer placement;
    FieldInfo tileSpeed,wallSpeed;
    public override void PostSetupContent(){
        // QoT caches these in UpdateEquips, a human-player lifecycle stage the
        // isolated actor does not run. Its ApplyItemAnimation hook reads them
        // for all placement-capable items, including the Staff of Regrowth.
        placement=ModLoader.GetMod("ImproveGame").GetContent<ModPlayer>().FirstOrDefault(p=>p.Name=="TilePlaceBoostHandler");
        tileSpeed=placement?.GetType().GetField("OriginalTileSpeed",BindingFlags.Instance|BindingFlags.NonPublic);
        wallSpeed=placement?.GetType().GetField("OriginalWallSpeed",BindingFlags.Instance|BindingFlags.NonPublic);
        if(tileSpeed!=null&&wallSpeed!=null)CompanionCrossMod.PrepareActor+=PreparePlacement;
    }
    void PreparePlacement(Player actor,Vector2 aim){var cache=actor.GetModPlayer(placement);tileSpeed.SetValue(cache,actor.tileSpeed);wallSpeed.SetValue(cache,actor.wallSpeed);}
    public override void Unload(){CompanionCrossMod.PeacefulRespawnLimit-=PeacefulRespawn; CompanionCrossMod.InfiniteStack=null;CompanionCrossMod.MinedOre=null;CompanionCrossMod.InfernoOpacity=null;CompanionCrossMod.PrepareActor-=PreparePlacement;placement=null;tileSpeed=wallSpeed=null;}
    static int PeacefulRespawn(int normal){
        var config=ModContent.GetInstance<ImproveConfigs>();
        int reduction=System.Math.Clamp(config.ResurrectionTimeShortened,0,100);
        // Reuse QoT's configured reduction and its non-boss UpdateDead cap.
        return reduction==0?normal:System.Math.Min(System.Math.Max(90,normal*(100-reduction)/100),900*(100-reduction)/100);
    }
    static void Chain(Player owner,Point cell,int type){
        if(CompanionNetwork.Client||!ModContent.GetInstance<ImproveConfigs>().SimpleVeinMining||ModLoader.HasMod("BAM")||ModLoader.HasMod("OreExcavator")||
            !FieldAssist.AllowedOre(type)||!CompanionExclusions.CanChain(owner,type))return;
        int previous=VeinMinerSystem.MinerIndex;
        try{VeinMinerSystem.MinerIndex=owner.whoAmI;VeinMinerSystem.DoVeinMiningAt(cell,type);}
        finally{VeinMinerSystem.MinerIndex=previous;}
    }
    static int Threshold(Item item){
        // Use tML's config registry; QoT's convenience Instance property is not
        // present in all supported builds of the same config class.
        var c=ModContent.GetInstance<ImproveConfigs>();if(c==null||item.IsAir||!item.consumable)return 0;
        int count=item.ammo>0?(c.NoConsume_Ammo?(item.type==ItemID.FallenStar?999:3996):0):
            c.NoConsume_Projectile&&item.shoot>ProjectileID.None?3996:0;
        return count<=item.maxStack?count:0;
    }
}
