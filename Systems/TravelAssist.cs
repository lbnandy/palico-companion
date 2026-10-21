using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
namespace PalicoCompanion.Systems;

public sealed class TravelAssist {
    int cooldown;
    public string Status {get;private set;}="";
    public int Platforms {get;private set;}
    public int Ropes {get;private set;}
    public bool Active {get;private set;}
    public Vector2 MoveTarget {get;private set;}
    public void ClearGoal()=>Active=false;
    static bool Stand(int x,int y)=>WorldGen.InWorld(x,y,12)&&Main.tile[x,y].HasTile&&!Main.tile[x,y].IsActuated&&
        (WorldGen.SolidTile(x,y)||TileID.Sets.Platforms[Main.tile[x,y].TileType]);
    static bool Rope(int x,int y)=>WorldGen.InWorld(x,y,12)&&Main.tile[x,y].HasTile&&Main.tileRope[Main.tile[x,y].TileType];
    static bool Empty(int x,int y)=>WorldGen.InWorld(x,y,12)&&!Main.tile[x,y].HasTile;
    public static Item[] Materials(Player p,CompanionPlayer s,int type) {
        bool Available(Item i)=>!i.IsAir&&!i.favorited&&i.type==type;
        if(Array.Exists(s.Bag,Available))return s.Bag;
        if(CompanionNetwork.Server){CompanionNetwork.RequestMaterial(s,type);return null;}
        for(int i=0;i<50;i++)if(Available(p.inventory[i]))return p.inventory;
        return null;
    }
    // Bounded, same-height short crossing only. Never fills a shaft without a bank.
    public static bool BridgeTarget(Player p,out Point target) {
        target=default;
        if(Math.Abs(p.velocity.Y)>0.1f||p.controlDown)return false;
        int x=(int)(p.Center.X/16),y=(int)((p.Bottom.Y+2)/16),dir=p.direction;
        if(!Stand(x,y))return false;
        int gap=0;
        for(int n=1;n<=6;n++) {
            int tx=x+dir*n;
            if(!WorldGen.InWorld(tx,y,12))return false;
            if(Stand(tx,y)) {if(gap>0){target=new Point(x+dir*gap,y);return true;}continue;}
            if(!Empty(tx,y))return false;
            if(gap==0)gap=n;
        }
        return false;
    }
    // Extend an existing descent line, or start one beside a ledge on explicit down input.
    // No speculative rope walls while merely walking on the surface.
    public static bool RopeTarget(Player p,out Point target) {
        target=default;if(!p.controlDown&&p.velocity.Y<=1)return false;
        int px=(int)(p.Center.X/16),py=(int)(p.Bottom.Y/16);
        for(int dx=-1;dx<=1;dx++)for(int dy=-3;dy<=1;dy++) {
            int x=px+dx,y=py+dy;
            if(!Rope(x,y))continue;
            for(int n=1;n<=5;n++) {
                if(Rope(x,y+n))continue;
                if(Empty(x,y+n)&&Empty(x,y+n+1)){target=new Point(x,y+n);return true;}break;
            }
        }
        if(!p.controlDown||Math.Abs(p.velocity.X)>1)return false;
        int edge=px+p.direction;
        if(!Stand(px,py)||!Empty(edge,py)||!Empty(edge,py+1)||!Empty(edge,py+2)||!Empty(edge,py+3))return false;
        target=new Point(edge,py);return true;
    }
    public void Tick(Player p,CompanionPlayer s,Projectile cat) {
        Active=false;
        if(cooldown>0)cooldown--;
        Status="";
        if(Vector2.DistanceSquared(p.Center,cat.Center)>160*160||p.itemAnimation>0)return;
        Point target;int type;
        if(s.Ropes&&RopeTarget(p,out target))type=ItemID.Rope;
        else if(s.Bridges&&BridgeTarget(p,out target))type=ItemID.WoodPlatform;
        else return;
        var materials=Materials(p,s,type);
        if(materials==null){Status=type==ItemID.Rope?CompanionText.Message("TravelAssist.NoRope"):CompanionText.Message("TravelAssist.NoPlatform");cooldown=60;return;}
        Vector2 point=target.ToWorldCoordinates();
        Active=true;MoveTarget=cat.Center;
        if(Vector2.DistanceSquared(cat.Center,point)>72*72||!SafeConstruction.CanReach(cat.Center,point,type)) {
            // Step onto a real bank or an already placed platform; never walk to an empty gap.
            float best=float.MaxValue;
            for(int dx=-4;dx<=4;dx++)for(int dy=-4;dy<=2;dy++) {
                int sx=target.X+dx,sy=target.Y+dy;
                if(!Stand(sx,sy))continue;
                Vector2 stand=new(sx*16+8,sy*16-cat.height/2f);
                if(Vector2.DistanceSquared(stand,point)>72*72||!CompanionNavigation.Fits(stand)||
                   !SafeConstruction.CanReach(stand,point,type)||!Reachability.Clear(cat.Center,stand))continue;
                float score=Vector2.DistanceSquared(cat.Center,stand);
                if(score<best){best=score;MoveTarget=stand;}
            }
            Status=best<float.MaxValue?CompanionText.Message("TravelAssist.Approaching"):CompanionText.Message("TravelAssist.NoFooting");
            if(best==float.MaxValue)Active=false;
            return;
        }
        (cat.ModProjectile as CompanionCat)?.FaceWork(point);
        if(cooldown>0)return;
        cooldown=15;
        Status=SafeConstruction.TryPlace(p,cat.Center,materials,target.X,target.Y,type);
        if(Status.StartsWith(SafeConstruction.SuccessPrefix)) {
            if(type==ItemID.Rope)Ropes++;else Platforms++;
            s.Expression.Say(s,cat,"Build",Terraria.GameContent.UI.EmoteID.EmoteHappiness);
        }
    }
}
