using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Inspired by vanilla 1.4.5's intention lifetime and directional smart-cursor
// selection. Independently implemented against stable 1.4.4; no 1.4.5 API/code.
public sealed class DiggingIntent
{
    public int Remaining {get;private set;}
    public Point Origin {get;private set;}
    public Point Direction {get;private set;}
    Vector2 anchor,aim;
    Player snapshot;
    readonly List<Tuple<int,int>> skipped=new();
    int delay;
    bool awaiting;
    public bool Active=>Remaining>0;
    // Classification, not a material whitelist: all solid pick-mined blocks,
    // including ores and modded blocks. Furniture/platforms/axe/hammer objects
    // are excluded; actual power and destruction rules remain vanilla's job.
    public static bool Allowed(int type)=>type>=0&&type<Main.tileSolid.Length&&Main.tileSolid[type]&&
        !Main.tileSolidTop[type]&&!Main.tileFrameImportant[type]&&!Main.tileAxe[type]&&!Main.tileHammer[type];
    public static Point Axis(Vector2 delta)=>Math.Abs(delta.X)>=Math.Abs(delta.Y)?new Point(delta.X>=0?1:-1,0):new Point(0,delta.Y>=0?1:-1);
    public void Observe(Player p,Point cell,int type,Vector2? mouse=null) {
        if(!Allowed(type))return;
        var s=p.GetModPlayer<CompanionPlayer>();
        if(s.Directive.Active)return;
        s.Construction.Cancel();
        // A deliberate new terrain stroke takes priority over autonomous ore work.
        if(s.Mining.Active&&!s.Mining.Terrain)s.Mining.Cancel();
        Point dir=Axis(cell.ToWorldCoordinates()-p.Center);
        bool redirect=!Active||Direction!=dir;
        if(redirect){delay=0;skipped.Clear();}
        else if(Origin!=cell){delay=0;skipped.Clear();}
        // Consecutive strokes down the same shaft renew the owner's intention;
        // they must not restart the helper's wind-up on every new target tile.
        if(s.Mining.Active&&s.Mining.Terrain&&redirect){s.Mining.Cancel();awaiting=false;}
        if(s.Trees.Active)s.Trees.Cancel();
        if(s.Garden.Active)s.Garden.Cancel();
        if(s.Storage.Active)s.Storage.Cancel();
        Origin=cell;Direction=dir;anchor=p.Center;Remaining=480;
        aim=mouse??cell.ToWorldCoordinates();
        snapshot??=new Player();
        snapshot.position=p.position;snapshot.width=p.width;snapshot.height=p.height;
        snapshot.direction=p.direction;snapshot.gravDir=p.gravDir;snapshot.whoAmI=p.whoAmI;
    }
    public void Clear(CompanionPlayer s) {
        Remaining=0;awaiting=false;
        if(s.Mining.Active&&s.Mining.Terrain)s.Mining.Cancel();
    }
    public void Tick(Player p,CompanionPlayer s) {
        if(s.Directive.Active)return;
        if(!Active)return;
        // Inventory clicks are not a new world action. The real PickTile hook
        // already rejects UI input; inspecting the panel must not cancel work.
        bool otherUse=!p.mouseInterface&&(p.controlUseItem||p.itemAnimation>0)&&p.HeldItem.pick<=0;
        // Match autonomous mining and combat selection: an enemy sealed behind
        // a cave wall must not repeatedly erase the owner's digging intention.
        bool danger=CompanionThreat.WorkDanger(p,s.Cat?.Center??p.Center);
        // Jump is also wing/mount ascent. The real pick stroke remains valid
        // while airborne; distance, lifetime and a different tool end it.
        if(Remaining<=0||!s.Enabled||s.Paused||s.Vitals.Downed||p.dead||otherUse||
            !(CompanionNetwork.Server?s.NetDigging:ModContent.GetInstance<CompanionConfig>().DiggingAssist)||Vector2.DistanceSquared(anchor,p.Center)>80*80||
            s.Order.Active||s.Order.HasPreview){Clear(s);return;}
        // Combat pauses work but never refreshes the owner's finite intention.
        if(danger)return;
        Remaining--;
        if(awaiting&&!s.Mining.Active) {
            awaiting=false;
            // A bad face is not a cancelled owner command. Try another native
            // selector candidate while retaining the priority over loose drops.
            if(s.Mining.Failed){
                var failed=Tuple.Create(s.Mining.Tile.X,s.Mining.Tile.Y);
                if(!skipped.Contains(failed))skipped.Add(failed);
                delay=12;
            }
        }
        if(s.Mining.Active||s.Cat==null||s.Trees.Active||s.Garden.Active||s.Storage.Active||--delay>0)return;
        delay=12;
        // Same selector family used by Cattiva, reading a captured owner pose.
        // Safety-rejected faces are skipped, never dug through to reach another.
        for(int attempt=0;attempt<8;attempt++) {
            if(!VanillaMiningSelector.Select(snapshot,p,aim,Origin,skipped,out Point at))return;
            if(!WorldGen.InWorld(at.X,at.Y,10)||!Main.tile[at].HasTile||!Allowed(Main.tile[at].TileType)) {skipped.Add(Tuple.Create(at.X,at.Y));continue;}
            s.Mining.Request(p,at.ToWorldCoordinates(),terrain:true);
            if(s.Mining.Active&&s.Mining.CanReachSurface(p.Center)) {awaiting=true;s.Report(CompanionText.Message("DiggingIntent.HelpingDig"));return;}
            if(s.Mining.Active)s.Mining.Cancel();
            skipped.Add(Tuple.Create(at.X,at.Y));
        }
    }
}

// Observe real pick calls rather than cursor hover. Companion calls are excluded,
// otherwise each helper hit would renew its own permission indefinitely.
public sealed class MiningObservation : ModSystem
{
    internal static bool CompanionHit;
    public override void Load()=>On_Player.PickTile+=ObservePick;
    public override void Unload()=>On_Player.PickTile-=ObservePick;
    static void ObservePick(On_Player.orig_PickTile orig,Player p,int x,int y,int power) {
        bool observe=!CompanionHit&&Main.netMode!=NetmodeID.Server&&p.whoAmI==Main.myPlayer&&p.active&&!p.dead&&
            !p.HeldItem.IsAir&&p.HeldItem.pick>0&&(p.itemAnimation>0||p.controlUseItem)&&!p.mouseInterface&&WorldGen.InWorld(x,y,10)&&Main.tile[x,y].HasTile;
        int type=observe?Main.tile[x,y].TileType:-1;
        Vector2 aim=Main.MouseWorld;
        orig(p,x,y,power);
        if(!observe)return;
        if(CompanionNetwork.Client){CompanionNetwork.DigAt(new Point(x,y),type,aim);return;}
        var s=p.GetModPlayer<CompanionPlayer>();
        if(s.Directive.Active)return;
        if(s.Enabled&&!s.Paused&&!s.Vitals.Downed&&(CompanionNetwork.Server?s.NetDigging:ModContent.GetInstance<CompanionConfig>().DiggingAssist))
            s.Digging.Observe(p,new Point(x,y),type,aim);
    }
}
