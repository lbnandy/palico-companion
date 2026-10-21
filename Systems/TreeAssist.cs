using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Small, visible forestry jobs. Vanilla axe damage / KillTile / sapling placement,
// Replanting is best-effort after harvesting, never a prerequisite for felling.
public sealed class TreeAssist
{
    Point root;
    int cooldown,quiet,elapsed,cadence,interval;
    int replantTrunk,replantTicks;
    public bool ReplantPending=>replantTicks>0;
    Item axe;
    bool requested,requestedManual;
    int requestedType;
    public bool Requested => requested && (Active || ReplantPending);
    public int ObservedFinishes {get;private set;}
    readonly Item basicAxe=new(ItemID.CopperAxe); // Companion-owned tool; never inserted into an inventory.
    public bool Active {get;private set;}
    public Vector2 Target=>root.ToWorldCoordinates();
    public Vector2 MoveTarget {get;private set;}
    public int Harvests {get;private set;}
    public int Replants {get;private set;}
    public string Status {get;private set;}="";
    internal string Diagnostics=> $"active={Active} quiet={quiet} cooldown={cooldown} elapsed={elapsed} root={root} stand={MoveTarget} status={Status}";
    public int SwingTicks {get;private set;}
    public int SwingDuration {get;private set;}=20;
    public int SwingItemType {get;private set;}
    public Item SwingItem {get;private set;}
    public void UpdateVisual(){if(SwingTicks>0)SwingTicks--;}
    public void Cancel(){Active=false;requested=false;requestedManual=false;SwingTicks=cadence=elapsed=replantTicks=0;cooldown=120;Status="";}
    public void Suspend(){SwingTicks=cadence=0;Status=CompanionText.Message("TreeAssist.CombatPause");}
    public static bool FindRoot(int x,int y,out Point at) {
        at=default;
        if(!WorldGen.InWorld(x,y,10)||!Main.tile[x,y].HasTile||TreeSpecies.SeedType(Main.tile[x,y].TileType)==0)return false;
        WorldGen.GetTreeBottom(x,y,out int bx,out int by);at=new(bx,by-1);
        return Mature(at,out _);
    }
    public bool Assist(Player p,Point at,bool manual=false) {
        var s=p.GetModPlayer<CompanionPlayer>();
        if(!manual&&s.Directive.Active||Main.netMode==NetmodeID.MultiplayerClient||!s.Enabled||s.Paused||s.Vitals.Downed||
            !manual&&!(CompanionNetwork.Server?s.NetForestry:ModContent.GetInstance<CompanionConfig>().Forestry)||!Mature(at,out _))return false;
        if(requested&&Active&&root==at)return true; // Repeated axe hits must not reset the helper's swing.
        Cancel();root=at;requested=true;requestedManual=manual;requestedType=Main.tile[root].TileType;Active=true;cooldown=0;axe=BestAxe(p);
        s.Construction.Cancel();s.Mining.Cancel();s.Digging.Clear(s);s.Garden.Cancel();s.Storage.Cancel();s.Order.Cancel();
        MoveTarget=Target;Stand(s.Cat?.Center??p.Center,out var stand);if(stand!=default)MoveTarget=stand;
        Status=CompanionText.Message("TreeAssist.Helping");return true;
    }
    public void FailRoute(){Cancel();cooldown=600;Status=CompanionText.Message("TreeAssist.Blocked");}
    Item BestAxe(Player p) {
        Item best=basicAxe;
        for(int i=0;i<50;i++){var item=p.inventory[i];if(item==null||item.IsAir||item.axe<=0)continue;
            if(best==null||item.axe>best.axe||item.axe==best.axe&&item.useTime<best.useTime)best=item;}
        return best;
    }
    public static bool Mature(Point cell,out int top) {
        top=cell.Y;
        if(!WorldGen.InWorld(cell.X,cell.Y,50)||!Main.tile[cell].HasTile||TreeSpecies.SeedType(Main.tile[cell].TileType)==0||
            !TreeSpecies.Soil(cell.X,cell.Y+1,Main.tile[cell].TileType))return false;
        WorldGen.GetTreeBottom(cell.X,cell.Y,out int bx,out int by);
        if(bx!=cell.X||by!=cell.Y+1)return false; // Exclude side roots / branches.
        while(top>cell.Y-40&&Main.tile[cell.X,top-1].HasTile&&Main.tile[cell.X,top-1].TileType==Main.tile[cell].TileType)top--;
        return cell.Y-top>=4&&top>cell.Y-40;
    }
    public static bool Protected(Player p,Point at,int top) {
        // Protect a tree actually incorporated into a structure. Nearby chests,
        // residents, spawn points or wires do not make an unrelated tree decor.
        for(int y=top;y<=at.Y;y++) {
            var t=Main.tile[at.X,y];
            if(t.HasActuator||t.IsActuated||t.WallType!=WallID.None&&Main.wallHouse[t.WallType])return true;
        }
        return false; // Sapling room is checked after harvesting, not a felling requirement.
    }
    bool Stand(Vector2 from,out Vector2 spot) {
        spot=from;float best=float.MaxValue;
        for(int dx=-3;dx<=3;dx++)for(int dy=-2;dy<=2;dy++) {
            Vector2 c=new((root.X+dx)*16+8,(root.Y+1+dy)*16-CompanionNavigation.Height/2f);
            if(!CompanionNavigation.TryStand(c,out c)||Vector2.DistanceSquared(c,Target)>56*56||!Reachability.Clear(c,Target))continue;
            float d=Vector2.DistanceSquared(from,c);if(d<best){best=d;spot=c;}
        }
        return best<float.MaxValue;
    }
    public bool Tick(Player p,CompanionPlayer s,Projectile cat,bool danger) {
        if(Main.netMode==NetmodeID.MultiplayerClient||!requestedManual&&!(CompanionNetwork.Server?s.NetForestry:ModContent.GetInstance<CompanionConfig>().Forestry)||!s.Enabled||s.Paused||s.Vitals.Downed||
            s.Mining.Active||s.Order.Active||s.Order.HasPreview||s.Storage.Active||s.Garden.Active){if(Active||ReplantPending)Cancel();quiet=0;return false;}
        if(danger){if(Active||ReplantPending)Suspend();quiet=0;return false;}
        if(requested&&Active&&!ReplantPending&&!Main.tile[root].HasTile){
            // The owner or another helper finished it. Keep real drops, and only
            // attempt a seed-backed replant; never credit a second tree harvest.
            Active=false;replantTrunk=requestedType;replantTicks=180;ObservedFinishes++;
        }
        if(ReplantPending)return TryReplant(p,s,cat);
        if(cooldown>0){cooldown--;return false;}
        if(!Active) {
            if(!CompanionWorkOpportunity.KeepingUp(p,cat)){Status="";return false;}
            cooldown=60;Status="";axe=BestAxe(p);
            Point center=p.Center.ToTileCoordinates();float best=320*320;
            for(int x=center.X-20;x<=center.X+20;x++)for(int y=center.Y-7;y<=center.Y+8;y++) {
                Point cell=new(x,y);
                if(!Mature(cell,out int top)||TreeSpecies.Decorative(Main.tile[cell].TileType)||Protected(p,cell,top)||!CompanionWorkOpportunity.RemainsNearby(p,cell.ToWorldCoordinates(),320))continue;
                float d=Vector2.DistanceSquared(p.Center,cell.ToWorldCoordinates());if(d>=best)continue;
                Point previous=root;root=cell;if(!Stand(cat.Center,out var stand)){root=previous;continue;}best=d;MoveTarget=stand;Active=true;
            }
            if(!Active)return false;cooldown=elapsed=cadence=0;
        }
        var current=BestAxe(p);
        if(!ReferenceEquals(axe,current)){axe=current;cadence=SwingTicks=0;}
        if(++elapsed>600||axe==null||Vector2.DistanceSquared(p.Center,Target)>320*320||
            !Mature(root,out int treeTopY)||(!requested&&Protected(p,root,treeTopY))) {Cancel();return false;}
        if(Vector2.DistanceSquared(cat.Center,Target)>56*56||!Reachability.Clear(cat.Center,Target)) {
            cadence=0;if(elapsed%15==0&&Stand(cat.Center,out var stand))MoveTarget=stand;
            Status=s.Status=CompanionText.Message("TreeAssist.Approaching");return true;
        }
        MoveTarget=cat.Center;Status=s.Status=CompanionText.Message("TreeAssist.Chopping");
        (cat.ModProjectile as CompanionCat)?.FaceWork(Target);
        if(cadence==0)interval=Math.Clamp(CombinedHooks.TotalUseTime(axe.useTime,p,axe),1,300);
        int windup=Math.Max(1,(int)(interval*0.6f));cadence++;
        if(cadence==interval-windup+1){SwingDuration=Math.Max(1,(int)Math.Ceiling(windup/0.6f));SwingTicks=SwingDuration;SwingItemType=axe.type;SwingItem=axe;CompanionItemVisual.BeginUse(axe,cat.Center);}
        if(cadence<interval)return true;cadence=0;
        if(!WorldGen.CanKillTile(root.X,root.Y)){Cancel();return false;}
        int hit=p.hitTile.HitObject(root.X,root.Y,1);
        int damage=Math.Max(1,(int)(axe.axe*1.2f));if(Main.getGoodWorld)damage=(int)(damage*1.3f);
        if(p.hitTile.AddDamage(hit,damage)<100){using(CompanionTileActions.Begin())WorldGen.KillTile(root.X,root.Y,fail:true);return true;}
        replantTrunk=Main.tile[root].TileType;
        p.hitTile.Clear(hit);using(CompanionTileActions.Begin())using(new HarvestBasket(s))WorldGen.KillTile(root.X,root.Y);
        // Mature returns an absolute Y coordinate, not a height. TileSquare
        // stores dimensions as bytes; a world Y here corrupts the row stride
        // on clients and paints unrelated tile data beside the felled tree.
        if(CompanionNetwork.Server)NetMessage.SendTileSquare(-1,root.X-4,treeTopY-2,9,root.Y-treeTopY+5);
        if(!Main.tile[root].HasTile) {
            Harvests++;s.Experience++;
            Cancel();cooldown=300;replantTicks=180;
            return TryReplant(p,s,cat);
        }
        Cancel();cooldown=300;return false;
    }
    bool TryReplant(Player p,CompanionPlayer s,Projectile cat){
        Active=false;
        if(Vector2.DistanceSquared(p.Center,Target)>320*320){replantTicks=0;return false;}
        var seed=TreeSpecies.FindSeed(p,s,replantTrunk);
        if(seed!=null&&Vector2.DistanceSquared(cat.Center,Target)<=56*56&&Reachability.Clear(cat.Center,Target)){
            replantTicks=0;
            foreach(int x in new[]{root.X,root.X-1}){
                if(!TreeSpecies.Plant(x,root.Y,seed))continue;
                if(cat.ModProjectile is CompanionCat visual)visual.ActionVisual.Begin(visual,CompanionGesture.Plant,seed.type);
                CompanionConsumption.Consume(s,seed);Replants++;
                if(CompanionNetwork.Server)NetMessage.SendTileSquare(-1,x-1,root.Y-2,4,4);
                s.Expression.Say(s,cat,"Tree",Terraria.GameContent.UI.EmoteID.EmoteHappiness);
                s.Report(CompanionText.Message("TreeAssist.Replanted"));return false;
            }
            s.Report(CompanionText.Message("TreeAssist.ReplantBlocked"));return false;
        }
        // Give ordinary pickup a short chance to collect this tree's own drops.
        // No movement task while waiting for seeds. Once acquired, briefly
        // approach the stump; danger, player movement and higher jobs cancel it.
        if(--replantTicks==0){s.Report(seed==null?CompanionText.Message("TreeAssist.NoAcorn"):CompanionText.Message("TreeAssist.LeftTree"));return false;}
        if(seed!=null&&Stand(cat.Center,out var stand)){
            Active=true;MoveTarget=stand;Status=s.Status=CompanionText.Message("TreeAssist.Replanting");return true;
        }
        return false;
    }
}
