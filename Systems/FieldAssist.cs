using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace PalicoCompanion.Systems;

/// <summary>Bounded mining assistance. Uses vanilla PickTile and its normal drops.</summary>
public sealed class FieldAssist
{
    public bool Active { get; private set; }
    public bool Terrain {get;private set;}
    public bool OwnerRequested {get;private set;}
    public bool Failed {get;private set;}
    public string Status { get; private set; } = "No mining request";
    public Point Tile { get; private set; }
    public Vector2 Target => new Vector2(Tile.X * 16 + 8, Tile.Y * 16 + 8);
    public Vector2 MoveTarget { get; private set; }
    int approachRetry, blockedTicks;
    private int originalType, owner, elapsed, cadence;
    readonly MiningCadence miningClock=new();
    private string world = "";
    private Item miningTool;
    int ReachBonus=>16*Main.player[owner].GetModPlayer<CompanionPlayer>().Training.MiningReach;
    readonly Queue<Point> pending = new();
    public int Completed { get; private set; }
    public int Planned { get; private set; }
    public int SwingTicks { get; private set; }
    public int SwingDuration { get; private set; } = 20;
    public int ToolType => miningTool?.type ?? 0;
    public int HitInterval { get; private set; }
    public int SwingItemType { get; private set; }
    public Item SwingItem { get; private set; }
    public Vector2 SwingTarget { get; private set; }
    public void UpdateVisual() { if(SwingTicks>0)SwingTicks--; }


    public string Request(Player player, Vector2 mouseWorld, bool vein = false, Item observedTool = null, bool terrain = false, int veinLimit = 12, bool ownerRequested = false)
    {
        if (Active) return Status = "Cancel the current mining request first";
        if (Main.netMode == NetmodeID.MultiplayerClient || player == null || !player.active || player.dead)
            return Status = "Mining assistance requires a living single player";
        if (!float.IsFinite(mouseWorld.X) || !float.IsFinite(mouseWorld.Y)) return Status = "Invalid mining target";
        Terrain=terrain;Failed=false;OwnerRequested=ownerRequested;
        Tile = new Point((int)(mouseWorld.X / 16), (int)(mouseWorld.Y / 16));
        if (!WorldGen.InWorld(Tile.X, Tile.Y, 10)) return Status = "Mining target touches the world boundary";
        if (!Main.tile[Tile.X, Tile.Y].HasTile || !(Terrain?DiggingIntent.Allowed(Main.tile[Tile.X, Tile.Y].TileType):AllowedOre(Main.tile[Tile.X, Tile.Y].TileType)))
            return Status = Terrain?CompanionText.Message("FieldAssist.NotSolid"):CompanionText.Message("FieldAssist.NotOre");
        originalType = Main.tile[Tile.X, Tile.Y].TileType;
        if(!CompanionExclusions.Mine(player,Tile))return Status=CompanionText.Message("FieldAssist.Excluded");
        owner = player.whoAmI; world = Main.ActiveWorldFileData.UniqueId.ToString();
        // Keep the actual tool, not a cloned item or a remembered power value.
        // Re-select each tick; removal falls back to the companion's basic pick.
        miningTool=MiningTools.Best(player);
        string error = Validate(player);
        if (error != null) return Status = error;
        pending.Clear();Completed=0;Planned=1;
        if(vein&&!Terrain) {
            // Snapshot at most twelve connected, same-type ore cells; never chase newly exposed ore.
            var seen=new HashSet<Point>{Tile};var search=new Queue<Point>();search.Enqueue(Tile);
            while(search.Count>0 && seen.Count<veinLimit) {
                var cell=search.Dequeue();
                foreach(var offset in new[]{new Point(-1,0),new Point(1,0),new Point(0,-1),new Point(0,1)}) {
                    var next=new Point(cell.X+offset.X,cell.Y+offset.Y);
                    if(seen.Count>=veinLimit)break;
                    if(!WorldGen.InWorld(next.X,next.Y,10)||seen.Contains(next))continue;
                    var tile=Main.tile[next.X,next.Y];
                    if(!tile.HasTile||tile.TileType!=originalType||!MiningTools.InRange(player,miningTool,next,true))continue;
                    seen.Add(next);search.Enqueue(next);pending.Enqueue(next);
                }
            }
            Planned=1+pending.Count;
        }
        Active = true; elapsed = cadence = approachRetry = blockedTicks = 0;MoveTarget=player.Center;
        return Status = Terrain?CompanionText.Message("FieldAssist.StayNearby"):$"Mining requested: {Planned} ore tiles; stay nearby";
    }

