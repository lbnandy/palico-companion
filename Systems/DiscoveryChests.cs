using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
namespace PalicoCompanion.Systems;

// World history, independent of whether a companion is currently enabled.
// Only confirmed native player placement/opening counts; world generation and
// section downloads must never mark untouched treasure as player storage.
public sealed class DiscoveryChests:ModSystem {
    static readonly HashSet<Point> handled=new();
    static bool receivingPlacement;
    public override void ClearWorld(){handled.Clear();receivingPlacement=false;}
    internal static bool Contains(Point at)=>handled.Contains(at);
    internal static void Apply(Point at,bool value){if(!WorldGen.InWorld(at.X,at.Y,1))return;if(value)handled.Add(at);else handled.Remove(at);}
    internal static void Mark(int index){
        if(index<0||index>=Main.maxChests||Main.chest[index] is not {} chest)return;
        var at=new Point(chest.x,chest.y);
        if(!WorldGen.InWorld(at.X,at.Y,1)||!handled.Add(at))return;
        if(Main.netMode==NetmodeID.Server)CompanionNetwork.DiscoveryChest(at,true);
    }
    internal static void Forget(int x,int y){
        var at=new Point(x,y);if(!handled.Remove(at))return;
        if(Main.netMode==NetmodeID.Server)CompanionNetwork.DiscoveryChest(at,false);
    }
    public override void SaveWorldData(TagCompound tag)=>tag["handledChests"]=handled.Select(p=>new TagCompound{{"x",p.X},{"y",p.Y}}).ToList();
    public override void LoadWorldData(TagCompound tag){handled.Clear();foreach(var p in tag.GetList<TagCompound>("handledChests"))Apply(new Point(p.GetInt("x"),p.GetInt("y")),true);}
    public override void NetSend(BinaryWriter writer){writer.Write((ushort)handled.Count);foreach(var p in handled){writer.Write((short)p.X);writer.Write((short)p.Y);}}
    public override void NetReceive(BinaryReader reader){handled.Clear();int count=reader.ReadUInt16();for(int i=0;i<count;i++)Apply(new Point(reader.ReadInt16(),reader.ReadInt16()),true);}
    public override void PostUpdatePlayers(){
        foreach(var p in Main.player)if(p.active)Mark(p.chest);
    }
    public override void Load(){On_MessageBuffer.GetData+=Receive;On_WorldGen.PlaceChest+=Place;On_Player.OpenChest+=Open;}
    public override void Unload(){On_MessageBuffer.GetData-=Receive;On_WorldGen.PlaceChest-=Place;On_Player.OpenChest-=Open;handled.Clear();}
    static void Open(On_Player.orig_OpenChest orig,Player player,int x,int y,int index){orig(player,x,y,index);Mark(player.chest);}
    static int Place(On_WorldGen.orig_PlaceChest orig,int x,int y,ushort type,bool notNearOtherChests,int style){
        int index=orig(x,y,type,notNearOtherChests,style);
        if(receivingPlacement&&!WorldGen.gen)Mark(index);
        return index;
    }
    static void Receive(On_MessageBuffer.orig_GetData orig,MessageBuffer buffer,int start,int length,out int messageType){
        bool previous=receivingPlacement;
        receivingPlacement=Main.netMode==NetmodeID.Server&&length>0&&buffer.readBuffer[start]==MessageID.ChestUpdates;
        try{orig(buffer,start,length,out messageType);}
        finally{receivingPlacement=previous;}
        if(messageType!=MessageID.RequestChestOpen&&messageType!=MessageID.SyncPlayerChest)return;
        int player=Main.netMode==NetmodeID.Server?buffer.whoAmI:Main.myPlayer;
        if(player>=0&&player<Main.maxPlayers&&Main.player[player].active)Mark(Main.player[player].chest);
    }
}

public sealed class DiscoveryChestPlacement:GlobalTile {
    public override void PlaceInWorld(int i,int j,int type,Item item){
        if(WorldGen.gen||!TileID.Sets.BasicChest[type])return;
        var tile=Main.tile[i,j];
        DiscoveryChests.Mark(Chest.FindChest(i-tile.TileFrameX%36/18,j-tile.TileFrameY%36/18));
    }
}

internal static partial class CompanionNetwork {
    const byte ChestDiscoveryState=43;
    internal static void DiscoveryChest(Point at,bool handled){var p=Packet(ChestDiscoveryState);p.Write((short)at.X);p.Write((short)at.Y);p.Write(handled);p.Send();}
}
