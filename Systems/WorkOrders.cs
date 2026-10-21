using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace PalicoCompanion.Systems;

/// <summary>Transient, per-player explicit construction order. Do not save or resume across worlds.</summary>
public sealed class WorkOrders
{
    public bool Active { get; private set; }
    public bool HasPreview { get; private set; }
    public string Status { get; private set; } = "No construction order";
    public int Completed { get; private set; }
    public int Length { get; private set; }
    public int ItemType { get; private set; }
    public Point Start { get; private set; }
    public Point Step { get; private set; }
    public Vector2 CurrentTarget => new Vector2((Start.X + Step.X * Completed) * 16 + 8, (Start.Y + Step.Y * Completed) * 16 + 8);
    private string world = "";
    private int owner = -1, cadence, approachTicks;

    public string Preview(Player player, Vector2 mouseWorld, int itemType, int dx, int dy, int length, Item[] bag)
    {
        if (Active) return Status = "Cancel the active construction order first";
        HasPreview = false;
        if (Main.netMode != NetmodeID.SinglePlayer || player == null || !player.active || player.dead)
            return Status = "Construction requires a living player in single player";
        if (!float.IsFinite(mouseWorld.X) || !float.IsFinite(mouseWorld.Y)) return Status = "Invalid cursor position";
        if (length < 1 || length > 24) return Status = "Length must be 1 to 24 tiles";
        if (itemType == ItemID.WoodPlatform) {
            if (Math.Abs((long)dx) != 1 || dy != 0) return Status = "Platforms require horizontal direction -1 or +1";
        }
        else if (itemType == ItemID.Rope) {
            if (dx != 0 || dy != 1) return Status = "Rope orders build downward only";
        }
        else return Status = "Only wood platforms and rope support construction orders";
        Start = new Point((int)(mouseWorld.X / 16), (int)(mouseWorld.Y / 16));
        Step = new Point(dx, dy); Length = length; ItemType = itemType; Completed = 0;
        world = Main.ActiveWorldFileData.UniqueId.ToString(); owner = player.whoAmI;
        string error = Validate(player, bag);
        if (error != null) return Status = error;
        HasPreview = true;
        return Status = $"Preview: {length} {(itemType == ItemID.Rope ? "rope" : "platform")} at ({Start.X},{Start.Y}), direction ({dx},{dy}); budget {length} items. Confirm to start.";
    }

    public string Confirm(Player player, Item[] bag)
    {
        if (!HasPreview || Active) return Status = "Preview a construction order first";
        string error = Validate(player, bag);
        if (error != null) { HasPreview = false; return Status = error; }
        HasPreview = false; Active = true; cadence = 0; approachTicks = 0;
        return Status = "Construction confirmed; walking to first tile";
    }

    public string Cancel()
    {
        Active = false; HasPreview = false; cadence = 0; approachTicks = 0;
        return Status = $"Construction cancelled; {Completed} placed tiles retained";
    }

    /// <summary>Call once per game tick. Caller walks the cat toward CurrentTarget while Active.</summary>
    public void Tick(Player player, Vector2 catCenter, Item[] bag, bool paused, bool danger)
    {
        if (!Active) return;
        if (Main.netMode != NetmodeID.SinglePlayer || player == null || !player.active || player.dead ||
            player.whoAmI != owner || Main.ActiveWorldFileData.UniqueId.ToString() != world) {
            Stop("Construction stopped: player or world changed"); return;
        }
        if (paused || danger) { Status = paused ? "Construction paused" : "Construction waiting for danger to pass"; return; }
        Vector2 target = CurrentTarget;
        if (Vector2.DistanceSquared(player.Center, target) > 480 * 480) { Stop("Construction stopped: owner is more than 30 tiles away"); return; }
        if (++approachTicks > 600) { Stop("Construction stopped: tile unreachable for 10 seconds"); return; }
        if (!float.IsFinite(catCenter.X) || !float.IsFinite(catCenter.Y)) { Stop("Construction stopped: invalid cat position"); return; }
        Vector2 reachPoint = ItemType == ItemID.WoodPlatform ? target - new Vector2(0, 9) : target;
        if (Vector2.DistanceSquared(catCenter, target) > 64 * 64 || !Reachability.Clear(catCenter, reachPoint)) {
            Status = $"Walking to construction tile {Completed + 1}/{Length}"; cadence = 0; return;
        }
        Status = $"Working on tile {Completed + 1}/{Length}";
        if (++cadence < 45) return;
        cadence = 0;
        int x = Start.X + Step.X * Completed, y = Start.Y + Step.Y * Completed;
        string result = SafeConstruction.TryPlace(player, catCenter, bag, x, y, ItemType);
        if (!result.StartsWith(SafeConstruction.SuccessPrefix, StringComparison.Ordinal)) { Stop(result); return; }
        Completed++; approachTicks = 0;
        if (Completed >= Length) { Active = false; Status = $"Construction complete: {Completed} tiles"; }
        else Status = $"Placed {Completed}/{Length}; walking to next tile";
    }

    private void Stop(string reason) { Active = false; HasPreview = false; Status = reason + $" ({Completed}/{Length} placed; retained)"; }

    private string Validate(Player player, Item[] bag)
    {
        if (Main.netMode != NetmodeID.SinglePlayer || player == null || !player.active || player.dead || player.whoAmI != owner)
            return "Construction requires the original living player in single player";
        if (Main.ActiveWorldFileData.UniqueId.ToString() != world) return "Preview belongs to another world";
        if (Vector2.DistanceSquared(player.Center, new Vector2(Start.X * 16 + 8, Start.Y * 16 + 8)) > 192 * 192)
            return "Construction start must be within 12 tiles of the player";
        int available = 0;
        if (bag != null) foreach (Item item in bag)
            if (item != null && !item.IsAir && !item.favorited && item.type == ItemType) available += Math.Min(item.stack, 24);
        if (available < Length) return $"Insufficient un-favorited materials: {available}/{Length}";
        for (int n = 0; n < Length; n++) {
            int x = Start.X + Step.X * n, y = Start.Y + Step.Y * n;
            if (!WorldGen.InWorld(x, y, 10)) return "Preview touches the world boundary";
            if (Main.tile[x, y].HasTile) return $"Preview blocked at ({x},{y}); existing tiles are never replaced";
            if (Math.Abs(x - Main.spawnTileX) <= 16 && Math.Abs(y - Main.spawnTileY) <= 12) return "Preview touches world spawn protection";
            if (player.SpawnX >= 0 && Math.Abs(x - player.SpawnX) <= 12 && Math.Abs(y - player.SpawnY) <= 10) return "Preview touches bed spawn protection";
            for (int a = x - 1; a <= x + 1; a++) for (int b = y - 1; b <= y + 1; b++) {
                Tile t = Main.tile[a, b];
                if (t.LiquidAmount > 0 || t.RedWire || t.BlueWire || t.GreenWire || t.YellowWire || t.HasActuator)
                    return $"Preview unsafe near ({x},{y}): liquid or wiring";
                if (t.WallType > WallID.None && Main.wallHouse[t.WallType]) return "Preview touches housing walls";
            }
            foreach (Chest chest in Main.chest) if (chest != null && Math.Abs(chest.x - x) <= 8 && Math.Abs(chest.y - y) <= 8)
                return "Preview touches chest protection";
            Vector2 center = new Vector2(x * 16 + 8, y * 16 + 8);
            foreach (NPC npc in Main.npc) if (npc.active && npc.townNPC && Vector2.DistanceSquared(npc.Center, center) < 160 * 160)
                return "Preview touches town NPC protection";
        }
        return null;
    }
}
