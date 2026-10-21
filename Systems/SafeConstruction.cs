using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

/// <summary>Authoritative one-cell construction. Never replaces or destroys a tile.</summary>
public static class SafeConstruction
{
    public const string SuccessPrefix = "完成：";
    public static bool CanReach(Vector2 from,Vector2 target,int itemType) {
        Vector2 top=target-new Vector2(0,9);
        if(Reachability.Clear(from,ItemID.Sets.Torches[itemType]?target:top))return true;
        // A rope can be fed over the lip of a ledge; both legs of the route must be empty.
        Vector2 corner=new(target.X,from.Y);
        return ContentSamples.ItemsByType.TryGetValue(itemType,out var rope)&&rope.createTile>=TileID.Dirt&&Main.tileRope[rope.createTile]&&Reachability.Clear(from,corner)&&Reachability.Clear(corner,top);
    }

    public static string TryPlace(Player player, Vector2 catCenter, Item[] bag, int x, int y, int itemType, bool ownerRequested = false)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return CompanionText.Message("SafeConstruction.ServerPending");
        if (player == null || !player.active || player.dead) return CompanionText.Message("SafeConstruction.OwnerUnavailable");
        if (bag == null) return CompanionText.Message("SafeConstruction.BagUnavailable");
        int tileType;
        bool torch=ContentSamples.ItemsByType.TryGetValue(itemType,out var sample)&&CompanionLighting.Torch(sample);
        if (torch) tileType = sample.createTile;
        else if (ownerRequested && sample != null && ConstructionAssist.Supported(sample)) tileType = sample.createTile;
        else if (itemType == ItemID.WoodPlatform) tileType = TileID.Platforms;
        else if (itemType == ItemID.Rope) tileType = TileID.Rope;
        else return CompanionText.Message("SafeConstruction.Unsupported");
        if (!WorldGen.InWorld(x, y, 10)) return CompanionText.Message("SafeConstruction.WorldEdge");
        Vector2 target = new Vector2(x * 16 + 8, y * 16 + 8);
        if (!float.IsFinite(catCenter.X) || !float.IsFinite(catCenter.Y)) return CompanionText.Message("SafeConstruction.InvalidPosition");
        if (Vector2.DistanceSquared(catCenter, target) > MathF.Pow(128,2)) return CompanionText.Message("SafeConstruction.Approaching");
        if (Vector2.DistanceSquared(player.Center, target) > 480 * 480) return CompanionText.Message("SafeConstruction.OwnerTooFar");
        // For a floor-level platform, raycast to its top surface; the center ray clips
        // the neighboring supporting ground even though the empty placement cell is reachable.
        if (!CanReach(catCenter,target,itemType)) return CompanionText.Message("SafeConstruction.Obstructed");
        if(!ownerRequested) {
        if (Math.Abs(x - Main.spawnTileX) <= 16 && Math.Abs(y - Main.spawnTileY) <= 12)
            return CompanionText.Message("SafeConstruction.WorldSpawn");
        if (player.SpawnX >= 0 && Math.Abs(x - player.SpawnX) <= 12 && Math.Abs(y - player.SpawnY) <= 10)
            return CompanionText.Message("SafeConstruction.BedSpawn");
        if (Main.tile[x, y].HasTile) return CompanionText.Message("SafeConstruction.Occupied");
        for (int tx = x - 1; tx <= x + 1; tx++)
        for (int ty = y - 1; ty <= y + 1; ty++)
        {
            Tile tile = Main.tile[tx, ty];
            if (tile.LiquidAmount > 0) return CompanionText.Message("SafeConstruction.Liquid");
            if (tile.RedWire || tile.BlueWire || tile.GreenWire || tile.YellowWire || tile.HasActuator)
                return CompanionText.Message("SafeConstruction.Wiring");
            if (tile.WallType > WallID.None && Main.wallHouse[tile.WallType]) return CompanionText.Message("SafeConstruction.Housing");
            if (tile.HasTile && (tile.TileType == TileID.Containers || tile.TileType == TileID.Containers2 || tile.TileType == TileID.Dressers))
                return CompanionText.Message("SafeConstruction.Furniture");
        }
        foreach (Chest chest in Main.chest)
            if (chest != null && Math.Abs(chest.x - x) <= 8 && Math.Abs(chest.y - y) <= 8)
                return CompanionText.Message("SafeConstruction.Chest");
        foreach (NPC npc in Main.npc)
            if (npc.active && npc.townNPC && Vector2.DistanceSquared(npc.Center, target) < 160 * 160)
                return CompanionText.Message("SafeConstruction.TownNpc");
        }
        Item material = null;
        foreach (Item item in bag)
            if (item != null && !item.IsAir && item.type == itemType && item.stack > 0 && !item.favorited)
            {
                material = item;
                break;
            }
        if (material == null) return CompanionText.Message("SafeConstruction.NoMaterials");
        if (!TileLoader.CanPlace(x, y, tileType)) return CompanionText.Message("SafeConstruction.ModBlocked");
        // Recheck immediately before mutation. This entire action runs on the game thread.
        if (Main.tile[x, y].HasTile || material.IsAir || material.stack < 1 || material.type != itemType)
            return CompanionText.Message("SafeConstruction.Changed");
        if(Terraria.ObjectData.TileObjectData.CustomPlace(tileType,material.placeStyle)){
            if(!TileObject.CanPlace(x,y,(ushort)tileType,material.placeStyle,player.direction,out var data)||!TileObject.Place(data))
                return CompanionText.Message("SafeConstruction.PlacementFailed");
            WorldGen.SquareTileFrame(x,y);
        }else WorldGen.PlaceTile(x, y, tileType, mute: true, forced: false, plr: player.whoAmI, style: material.placeStyle);
        Tile placed = Main.tile[x, y];
        if (!placed.HasTile || placed.TileType != tileType) return CompanionText.Message("SafeConstruction.NoSupport");
        CompanionConsumption.Consume(player.GetModPlayer<CompanionPlayer>(),material);
        if(CompanionNetwork.Server)NetMessage.SendTileSquare(-1,x-1,y-1,3,3);
        // Native torch/platform/rope placement uses Dig, including CustomPlace.
        // Send presentation separately: replaying placement would run tile hooks twice.
        CompanionFeedback.Show(7,target,0);
        return SuccessPrefix + CompanionText.Message("SafeConstruction.Placed") + CompanionText.MessageFull(Lang.GetItemName(itemType).Key);
    }
}
