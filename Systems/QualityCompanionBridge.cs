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
    public override void Load(){CompanionCrossMod.InfiniteStack=Threshold;CompanionCrossMod.MinedOre=Chain;}
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
    public override void Unload(){CompanionCrossMod.InfiniteStack=null;CompanionCrossMod.MinedOre=null;CompanionCrossMod.PrepareActor-=PreparePlacement;placement=null;tileSpeed=wallSpeed=null;}
    static void Chain(Player owner,Point cell,int type){
        if(CompanionNetwork.Client||!ImproveConfigs.Instance.SimpleVeinMining||ModLoader.HasMod("BAM")||ModLoader.HasMod("OreExcavator")||
            !FieldAssist.AllowedOre(type)||!CompanionExclusions.CanChain(owner,type))return;
        int previous=VeinMinerSystem.MinerIndex;
        try{VeinMinerSystem.MinerIndex=owner.whoAmI;VeinMinerSystem.DoVeinMiningAt(cell,type);}
        finally{VeinMinerSystem.MinerIndex=previous;}
    }
    static int Threshold(Item item){
        var c=ImproveConfigs.Instance;if(c==null||item.IsAir||!item.consumable)return 0;
        int count=item.ammo>0?(c.NoConsume_Ammo?(item.type==ItemID.FallenStar?999:3996):0):
            c.NoConsume_Projectile&&item.shoot>ProjectileID.None?3996:0;
        return count<=item.maxStack?count:0;
    }
}
