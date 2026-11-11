using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// WorldGen owns door legality and tile changes, as in DoorOpeningHelper.
// Only doors opened by companions are eligible for automatic closing.
public sealed class CompanionDoors : ModSystem {
    record Opened(Point Tile,int Type,int FrameX,int Owner,bool Gate);
    static readonly List<Opened> opened=new();
    public override void ClearWorld()=>opened.Clear();
    public override void Unload()=>opened.Clear();

    public static void TryOpen(Projectile cat,Player owner,float movement) {
        if(CompanionNetwork.Client||owner.GetModPlayer<CompanionPlayer>().EnvironmentPaused||movement==0)return;
        int direction=Math.Sign(movement);
        int x=(int)((direction>0?cat.Right.X+8:cat.Left.X-8)/16);
        for(int y=(int)(cat.Top.Y/16);y<=(int)((cat.Bottom.Y-1)/16);y++) {
            if(!WorldGen.InWorld(x,y,3))continue;
            var tile=Main.tile[x,y];
            if(!tile.HasTile||tile.IsActuated)continue;
            bool gate=tile.TileType==TileID.TallGateClosed;
            if(!gate&&!TileLoader.IsClosedDoor(tile))continue;
            int top=y-tile.TileFrameY%(gate?90:54)/18;
            // Do not open doors on another floor or while jumping over them.
            if(cat.Top.Y<top*16||cat.Bottom.Y>(top+(gate?5:3))*16+16)continue;
            bool success=gate?WorldGen.ShiftTallGate(x,top,false):WorldGen.OpenDoor(x,top,direction);
            if(!success&&!gate){direction=-direction;success=WorldGen.OpenDoor(x,top,direction);}
            if(!success)return;
            if(CompanionNetwork.Server)NetMessage.SendData(MessageID.ToggleDoorState,-1,-1,null,gate?4:0,x,top,direction);
            tile=Main.tile[x,top];
            opened.Add(new(new Point(x,top),tile.TileType,tile.TileFrameX,owner.whoAmI,gate));
            return;
        }
    }

    public override void PostUpdateProjectiles() {
        if(CompanionNetwork.Client)return;
        for(int i=opened.Count-1;i>=0;i--) {
            var door=opened[i];var tile=Main.tile[door.Tile.X,door.Tile.Y];
            var owner=Main.player[door.Owner];
            if(!tile.HasTile||tile.TileType!=door.Type||tile.TileFrameX!=door.FrameX||!owner.active||owner.GetModPlayer<CompanionPlayer>().EnvironmentPaused){opened.RemoveAt(i);continue;}
            // Include the swing area and other companions, which WorldGen's
            // player/NPC collision checks do not see as living occupants.
            var area=new Rectangle((door.Tile.X-1)*16,door.Tile.Y*16,48,door.Gate?80:48);
            area.Inflate(4,4);
            bool occupied=false;
            foreach(var p in Main.ActivePlayers)if(p.Hitbox.Intersects(area)){occupied=true;break;}
            if(!occupied)foreach(var n in Main.ActiveNPCs)if(n.Hitbox.Intersects(area)){occupied=true;break;}
            if(!occupied)foreach(var p in Main.ActiveProjectiles)if(p.ModProjectile is CompanionCat&&p.Hitbox.Intersects(area)){occupied=true;break;}
            if(occupied)continue;
            bool closed=door.Gate?WorldGen.ShiftTallGate(door.Tile.X,door.Tile.Y,true):WorldGen.CloseDoor(door.Tile.X,door.Tile.Y);
            if(closed&&CompanionNetwork.Server)NetMessage.SendData(MessageID.ToggleDoorState,-1,-1,null,door.Gate?5:1,door.Tile.X,door.Tile.Y,1);
            opened.RemoveAt(i);
        }
    }
}
