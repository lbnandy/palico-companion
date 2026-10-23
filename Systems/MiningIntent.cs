using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Owner activity guides priority; exposed nearby ore no longer needs a first hit.
public sealed class MiningIntent
{
    Point preferred, lastTarget;
    int preferenceTicks, scanDelay, clock;
    bool wasActive;
    readonly Dictionary<Point,int> deferred=new();
    public void Clear(){preferenceTicks=scanDelay=clock=0;wasActive=false;deferred.Clear();}
    public void Observe(Player p,Point cell)
    {
        if(!p.active||p.dead||p.HeldItem.pick<=0||(p.itemAnimation<=0&&!p.controlUseItem)||p.mouseInterface||
            !WorldGen.InWorld(cell.X,cell.Y,10))return;
        var tile=Main.tile[cell];
        if(tile.HasTile&&FieldAssist.AllowedOre(tile.TileType)){preferred=cell;preferenceTicks=120;}
    }
    public void Tick(Player p,CompanionPlayer s)
    {
        if(s.Directive.Active)return;
        if(s.EnvironmentPaused){if(s.Mining.Active&&!s.Mining.OwnerRequested)s.Mining.Cancel();Clear();return;}
        clock++;if(preferenceTicks>0)preferenceTicks--;
        if(Main.netMode==NetmodeID.MultiplayerClient||!(CompanionNetwork.Server?s.NetMining:ModContent.GetInstance<CompanionConfig>().MiningAssist)||!s.Enabled||s.Paused||s.Vitals.Downed||p.dead) {
            if(s.Mining.Active&&!s.Mining.Terrain&&!s.Mining.OwnerRequested)s.Mining.Cancel();
            wasActive=false;return;
        }
        if(s.Digging.Active||s.Mining.Active&&s.Mining.Terrain){wasActive=false;return;}
        if(s.Mining.Active){wasActive=true;lastTarget=s.Mining.Tile;return;}
        if(wasActive){wasActive=false;deferred[lastTarget]=clock+600;scanDelay=60;}
        if(s.Order.Active||s.Order.HasPreview||s.Storage.Active||s.Trees.Active||s.Garden.Active||s.Cat==null)return;
        if(!CompanionWorkOpportunity.KeepingUp(p,s.Cat)||--scanDelay>0)return;
        scanDelay=60; // One bounded scan per second, including walking exploration.
        if(CompanionThreat.WorkDanger(p,s.Cat.Center))return;
        Item tool=MiningTools.Best(p);if(tool==null)return;
        foreach(var key in new List<Point>(deferred.Keys))if(deferred[key]<=clock)deferred.Remove(key);
        Point range=MiningTools.Range(p,tool,true),center=p.Center.ToTileCoordinates();
        var candidates=new List<Point>();
        for(int x=center.X-range.X-1;x<=center.X+range.X+1;x++)
        for(int y=center.Y-range.Y-2;y<=center.Y+range.Y+2;y++) {
            var cell=new Point(x,y);
            if(!WorldGen.InWorld(x,y,10)||deferred.ContainsKey(cell)||!MiningTools.InRange(p,tool,cell,true))continue;
            var tile=Main.tile[cell];
            if(!tile.HasTile||!CompanionExclusions.Mine(p,cell)||!FieldAssist.AllowedOre(tile.TileType)||!CompanionWorkOpportunity.RemainsNearby(p,cell.ToWorldCoordinates(),System.Math.Max(range.X,range.Y)*16))continue;
            if(!VanillaMiningSelector.CanDamage(p,cell,tool))continue;
            if(WorldGen.SolidTile(x-1,y)&&WorldGen.SolidTile(x+1,y)&&WorldGen.SolidTile(x,y-1)&&WorldGen.SolidTile(x,y+1))continue;
            candidates.Add(cell);
        }
        float Score(Point cell)=>Vector2.DistanceSquared(cell.ToWorldCoordinates(),p.Center)-
            (preferenceTicks>0&&cell==preferred?100000:0);
        candidates.Sort((a,b)=>Score(a).CompareTo(Score(b)));
        int attempts=0;
        foreach(var cell in candidates) {
            if(++attempts>8)break;
            s.Mining.Request(p,cell.ToWorldCoordinates(),true);
            if(s.Mining.Active&&s.Mining.CanReachSurface(s.Cat.Center)) {
                wasActive=true;lastTarget=cell;s.Report(CompanionText.Message("MiningIntent.ExposedOre"));return;
            }
            if(s.Mining.Active)s.Mining.Cancel();
            deferred[cell]=clock+600;
        }
    }
}