    public string Cancel() { Active = false;pending.Clear();SwingTicks=0; return Status = $"Mining cancelled; {Completed}/{Planned} mined, drops retained"; }
    public string FailRoute() {Stop(CompanionText.Message("FieldAssist.RouteBlocked"));return Status;}

    /// <summary>Call once per game tick; caller may move the cat toward Target while Active.</summary>
    public void Tick(Player player, Vector2 catCenter, bool paused, bool danger)
    {
        if (!Active) return;
        if (paused) { Stop("Mining cancelled by pause"); return; }
        Item best=MiningTools.Best(player);
        if(!ReferenceEquals(best,miningTool)){miningTool=best;cadence=SwingTicks=0;}
        if(miningTool==null){Stop("Mining stopped: no pickaxe in your backpack");return;}
        if(danger){
            string invalid=Validate(player);
            if(invalid!=null){Stop(invalid);return;}
            SwingTicks=cadence=0;Status=CompanionText.Message("FieldAssist.CombatPause");return;
        }
        if (++elapsed > CompanionWorkTiming.Timeout(600)) { Stop("Mining stopped: check reach or pickaxe power"); return; }
        // Another miner can finish a queued tile. Skip it without claiming its work or drops.
        while (WorldGen.InWorld(Tile.X,Tile.Y,10) && !Main.tile[Tile.X,Tile.Y].HasTile) {
            if(pending.Count==0){Stop("Ore already cleared; remaining work finished",false);return;}
            Tile=pending.Dequeue();cadence=approachRetry=0;
        }
        // A vein snapshot is not a route: a queued interior cell can be hidden
        // behind another queued ore. Work the exposed frontier first.
        if((!CanReachSurface(player.Center)||Validate(player)!=null)&&pending.Count>0) {
            int count=pending.Count;
            while(count-->0&&(!CanReachSurface(player.Center)||Validate(player)!=null)) {pending.Enqueue(Tile);Tile=pending.Dequeue();cadence=approachRetry=0;}
        }
        string error = Validate(player);
        if (error != null) { Stop(error); return; }
        // PickTile updates tile damage, not item animation; keep assisting during owner swings.
        if (!float.IsFinite(catCenter.X) || !float.IsFinite(catCenter.Y)) { Stop("Invalid cat position"); return; }
        bool inReach=Vector2.DistanceSquared(catCenter,Target)<=MathF.Pow(64+ReachBonus,2)&&CanReachSurface(catCenter)&&CanReachSurface(player.Center);
        if(!inReach) {
            cadence=0;
            if(approachRetry--<=0) {
                approachRetry=15;
                MoveTarget=FindStand(catCenter,out Vector2 spot)?spot:player.Center;
            }
            if(++blockedTicks>=180){FailRoute();return;}
            Status=CompanionText.Message("FieldAssist.FindingFooting");return;
        }
        blockedTicks=0;MoveTarget=catCenter;
        // Ore is solid, so line-of-sight must be tested to its near surface, not its center.
        if (!CanReachSurface(catCenter) || !CanReachSurface(player.Center)) { cadence = 0; Status = "Mining target is obstructed"; return; }
        Status = Terrain?CompanionText.Message("FieldAssist.DiggingAhead"):CompanionText.Message("FieldAssist.MiningOre");
        if(cadence==0)HitInterval=miningClock.Begin(MiningTools.ExactInterval(player,miningTool));
        ++cadence;
        // Wind-up starts before the real PickTile hit; never animate an obstructed attempt.
        int windup=Math.Max(1,(int)(HitInterval*0.6f));
        if(cadence==HitInterval-windup+1) {SwingDuration=Math.Max(1,(int)Math.Ceiling(windup/0.6f));SwingTicks=SwingDuration;SwingItemType=miningTool.type;SwingItem=miningTool;SwingTarget=Target;CompanionItemVisual.BeginUse(miningTool,catCenter);}
        if (cadence < HitInterval) return;
        cadence = 0;
        // PickTile applies Terraria's actual pick-power rules, mining damage and normal drops.
        // Revalidated borrowed or built-in tool; do not change selectedItem or player input.
        MiningObservation.CompanionHit=true;
        try {using(CompanionTileActions.Begin())player.PickTile(Tile.X, Tile.Y, miningTool.pick);miningClock.Commit();}
        finally {MiningObservation.CompanionHit=false;}
        TrainingWorkFeedback.Trigger(player.GetModPlayer<CompanionPlayer>(),9);
        if(CompanionNetwork.Server)NetMessage.SendTileSquare(-1,Tile.X,Tile.Y,1);
        Terraria.Tile result = Main.tile[Tile.X, Tile.Y];
        if (!result.HasTile) {
            if(AllowedOre(originalType))CompanionCrossMod.MinedOre?.Invoke(player,Tile,originalType);
            Completed++;
            if(!Terrain&&pending.Count==0) {
                var state=player.GetModPlayer<CompanionPlayer>();
                state.Expression.Say(state,state.Cat,"Ore",Terraria.GameContent.UI.EmoteID.EmoteHappiness);
            }
            if(pending.Count==0){Active=false;Status=$"Mining complete: {Completed}/{Planned}; normal drops retained";}
            else {Tile=pending.Dequeue();elapsed=cadence=approachRetry=0;Status=$"Mining {Completed}/{Planned}; approaching next ore";}
        }
        else if (result.TileType != originalType) {
            if(Terrain&&DiggingIntent.Allowed(result.TileType))originalType=result.TileType; // Vanilla grass/moss stripping can expose its base block.
            else Stop("Mining stopped: target changed");
        }
    }

