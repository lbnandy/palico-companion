using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
namespace PalicoCompanion.Systems;

// A short continuation of actual successful placements, never a guessed house
// plan. TileLoader and WorldGen still decide whether each cell can be placed.
public sealed class ConstructionAssist {
    Point last,step,next;int type,streak,left,cadence,elapsed;ulong lastPlacement;
    public bool Active=>left>0;
    public Vector2 Target=>next.ToWorldCoordinates();
    public Vector2 MoveTarget {get;private set;}
    public static bool Supported(Item item)=>item.createTile>=TileID.Dirt&&item.createTile<Main.tileRope.Length&&
        (TileID.Sets.Platforms[item.createTile]||Main.tileRope[item.createTile]);
    public void Cancel(){left=streak=cadence=elapsed=0;type=0;}
    public void Observe(Player p,Point at,int itemType){
        var s=p.GetModPlayer<CompanionPlayer>();
        if(s.Directive.Active||!s.Enabled||s.Paused||s.Vitals.Downed||!ContentSamples.ItemsByType.TryGetValue(itemType,out var item)||!Supported(item))return;
        bool platform=TileID.Sets.Platforms[item.createTile];
        if(platform?!s.Bridges:!s.Ropes)return;
        var tile=Main.tile[at];if(!tile.HasTile||tile.TileType!=item.createTile)return;
        Point delta=new(at.X-last.X,at.Y-last.Y);
        bool aligned=platform?delta.Y==0&&Math.Abs(delta.X)==1:delta.X==0&&Math.Abs(delta.Y)==1;
        if(type!=itemType||Main.GameUpdateCount-lastPlacement>180||!aligned){Cancel();type=itemType;streak=1;}
        else if(streak<2||step==delta){step=delta;streak++;}
        else{left=0;step=delta;streak=2;}
        last=at;lastPlacement=Main.GameUpdateCount;
        if(streak<3)return;
        s.Mining.Cancel();s.Digging.Clear(s);s.Trees.Cancel();s.Garden.Cancel();s.Storage.Cancel();
        next=new(at.X+step.X,at.Y+step.Y);left=3;cadence=elapsed=0;
    }
    public bool Tick(Player p,CompanionPlayer s,Projectile cat,bool danger){
        if(!Active)return false;
        if(s.Directive.Active||!s.Enabled||s.Paused||s.Vitals.Downed||p.dead||p.HeldItem.type!=type||s.Mining.Active||s.Digging.Active||s.Trees.Requested||s.Order.Active){Cancel();return false;}
        var sample=ContentSamples.ItemsByType[type];
        if(TileID.Sets.Platforms[sample.createTile]?!s.Bridges:!s.Ropes){Cancel();return false;}
        if(Vector2.DistanceSquared(p.Center,Target)>480*480){Cancel();return false;}
        if(danger){cadence=0;return false;}
        if(++elapsed>600){Cancel();return false;}
        while(left>0&&WorldGen.InWorld(next.X,next.Y,10)&&Main.tile[next].HasTile){
            if(Main.tile[next].TileType!=sample.createTile){Cancel();return false;}
            left--;next=new(next.X+step.X,next.Y+step.Y);
        }
        if(left<=0)return false;
        var material=Array.Find(s.Bag,i=>!i.IsAir&&!i.favorited&&i.type==type);
        if(material==null){Cancel();return false;}
        bool reachable=Vector2.DistanceSquared(cat.Center,Target)<=MathF.Pow(128,2)&&SafeConstruction.CanReach(cat.Center,Target,type);
        MoveTarget=reachable?cat.Center:Target-new Vector2(0,24);
        s.Status=CompanionText.Message("ConstructionAssist.HelpingBuild");
        if(!reachable){cadence=0;return true;}
        (cat.ModProjectile as CompanionCat)?.FaceWork(Target);
        if(++cadence<Math.Max(4,(int)Math.Ceiling(CombinedHooks.TotalUseTime(material.useTime,p,material)/s.Training.WorkSpeed(12))))return true;
        cadence=0;
        string result=SafeConstruction.TryPlace(p,cat.Center,s.Bag,next.X,next.Y,type,ownerRequested:true);
        if(!result.StartsWith(SafeConstruction.SuccessPrefix,StringComparison.Ordinal)){Cancel();return false;}
        left--;elapsed=0;next=new(next.X+step.X,next.Y+step.Y);return Active;
    }
}

public sealed class ConstructionObservation : GlobalTile {
    public override void PlaceInWorld(int i,int j,int tileType,Item item){
        if(Main.netMode==NetmodeID.Server||CompanionNativeUse.Current!=null||!ConstructionAssist.Supported(item))return;
        var p=Main.LocalPlayer;if(!p.active||p.dead||p.HeldItem.type!=item.type)return;
        if(CompanionNetwork.Client)CompanionNetwork.BuiltAt(i,j,item.type);
        else p.GetModPlayer<CompanionPlayer>().Construction.Observe(p,new Point(i,j),item.type);
    }
}
