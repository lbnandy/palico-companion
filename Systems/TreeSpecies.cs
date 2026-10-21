using Terraria;
using Terraria.ID;
using Terraria.GameContent;
namespace PalicoCompanion.Systems;

// Native growth profiles supply soil tests. Decorative trees can be requested
// or helped when the owner chops them; idle forestry leaves landscaping alone.
public static class TreeSpecies {
    public static int SeedType(int trunk)=>trunk switch {
        TileID.Trees or TileID.PalmTree or TileID.TreeAsh=>ItemID.Acorn,
        >=TileID.TreeTopaz and <=TileID.TreeAmber=>ItemID.GemTreeTopazSeed+trunk-TileID.TreeTopaz,
        TileID.VanityTreeSakura=>ItemID.VanityTreeSakuraSeed,
        TileID.VanityTreeYellowWillow=>ItemID.VanityTreeYellowWillowSeed,
        _=>0
    };
    internal static bool Decorative(int trunk)=>trunk is TileID.VanityTreeSakura or TileID.VanityTreeYellowWillow;
    public static bool IsSeed(int type)=>type is ItemID.Acorn or ItemID.VanityTreeSakuraSeed or ItemID.VanityTreeYellowWillowSeed||type>=ItemID.GemTreeTopazSeed&&type<=ItemID.GemTreeAmberSeed;
    public static bool Soil(int x,int y,int trunk){
        if(!WorldGen.InWorld(x,y,10))return false;
        var t=Main.tile[x,y];if(!t.HasTile||t.IsActuated||t.IsHalfBlock||t.Slope!=0)return false;
        if(trunk==TileID.PalmTree)return t.TileType is TileID.Sand or TileID.Ebonsand or TileID.Crimsand or TileID.Pearlsand;
        if(trunk==TileID.Trees)return t.TileType<TileID.Count&&t.TileType!=TileID.MushroomGrass&&WorldGen.IsTileTypeFitForTree(t.TileType);
        return SeedType(trunk)>0&&WorldGen.GrowTreeSettings.Profiles.TryGetFromTreeId(trunk,out var profile)&&profile.GroundTest(t.TileType);
    }
    public static Item FindSeed(Player p,CompanionPlayer s,int trunk){
        int type=SeedType(trunk);if(type==0)return null;
        foreach(var i in s.Bag)if(!i.IsAir&&!i.favorited&&i.type==type)return i;
        if(CompanionNetwork.Server){CompanionNetwork.RequestMaterial(s,type);return null;}
        for(int n=0;n<50;n++){var i=p.inventory[n];if(!i.IsAir&&!i.favorited&&i.type==type)return i;}
        return null;
    }
    public static bool Plant(int x,int y,Item seed){
        var sample=ContentSamples.ItemsByType[seed.type];
        if(!WorldGen.PlaceObject(x,y,sample.createTile,mute:true,style:sample.placeStyle))return false;
        // Native framing selects ordinary sapling's biome style from its soil.
        WorldGen.SquareTileFrame(x,y);
        return Main.tile[x,y].HasTile&&Main.tile[x,y].TileType==sample.createTile;
    }
}