    private bool FindStand(Vector2 from,out Vector2 result)
    {
        result=from;float best=float.MaxValue;
        for(int dx=-4;dx<=4;dx++)for(int dy=-4;dy<=4;dy++) {
            var center=new Vector2((Tile.X+dx)*16+8,(Tile.Y+dy)*16-CompanionNavigation.Height/2f);
            if(!CompanionNavigation.TryStand(center,out center)||Vector2.DistanceSquared(center,Target)>MathF.Pow(60+ReachBonus,2)||!CanReachSurface(center))continue;
            // Prefer the near side of an obstacle; never use the ore center as a destination.
            float score=Vector2.DistanceSquared(from,center)+(Reachability.Clear(from,center)?0:160*160);
            if(score<best){best=score;result=center;}
        }
        // An exposed ceiling ore does not always have a floor underneath it.
        // Select a free hover position; navigation can take a short detour to it.
        if(best==float.MaxValue)for(int dx=-3;dx<=3;dx++)for(int dy=-3;dy<=3;dy++) {
            var center=new Vector2((Tile.X+dx)*16+8,(Tile.Y+dy)*16+8);
            if(Vector2.DistanceSquared(center,Target)>MathF.Pow(60+ReachBonus,2)||!CanReachSurface(center)||!CompanionNavigation.Fits(center))continue;
            float score=Vector2.DistanceSquared(from,center)+(CompanionNavigation.ClearFlight(from,center)?0:160*160);
            if(score<best){best=score;result=center;}
        }
        return best<float.MaxValue;
    }

