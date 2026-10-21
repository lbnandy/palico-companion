using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
namespace PalicoCompanion.Systems;

// Local breadcrumb routing, informed by TerraGuardians' bounded path search.
// This is a separate whole-body flight solver, not its ground/jump simulation.
// Nothing moves the projectile here; native collision still owns movement.
internal sealed class CompanionFlightRoute {
    internal const int SearchBudget=384;
    readonly List<Vector2> route=new();
    Vector2 target,failedFrom;
    ulong retryAt,expires;
    public int Searches {get;private set;}
    public int Detours {get;private set;}
    public int LastExpanded {get;private set;}
    public void Clear(){route.Clear();retryAt=expires=0;}
    public bool Next(Vector2 from,Vector2 goal,out Vector2 next) {
        next=goal;
        if(!CompanionNavigation.Fits(from)||!CompanionNavigation.Fits(goal)||Vector2.DistanceSquared(from,goal)>640*640)return false;
        if(CompanionNavigation.ClearFlight(from,goal)){Clear();return true;}
        bool changed=Vector2.DistanceSquared(goal,target)>16*16;
        if(changed||Main.GameUpdateCount>=expires)route.Clear();
        if(route.Count>0) {
            // A newly placed block also invalidates the still-cached route.
            for(int i=route.Count-1;i>=0;i--)if(Vector2.DistanceSquared(from,route[i])>4&&CompanionNavigation.ClearFlight(from,route[i])) {
                next=route[i];if(i>0)route.RemoveRange(0,i);return true;
            }
            route.Clear();retryAt=0;
        }
        if(!changed&&Vector2.DistanceSquared(from,failedFrom)<32*32&&Main.GameUpdateCount<retryAt)return false;
        target=goal;failedFrom=from;retryAt=Main.GameUpdateCount+30;expires=Main.GameUpdateCount+120;
        if(!Search(from,goal))return false;
        Detours++;next=route[0];return true;
    }
    bool Search(Vector2 from,Vector2 goal) {
        Searches++;LastExpanded=0;
        var open=new PriorityQueue<Point,float>();
        var cost=new Dictionary<Point,float>{{Point.Zero,0}};
        var parent=new Dictionary<Point,Point>();
        var closed=new HashSet<Point>();
        open.Enqueue(Point.Zero,Vector2.Distance(from,goal));
        Vector2 min=Vector2.Min(from,goal)-new Vector2(96),max=Vector2.Max(from,goal)+new Vector2(96);
        while(open.Count>0&&LastExpanded<SearchBudget) {
            var cell=open.Dequeue();if(!closed.Add(cell))continue;LastExpanded++;
            var at=from+new Vector2(cell.X*16,cell.Y*16);
            if(Vector2.DistanceSquared(at,goal)<=80*80&&CompanionNavigation.ClearFlight(at,goal)) {
                route.Add(goal);
                for(var p=cell;p!=Point.Zero;p=parent[p])route.Add(from+new Vector2(p.X*16,p.Y*16));
                route.Reverse();return true;
            }
            for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++) {
                if(dx==0&&dy==0)continue;
                var adj=new Point(cell.X+dx,cell.Y+dy);if(closed.Contains(adj))continue;
                var pos=from+new Vector2(adj.X*16,adj.Y*16);
                if(pos.X<min.X||pos.Y<min.Y||pos.X>max.X||pos.Y>max.Y)continue;
                float score=cost[cell]+(dx!=0&&dy!=0?22.627417f:16);
                if(cost.TryGetValue(adj,out float old)&&old<=score)continue;
                if(!CompanionNavigation.ClearFlight(at,pos))continue;
                cost[adj]=score;parent[adj]=cell;open.Enqueue(adj,score+Vector2.Distance(pos,goal));
            }
        }
        return false;
    }
}
