using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;
namespace PalicoCompanion.Systems;

// Harvest/replant engine sequence adapted from ImproveGame AutomaticPlantingPacket (MIT).
// Copyright (c) 2022 局长. Full license: ThirdParty/ImproveGame-LICENSE.txt.
// Palico additions: visible approach, blooming-only planted herbs, actual seed debit,
// bounds, revalidation, cancellation and no destructive retries.
public sealed class GardenAssist {
    static readonly int[] Seeds={ItemID.DaybloomSeeds,ItemID.MoonglowSeeds,ItemID.BlinkrootSeeds,ItemID.DeathweedSeeds,ItemID.WaterleafSeeds,ItemID.FireblossomSeeds,ItemID.ShiverthornSeeds};
    public static bool IsSeed(int type)=>Array.IndexOf(Seeds,type)>=0;
    Point tile; int cooldown,elapsed,workTicks;
    public bool Active {get;private set;}
    public Vector2 Target=>tile.ToWorldCoordinates();
    public void Cancel(){Active=false;workTicks=elapsed=0;}
    static int Style(Point at) {
        if(!WorldGen.InWorld(at.X,at.Y,10))return -1;
        var t=Main.tile[at];var below=Main.tile[at.X,at.Y+1];
        int style=t.TileFrameX/18;
        if(!t.HasTile||t.TileType!=TileID.BloomingHerbs||style<0||style>=Seeds.Length||!below.HasTile)return -1;
        if(t.IsActuated||below.IsActuated)return -1;
        return style;
    }
    static Item Seed(CompanionPlayer s,int style)=>Array.Find(s.Bag,i=>!i.IsAir&&!i.favorited&&i.type==Seeds[style]);
    public bool Tick(Player owner,CompanionPlayer s,Projectile cat,bool danger) {
        if(Main.netMode==NetmodeID.MultiplayerClient||!(CompanionNetwork.Server?s.NetGarden:ModContent.GetInstance<CompanionConfig>().Gardening)||!s.Enabled||s.Paused||s.EnvironmentPaused||s.Vitals.Downed||s.Mining.Active||s.Order.Active||s.Order.HasPreview||s.Storage.Active||s.Trees.Active){Cancel();return false;}
        if(danger){workTicks=0;return false;}
        if(cooldown>0){cooldown--;return false;}
        if(!Active) {
            cooldown=60;float best=224*224;
            var center=s.WorkAnchor.ToTileCoordinates();
            // 29x17 cells once per second; no whole-world scans.
            for(int x=center.X-14;x<=center.X+14;x++)for(int y=center.Y-8;y<=center.Y+8;y++) {
                var candidate=new Point(x,y);int style=Style(candidate);if(style<0)continue;
                if(Vector2.DistanceSquared(s.WorkAnchor,candidate.ToWorldCoordinates())>224*224)continue;
                float distance=Vector2.DistanceSquared(cat.Center,candidate.ToWorldCoordinates());
                if(distance>=best||!Reachability.Clear(cat.Center,candidate.ToWorldCoordinates()))continue;
                tile=candidate;best=distance;Active=true;
            }
            if(!Active)return false;cooldown=0;elapsed=workTicks=0;
        }
        int current=Style(tile);
        if(++elapsed>CompanionWorkTiming.Timeout(300)||current<0||Vector2.DistanceSquared(s.WorkAnchor,Target)>224*224){Cancel();cooldown=120;return false;}
        s.Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.GardenWalking");
        if(Vector2.DistanceSquared(cat.Center,Target)>40*40){workTicks=0;return true;}
        if(!Reachability.Clear(cat.Center,Target)){Cancel();cooldown=120;return false;}
        s.Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.GardenWorking");
        (cat.ModProjectile as CompanionCat)?.FaceWork(Target);
        if(++workTicks<CompanionWorkTiming.Ticks(30))return true;
        // Terraria generates the normal herb/seed drops. Never fabricate inventory rewards.
        using var drops=new GardenHarvestDrops.Capture(tile);
        using(CompanionTileActions.Begin())using(new HarvestBasket(s))WorldGen.KillTile(tile.X,tile.Y);
        if(!Main.tile[tile].HasTile) {
            s.Experience++;
            var seed=Seed(s,current)??drops.Items.FirstOrDefault(i=>i.active&&!i.IsAir&&!i.favorited&&i.type==Seeds[current]&&
                (i.playerIndexTheItemIsReservedFor==255||i.playerIndexTheItemIsReservedFor==owner.whoAmI));
            if(seed!=null){
                WorldGen.PlaceTile(tile.X,tile.Y,TileID.ImmatureHerbs,true,false,-1,current);
                var planted=Main.tile[tile];
                if(planted.HasTile&&planted.TileType==TileID.ImmatureHerbs&&planted.TileFrameX/18==current) {
                    if(cat.ModProjectile is CompanionCat visual)visual.ActionVisual.Begin(visual,CompanionGesture.Plant,seed.type);
                    int ground=Array.IndexOf(Main.item,seed);
                    CompanionConsumption.Consume(s,seed);if(seed.IsAir&&ground>=0)seed.active=false;
                    if(ground>=0&&CompanionNetwork.Server)NetMessage.SendData(MessageID.SyncItem,-1,-1,null,ground);
                    s.Report(CompanionText.MessageFull("Mods.PalicoCompanion.UI.GardenDone"));
                }else s.Report(CompanionText.MessageFull("Mods.PalicoCompanion.UI.GardenReplantBlocked"));
            }else s.Report(CompanionText.Message("GardenAssist.NoSeeds"));
        } else s.Report(CompanionText.MessageFull("Mods.PalicoCompanion.UI.GardenBlocked"));
        if(CompanionNetwork.Server)NetMessage.SendTileSquare(-1,tile.X-1,tile.Y-1,3,3);
        Cancel();cooldown=CompanionWorkTiming.Ticks(120);return false;
    }
}

// Capture only this native harvest's genuine seeds; never spend somebody else's
// nearby drops or invent a seed when vanilla/mod hooks produced none.
public sealed class GardenHarvestDrops:GlobalItem {
    internal sealed class Capture:IDisposable {
        internal readonly Point Tile;internal readonly List<Item> Items=new();
        readonly Capture previous;
        internal Capture(Point tile){Tile=tile;previous=current;current=this;}
        public void Dispose()=>current=previous;
    }
    static Capture current;
    public override void OnSpawn(Item item,Terraria.DataStructures.IEntitySource source){
        if(current!=null&&source is Terraria.DataStructures.EntitySource_TileBreak tile&&
            tile.TileCoords.X==current.Tile.X&&tile.TileCoords.Y==current.Tile.Y&&GardenAssist.IsSeed(item.type))current.Items.Add(item);
    }
}
