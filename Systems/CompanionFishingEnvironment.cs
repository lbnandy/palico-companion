using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace PalicoCompanion.Systems;

// Reuse native tile weights and thresholds without running the local player's
// scene scan, which also changes music, visuals and nearby-tile effects.
internal sealed class CompanionFishingEnvironment {
    static readonly FieldInfo Counts=typeof(SceneMetrics).GetField("_tileCounts",BindingFlags.Instance|BindingFlags.NonPublic)!;
    static readonly Action<SceneMetrics> Export=typeof(SceneMetrics).GetMethod("ExportTileCountsToMain",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate<Action<SceneMetrics>>();
    readonly SceneMetrics metrics=new();
    Point scanned;
    ulong nextScan;
    bool ready;
    internal void Apply(Player actor){
        var cell=actor.Center.ToTileCoordinates();
        if(!WorldGen.InWorld(cell.X,cell.Y,1))return;
        if(!ready||Main.GameUpdateCount>=nextScan||Math.Abs(cell.X-scanned.X)>8||Math.Abs(cell.Y-scanned.Y)>8){
            metrics.Reset();var counts=(int[])Counts.GetValue(metrics)!;
            int left=Math.Max(0,cell.X-Main.buffScanAreaWidth/2),top=Math.Max(0,cell.Y-Main.buffScanAreaHeight/2);
            int right=Math.Min(Main.maxTilesX,cell.X-Main.buffScanAreaWidth/2+Main.buffScanAreaWidth),bottom=Math.Min(Main.maxTilesY,cell.Y-Main.buffScanAreaHeight/2+Main.buffScanAreaHeight);
            for(int x=left;x<right;x++)for(int y=top;y<bottom;y++){
                var tile=Main.tile[x,y];
                if(tile.HasTile&&(!TileID.Sets.isDesertBiomeSand[tile.TileType]||!WorldGen.oceanDepths(x,y)))counts[tile.TileType]++;
            }
            Export(metrics);scanned=cell;nextScan=Main.GameUpdateCount+60;ready=true;
        }
        actor.zone1=actor.zone2=actor.zone3=actor.zone4=actor.zone5=0;
        actor.ZoneCorrupt=metrics.EnoughTilesForCorruption;
        actor.ZoneCrimson=metrics.EnoughTilesForCrimson;
        actor.ZoneHallow=metrics.EnoughTilesForHallow;
        actor.ZoneJungle=metrics.EnoughTilesForJungle&&cell.Y<Main.UnderworldLayer;
        actor.ZoneSnow=metrics.EnoughTilesForSnow;
        actor.ZoneDesert=metrics.EnoughTilesForDesert;
        actor.ZoneGlowshroom=metrics.EnoughTilesForGlowingMushroom;
        actor.ZoneMeteor=metrics.EnoughTilesForMeteor;
        actor.ZoneGraveyard=metrics.EnoughTilesForGraveyard;
        actor.ZoneUnderworldHeight=cell.Y>Main.UnderworldLayer;
        actor.ZoneRockLayerHeight=cell.Y<=Main.UnderworldLayer&&cell.Y>Main.rockLayer;
        actor.ZoneDirtLayerHeight=cell.Y<=Main.rockLayer&&cell.Y>Main.worldSurface;
        actor.ZoneOverworldHeight=cell.Y<=Main.worldSurface&&cell.Y>Main.worldSurface*.35;
        actor.ZoneSkyHeight=cell.Y<=Main.worldSurface*.35;
        actor.ZoneBeach=WorldGen.oceanDepths(cell.X,cell.Y);
        actor.ZoneRain=Main.raining&&cell.Y<=Main.worldSurface;
        var wall=Main.tile[cell.X,cell.Y].WallType;
        actor.ZoneDungeon=metrics.DungeonTileCount>=250&&cell.Y>Main.worldSurface&&Main.wallDungeon[wall];
        actor.ZoneUndergroundDesert=actor.ZoneDesert&&cell.Y>Main.worldSurface&&(WallID.Sets.Conversion.Sandstone[wall]||WallID.Sets.Conversion.HardenedSand[wall]);
    }
}
