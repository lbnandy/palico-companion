using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.GameContent.UI;
namespace PalicoCompanion.Systems;

// Local observation, never chest-content inspection or terrain excavation.
public sealed class CompanionDiscovery {
    const int Radius=16,MemoryLimit=4096;
    int clock;
    readonly HashSet<string> known=new();
    readonly Queue<string> order=new();
    internal sealed record Marker(Point Tile,int Type,NPC Npc,ulong Until);
    readonly List<Marker> markers=new();
    ulong visualTick=ulong.MaxValue;
    internal IReadOnlyList<Marker> Markers=>markers;
    internal Marker Mark(Point tile,int type,NPC npc=null){
        markers.RemoveAll(m=>m.Tile==tile&&m.Type==type&&m.Npc==npc);
        if(markers.Count==4)markers.RemoveAt(0);
        var marker=new Marker(tile,type,npc,Main.GameUpdateCount+360);markers.Add(marker);return marker;
    }
    internal bool MarkerBounds(Marker marker,Player owner,ulong now,out Rectangle bounds){
        bounds=default;if(now>=marker.Until)return false;
        if(marker.Npc is {} npc){
            if(!Rescue(npc)||npc.type!=marker.Type||!ReferenceEquals(Main.npc[npc.whoAmI],npc))return false;
            bounds=npc.Hitbox;return true;
        }
        if(!Candidate(marker.Tile.X,marker.Tile.Y,out var at,out int type)||at!=marker.Tile||type!=marker.Type)return false;
        if(HandledChest(at,type))return false;
        if(owner.chest>=0&&owner.chest<Main.maxChests&&Main.chest[owner.chest] is {} chest&&chest.x==at.X&&chest.y==at.Y)return false;
        bounds=new Rectangle(at.X*16,at.Y*16,32,32);return true;
    }
    public int Reports {get;private set;}
    public static string Key(string world,Point tile,int type)=>world+"|"+type+"|"+tile.X+"|"+tile.Y;
    public bool Knows(string key)=>known.Contains(key);
    public void Remember(string key) {
        if(!known.Add(key))return;order.Enqueue(key);
        while(order.Count>MemoryLimit)known.Remove(order.Dequeue());
    }
    public TagCompound Save()=>new(){["known"]=order.ToList()};
    public void Load(TagCompound tag) {
        known.Clear();order.Clear();markers.Clear();clock=0;Reports=0;
        foreach(string key in tag.GetList<string>("known").TakeLast(MemoryLimit))if(key.Length<128)Remember(key);
    }
    public void ResetSession(){clock=0;Reports=0;markers.Clear();}
    public static bool Candidate(int x,int y,out Point topLeft,out int type) {
        topLeft=default;type=0;if(!WorldGen.InWorld(x,y,2))return false;
        var tile=Main.tile[x,y];if(!tile.HasTile||tile.IsActuated)return false;
        type=tile.TileType;
        if(type!=TileID.Heart&&type!=TileID.LifeFruit&&type!=TileID.PlanteraBulb&&type!=TileID.Containers&&type!=TileID.Containers2)return false;
        topLeft=new Point(x-tile.TileFrameX%36/18,y-tile.TileFrameY%36/18);
        if(type==TileID.Containers||type==TileID.Containers2)return Chest.FindChest(topLeft.X,topLeft.Y)>=0;
        return true;
    }
    public static bool Rescue(NPC n)=>n.active&&(n.type==NPCID.BoundGoblin||n.type==NPCID.BoundWizard||n.type==NPCID.BoundMechanic||n.type==NPCID.WebbedStylist||n.type==NPCID.GolferRescue);
    internal static bool HandledChest(Point at,int type)=>(type==TileID.Containers||type==TileID.Containers2)&&DiscoveryChests.Contains(at);
    public static bool Visible(Vector2 observer,Point tile) {
        Vector2 center=tile.ToWorldCoordinates(16,16);
        if(Vector2.DistanceSquared(observer,center)>Radius*16*Radius*16)return false;
        // Nearby treasure sensing also works through cave walls.
        return true;
    }
    public static string Direction(Vector2 offset) {
        string horizontal=offset.X < -24?"Left":offset.X>24?"Right":"";
        string vertical=offset.Y < -24?"Up":offset.Y>24?"Down":"";
        string direction=horizontal+vertical;
        return direction.Length==0?"Near":direction;
    }
    public void Tick(CompanionPlayer s,Projectile cat) {
        bool Notify(string key,int emote,string context)=>s.Expression.TrySay(s,cat,key,emote,requireChat:true,context:context)||
            !CompanionExpression.ChatEnabled(s)&&!CompanionExpression.EmotesEnabled(s);
        markers.RemoveAll(m=>!MarkerBounds(m,s.Player,Main.GameUpdateCount,out _));
        if(visualTick!=Main.GameUpdateCount&&Main.GameUpdateCount%10==0){
            visualTick=Main.GameUpdateCount;
            foreach(var marker in markers)DiscoveryIndicator.Emit(s,marker);
        }
        if(Main.netMode==NetmodeID.Server||!s.Enabled)return;
        string world=Main.ActiveWorldFileData.UniqueId.ToString();
        if(s.Player.chest>=0&&Main.chest[s.Player.chest] is {} opened&&Candidate(opened.x,opened.y,out var openedTile,out int openedType))
            Remember(Key(world,openedTile,openedType));
        if(s.Paused||s.Vitals.Downed||!ModContent.GetInstance<CompanionConfig>().Discoveries||++clock%60!=0)return;
        var origin=s.Player.Center.ToTileCoordinates();
        Point best=default;int bestType=0;float distance=float.MaxValue;
        for(int x=origin.X-Radius;x<=origin.X+Radius;x++)for(int y=origin.Y-Radius;y<=origin.Y+Radius;y++) {
            if(!Candidate(x,y,out var cell,out int type)||cell.X!=x||cell.Y!=y||Knows(Key(world,cell,type)))continue;
            if(HandledChest(cell,type))continue;
            if(!Visible(cat.Center,cell)||!Visible(s.Player.Center,cell))continue;
            float d=Vector2.DistanceSquared(s.Player.Center,cell.ToWorldCoordinates(16,16));
            if(d<distance){distance=d;best=cell;bestType=type;}
        }
        NPC rescue=null;
        foreach(var npc in Main.npc) {
            if(!Rescue(npc)||Knows(Key(world,Point.Zero,-npc.type)))continue;
            float d=Vector2.DistanceSquared(s.Player.Center,npc.Center);
            if(d>Radius*16*Radius*16||Vector2.DistanceSquared(cat.Center,npc.Center)>Radius*16*Radius*16)continue;
            if(d<distance){distance=d;rescue=npc;}
        }
        if(rescue!=null) {
            Vector2 offset=rescue.Center-s.Player.Center;
            string context=Language.GetTextValue("Mods.PalicoCompanion.Discovery.Hint",Language.GetTextValue("Mods.PalicoCompanion.Discovery."+Direction(offset)),(int)Math.Ceiling(offset.Length()/16));
            if(Notify("FoundRescue",EmoteID.EmoteConfused,context)) {
                Remember(Key(world,Point.Zero,-rescue.type));DiscoveryIndicator.Emit(s,Mark(Point.Zero,rescue.type,rescue),4);Reports++;s.Report(context+"："+rescue.FullName);
            }
            return;
        }
        if(bestType==0)return;
        string eventKey=bestType==TileID.Heart?"FoundCrystal":bestType==TileID.LifeFruit?"FoundFruit":bestType==TileID.PlanteraBulb?"FoundBulb":"FoundChest";
        Vector2 delta=best.ToWorldCoordinates(16,16)-s.Player.Center;
        string hint=Language.GetTextValue("Mods.PalicoCompanion.Discovery.Hint",Language.GetTextValue("Mods.PalicoCompanion.Discovery."+Direction(delta)),(int)Math.Ceiling(delta.Length()/16));
        // Do not consume a discovery while a greeting/other message is cooling down.
        // Re-scan on the next second: a moved or removed object cannot leave a stale queued report.
        if(Notify(eventKey,EmoteID.EmoteHappiness,hint)) {
            Remember(Key(world,best,bestType));DiscoveryIndicator.Emit(s,Mark(best,bestType),4);Reports++;s.Report(hint);
        }
    }
}