    public bool CanReachSurface(Vector2 from)
    {
        float left=Tile.X*16,top=Tile.Y*16;
        // Use exposed face midpoints. A ray to the center (or a radial offset)
        // incorrectly clips neighboring floor blocks when approaching diagonally.
        return (from.Y<top && Reachability.Clear(from,new Vector2(left+8,top-1))) ||
            (from.Y>top+16 && Reachability.Clear(from,new Vector2(left+8,top+17))) ||
            (from.X<left && Reachability.Clear(from,new Vector2(left-1,top+8))) ||
            (from.X>left+16 && Reachability.Clear(from,new Vector2(left+17,top+8)));
    }

    private void Stop(string reason,bool failed=true) { Failed=failed;Active = false;pending.Clear();SwingTicks=0; Status = reason+$" ({Completed}/{Planned} mined; retained)"; }

    private string Validate(Player player)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || player == null || !player.active || player.dead || player.whoAmI != owner)
            return "Mining stopped: owner unavailable or multiplayer";
        if(!OwnerRequested&&player.GetModPlayer<CompanionPlayer>().EnvironmentPaused)return CompanionText.MessageFull("Mods.PalicoCompanion.UI.EnvironmentPaused");
        if (Main.ActiveWorldFileData.UniqueId.ToString() != world) return "Mining stopped: world changed";
        if (!HasTool(player,miningTool)) return "Mining stopped: working tool changed";
        if (!MiningTools.InRange(player,miningTool,Tile,!Terrain)) return CompanionText.Message("FieldAssist.OutOfReach");
        if(!VanillaMiningSelector.CanDamage(player,Tile,miningTool))return CompanionText.Message("FieldAssist.CannotMine");
        int x = Tile.X, y = Tile.Y;
        if(Terrain) {
            var state=player.GetModPlayer<CompanionPlayer>();
            if(!state.Digging.Active)return CompanionText.Message("FieldAssist.DirectionChanged");
            if(!VanillaMiningSelector.CanDamage(player,Tile,miningTool))return CompanionText.Message("FieldAssist.CannotMine");

        }
        if (!WorldGen.InWorld(x, y, 10)) return "Mining target outside safe world bounds";
        Terraria.Tile target = Main.tile[x, y];
        if (!Terrain && (target.IsActuated||target.HasActuator||target.TileColor!=PaintID.None))return CompanionText.Message("FieldAssist.ProtectedBlock");
        if (!CompanionExclusions.Mine(player,Tile))return CompanionText.Message("FieldAssist.Excluded");
        if (!target.HasTile || target.TileType != originalType || !(Terrain?DiggingIntent.Allowed(target.TileType):AllowedOre(target.TileType))) return "Mining stopped: selected ore changed";
        // Deliberate player-led digging uses vanilla eligibility, not autonomous-area bans.
        if(Terrain)return null;
        // Autonomous ore work examines the target, not large exclusion halos
        // around unrelated chests, residents, spawn points or nearby water.
        if(target.WallType!=WallID.None&&Main.wallHouse[target.WallType])return CompanionText.Message("FieldAssist.HousingWall");
        var above=Main.tile[x,y-1];
        if(above.HasTile&&TileID.Sets.Falling[above.TileType])return CompanionText.Message("FieldAssist.FallingBlock");
        return null;
    }

    // Luminite is an ore but is absent from vanilla's world-generation ore set.
    // Registered mod ores participate; native mining hooks still decide eligibility.
    public static bool AllowedOre(int type) => type>=0 && type<TileID.Sets.Ore.Length &&
        (TileID.Sets.Ore[type] || type==TileID.LunarOre || Gem(type));
    internal static bool Gem(int type)=>type is TileID.Sapphire or TileID.Ruby or TileID.Emerald or TileID.Topaz or TileID.Amethyst or TileID.Diamond or TileID.ExposedGems;
    public static bool HasTool(Player player,Item tool) => tool!=null&&!tool.IsAir&&tool.pick>0&&
        ReferenceEquals(MiningTools.Best(player),tool);
}
