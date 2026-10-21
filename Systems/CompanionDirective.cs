using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace PalicoCompanion.Systems;

internal enum DirectiveKind:byte { None, Mine, Tree, Collect, Fish }

// A transient priority over existing workers. No second mining/fishing/drop implementation.
internal sealed class CompanionDirective {
    internal DirectiveKind Kind;
    internal Point Cell;
    internal bool Active=>Kind!=DirectiveKind.None;
    readonly Dictionary<Item,int> drops=new();
    int idleTicks;
    internal bool Selects(Item item)=>drops.TryGetValue(item,out int type)&&item.type==type;
    internal void Clear(CompanionPlayer s,bool workers=true) {
        var old=Kind;Kind=DirectiveKind.None;drops.Clear();idleTicks=0;
        if(!workers)return;
        if(old==DirectiveKind.Mine)s.Mining.Cancel();
        if(old==DirectiveKind.Tree)s.Trees.Cancel();
        if(old==DirectiveKind.Fish&&s.Cat?.ModProjectile is CompanionCat cat){cat.Fishing.Cancel();cat.Fishing.RequestedWater=null;}
    }
    internal static DirectiveKind Identify(CompanionPlayer s,Vector2 point,out Point cell) {
        cell=point.ToTileCoordinates();if(!WorldGen.InWorld(cell.X,cell.Y,10))return DirectiveKind.None;
        // Match Main.DrawMouseOver: visible sprites can be much larger than
        // their physics hitboxes. A directly pointed drop takes precedence
        // over the tree or ore behind it.
        foreach(var item in Main.item){
            if(!item.active||item.IsAir)continue;
            var draw=Item.GetDrawHitbox(item.type,null);
            var box=new Rectangle((int)(item.Bottom.X-draw.Width*.5f),(int)(item.Bottom.Y-draw.Height),draw.Width,draw.Height);
            if(box.Contains(point.ToPoint())){cell=item.Center.ToTileCoordinates();return DirectiveKind.Collect;}
        }
        var tile=Main.tile[cell];
        if(tile.HasTile&&FieldAssist.AllowedOre(tile.TileType))return DirectiveKind.Mine;
        if(TreeAssist.FindRoot(cell.X,cell.Y,out var root)){cell=root;return DirectiveKind.Tree;}
        // Show a target even while a new drop's native noGrabDelay is counting
        // down. The authority still validates ownership, blacklist and capacity.
        if(Main.item.Any(i=>i.active&&!i.IsAir&&Vector2.DistanceSquared(i.Center,point)<40*40))return DirectiveKind.Collect;
        if(CompanionFishing.TrySurface(cell,out var surface)){cell=surface;return DirectiveKind.Fish;}
        return DirectiveKind.None;
    }
    internal static string Label(DirectiveKind kind)=>kind switch {
        DirectiveKind.Mine=>CompanionText.Get("CompanionDirective.Mine"),DirectiveKind.Tree=>CompanionText.Get("CompanionDirective.Tree"),DirectiveKind.Collect=>CompanionText.Get("CompanionDirective.Collect"),DirectiveKind.Fish=>CompanionText.Get("CompanionDirective.Fish"),_=>CompanionText.Get("CompanionDirective.AimHint")
    };
    internal bool Request(CompanionPlayer s,DirectiveKind kind,Point cell,out string message) {
        message=CompanionText.Message("CompanionDirective.CannotWork");
        if(CompanionNetwork.Client||!s.Enabled||s.Stowed||s.Paused||s.Player.dead||s.Vitals.Downed||s.Cat?.ModProjectile is not CompanionCat cat)return false;
        message=CompanionText.Message("CompanionDirective.TooFar");
        if(!WorldGen.InWorld(cell.X,cell.Y,10)||Vector2.DistanceSquared(cell.ToWorldCoordinates(),s.Player.Center)>480*480)return false;
        if(Kind==kind&&Cell==cell){message=CompanionText.Message("CompanionDirective.AlreadyWorking");return true;}
        FieldAssist mining=null;
        var selected=new List<Item>();
        switch(kind) {
            case DirectiveKind.Mine:
                mining=new();message=mining.Request(s.Player,cell.ToWorldCoordinates(),true,veinLimit:2048);
                if(!mining.Active)return false;break;
            case DirectiveKind.Tree:
                message=CompanionText.Message("CompanionDirective.CannotChop");
                if(!TreeAssist.Mature(cell,out var top)||TreeAssist.Protected(s.Player,cell,top)||Vector2.DistanceSquared(cell.ToWorldCoordinates(),s.Player.Center)>320*320)return false;
                break;
            case DirectiveKind.Collect:
                selected.AddRange(Main.item.Where(i=>i.active&&!i.IsAir&&Vector2.DistanceSquared(i.Center,cell.ToWorldCoordinates())<112*112&&CompanionPickup.CanCollect(i,s.Player,s,false)));
                message=CompanionText.Message("CompanionDirective.NoItems");if(selected.Count==0)return false;
                message=CompanionText.Message("CompanionDirective.BagFull");if(!selected.Any(i=>InventoryTransfer.HasRoom(i,s.Bag)))return false;break;
            case DirectiveKind.Fish:
                message=CompanionText.Message("CompanionDirective.NoBait");if(!s.Bag.Any(CompanionFishing.SafeBait))return false;
                message=CompanionText.Message("CompanionDirective.NoWater");
                if(!CompanionFishing.TrySurface(cell,out cell))return false;
                if(!cat.Fishing.CanRequest(s,cell)){message=CompanionText.Message("CompanionDirective.FishingBlocked");return false;}
                break;
            default:message=CompanionText.Message("CompanionDirective.NoTarget");return false;
        }
        Clear(s);cat.StopWork(s);s.MiningHelper=new();s.Digging.Clear(s);
        Kind=kind;Cell=cell;idleTicks=0;
        if(kind==DirectiveKind.Mine)s.Mining=mining;
        if(kind==DirectiveKind.Tree&&!s.Trees.Assist(s.Player,cell,true)){Clear(s);message=CompanionText.Message("CompanionDirective.TreeBlocked");return false;}
        if(kind==DirectiveKind.Collect){cat.Pickup.BeginSelection();foreach(var i in selected)drops[i]=i.type;}
        if(kind==DirectiveKind.Fish)cat.Fishing.RequestedWater=cell;
        message=Label(kind);return true;
    }
    // Returns true while the worker owns movement. Danger is handled by the cat's
    // existing combat branch; its target and progress survive the interruption.
    internal bool Tick(CompanionPlayer s,CompanionCat cat,bool danger,out Vector2 goal) {
        goal=cat.Projectile.Center;if(!Active)return false;
        if(!s.Enabled||s.Stowed||s.Player.dead||s.Vitals.Downed){Clear(s);return false;}
        if(Vector2.DistanceSquared(s.Player.Center,Cell.ToWorldCoordinates())>480*480){Clear(s);s.Report(CompanionText.Message("CompanionDirective.LeftWorksite"));return false;}
        if(danger){if(Kind==DirectiveKind.Fish)cat.Fishing.Cancel();return false;}
        if(s.Paused)return false;
        string done=null;
        switch(Kind) {
            case DirectiveKind.Mine:
                s.Mining.Tick(s.Player,cat.Projectile.Center,false,false);goal=s.Mining.MoveTarget;s.Status=s.Mining.Status;
                if(!s.Mining.Active)done=s.Mining.Failed?s.Mining.Status:CompanionText.Message("CompanionDirective.MineDone");break;
            case DirectiveKind.Tree:
                bool working=s.Trees.Tick(s.Player,s,cat.Projectile,false);goal=s.Trees.MoveTarget;s.Status=s.Trees.Status;
                if(!working&&!s.Trees.Active&&!s.Trees.ReplantPending)done=CompanionText.Message("CompanionDirective.TreeDone");break;
            case DirectiveKind.Collect:
                int target=cat.Pickup.Tick(s.Player,s,cat.Projectile,ref goal,true,Selects);
                var remaining=drops.Where(e=>e.Key.active&&!e.Key.IsAir&&e.Key.type==e.Value&&Main.item.Contains(e.Key)).Select(e=>e.Key).ToArray();
                if(remaining.Length==0)done=CompanionText.Message("CompanionDirective.CollectDone");
                else if(!remaining.Any(i=>InventoryTransfer.HasRoom(i,s.Bag)))done=CompanionText.Message("CompanionDirective.CollectFull");
                else if(target<0&&cat.Pickup.PulledThisTick==0){if(++idleTicks>=60)done=CompanionText.Message("CompanionDirective.CollectBlocked");}
                else idleTicks=0;
                break;
            case DirectiveKind.Fish:
                // Land a flying worker once, on a dry stand. Calling HoldReturn
                // every grounded tick erases route progress; in water it also
                // disables collision and lets the cat fall through the lake bed.
                if(cat.Navigation.Riding&&CompanionNavigation.Fits(cat.Projectile.Center)&&CompanionNavigation.Supported(cat.Projectile.position,cat.Projectile.width,cat.Projectile.height))cat.Navigation.HoldReturn(cat.Projectile);
                bool fishing=cat.Fishing.Tick(s.Player,s,cat.Projectile);
                goal=cat.Fishing.Active?cat.Projectile.Center:cat.Fishing.MoveTarget;
                if(!s.Bag.Any(CompanionFishing.SafeBait))done=CompanionText.Message("CompanionDirective.OutOfBait");
                else if(cat.Fishing.WaitingForOverflow(s))done=CompanionText.Message("CompanionDirective.CatchFull");
                else if(!fishing){goal=cat.Projectile.Center;if(++idleTicks>180)done=CompanionText.Message("CompanionDirective.CannotCast");}
                else idleTicks=0;
                break;
        }
        if(done!=null){Clear(s);s.Report(done);s.Status=done;}
        return true;
    }
}
