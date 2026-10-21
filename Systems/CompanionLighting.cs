using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
namespace PalicoCompanion.Systems;

// Separate held light from permanent placement. Bounded search, no terrain replacement.
// Design references: AutoTorchPlus candidate/occlusion checks; TerraGuardians offhand light.
public sealed class CompanionLighting {
    public bool Holding {get;private set;}
    public Item HeldItem {get;private set;}
    public bool HoldingGlowstick=>Holding&&IsGlowstick(HeldItem);
    public readonly CompanionGlowstickDrawing Glow=new();
    public string Status {get;private set;}="";
    int clock,cooldown;
    // Torch grip stays inside the original offhand; never move the paw itself.
    public Vector2 Hand(Projectile cat)=>CompanionSprite.Hand(cat,true)+GripOffset(cat);
    public static Vector2 GripOffset(Projectile cat)=>CompanionTorchDrawing.Offset(cat.direction);
    public Vector2 Flame(Projectile cat)=>Hand(cat)+new Vector2(cat.direction<0?-10:10,-12);
    public static bool Torch(Item i)=>i!=null&&!i.IsAir&&ItemID.Sets.Torches[i.type]&&i.createTile>=TileID.Dirt&&TileID.Sets.Torch[i.createTile];
    public static bool Available(Item i)=>Torch(i)&&!i.favorited&&i.stack>0;
    static bool IsGlowstick(Item i)=>i!=null&&!i.IsAir&&i.stack>0&&i.ModItem is null&&ItemID.Sets.Glowsticks[i.type];
    public static bool Glowstick(Item i)=>IsGlowstick(i)&&!i.favorited;
    public static Item SelectLight(Player p,CompanionPlayer s,bool wet) {
        if(!wet){var torch=Array.Find(s.Bag,Torch)??Array.Find(p.inventory,Torch);if(torch!=null)return torch;}
        return Array.Find(s.Bag,IsGlowstick)??Array.Find(p.inventory,IsGlowstick);
    }
    public static Item[] Materials(Player p,CompanionPlayer s) {
        if(Array.Exists(s.Bag,Available))return s.Bag;
        // Shared owner supply avoids requiring a separate manual cat refill.
        if(CompanionNetwork.Server){var item=Array.Find(p.inventory,Available);if(item!=null)CompanionNetwork.RequestMaterial(s,0);return null;}
        return Array.Exists(p.inventory,Available)?p.inventory:null;
    }
    internal static bool WantsLight(Vector2 center,bool holding){
        var cell=center.ToTileCoordinates();
        if(!WorldGen.InWorld(cell.X,cell.Y,12))return false;
        float brightness=Lighting.Brightness(cell.X,cell.Y);
        if(holding){
            float ambient=0;int samples=0;
            foreach(int dx in new[]{-6,6})if(!WorldGen.SolidTile(cell.X+dx,cell.Y)){
                ambient+=Lighting.Brightness(cell.X+dx,cell.Y);samples++;
            }
            if(samples>0)brightness=Math.Min(brightness,ambient/samples);
        }
        // A dim torch, glowing ore or an inactive light tile is not sufficient
        // illumination. Retain the held light against its own emitted brightness.
        bool daylight=Main.dayTime&&!Main.eclipse&&cell.Y<Main.worldSurface&&Main.tile[cell].WallType==WallID.None;
        return WantsLight(brightness,holding,daylight,HasVisibleLight(center));
    }
    internal static bool WantsLight(float brightness,bool holding,bool daylight,bool placedLight)=>
        brightness<.48f || holding&&!(brightness>=.65f&&(daylight||placedLight));
    public void Update(Player p,CompanionPlayer s,Projectile cat,bool danger) {
        clock++;if(cooldown>0)cooldown--;
        if(s.Paused||s.Vitals.Downed||!s.Torches||!s.Enabled) {Holding=false;HeldItem=null;Status="";return;}
        var cell=cat.Center.ToTileCoordinates();
        if(!WorldGen.InWorld(cell.X,cell.Y,12))return;
        int oldType=HeldItem?.type??0;HeldItem=SelectLight(p,s,cat.wet);
        if(oldType!=(HeldItem?.type??0))Status="";
        if(HeldItem==null){Holding=false;Status=(CompanionNetwork.Server?s.NetDark:WantsLight(cat.Center,false))?(cat.wet?CompanionText.Message("CompanionLighting.NeedGlowstick"):CompanionText.Message("CompanionLighting.NeedLight")):"";return;}
        if(clock%30==0) {
            bool existing=HasVisibleLight(cat.Center);
            Holding=CompanionNetwork.Server?s.NetDark:WantsLight(cat.Center,Holding);
            if(!Holding)Status="";
            if(Holding&&!existing&&Available(HeldItem)&&clock%60==0&&cooldown==0&&!danger&&Vector2.DistanceSquared(cat.Center,p.Center)<160*160) {
                Status=TryNearby(p,s,cat.Center,HeldItem);
                if(Status.StartsWith(SafeConstruction.SuccessPrefix)){Holding=false;cooldown=120;}
            }
        }
        if(HoldingGlowstick){Glow.Emit(cat,HeldItem,Flame(cat));Status=CompanionText.Message("CompanionLighting.HoldingGlowstick");}
        // Held torch emission is performed after the current pose/facing is set in PostAI.
    }
    public static bool HasVisibleLight(Vector2 from) {
        var c=from.ToTileCoordinates();
        for(int x=c.X-7;x<=c.X+7;x++)for(int y=c.Y-5;y<=c.Y+5;y++) {
            if(!WorldGen.InWorld(x,y,2))continue;
            var t=Main.tile[x,y];
            if(!t.HasTile||t.IsActuated||!Main.tileLighted[t.TileType]||t.TileType==TileID.Platforms)continue;
            if(t.TileType!=TileID.Torches&&Main.tileCut[t.TileType])continue;
            if(t.TileType==TileID.Torches&&t.TileFrameX>=66)continue;
            if(Reachability.Clear(from,new Vector2(x*16+8,y*16+8)))return true;
        }
        return false;
    }
    public static string TryNearby(Player p,CompanionPlayer s,Vector2 center,Item selected=null) {
        var materials=Materials(p,s);
        selected??=materials==null?null:Array.Find(materials,Available);
        if(materials==null||!Available(selected)||Array.IndexOf(materials,selected)<0)return CompanionText.Message("CompanionLighting.NoTorch");
        var c=center.ToTileCoordinates();string refusal=CompanionText.Message("CompanionLighting.NoSupport");
        // Nearest Manhattan rings: at most 61 candidates once a second, only within reach.
        for(int radius=0;radius<=5;radius++)for(int dx=-radius;dx<=radius;dx++)for(int dy=-radius;dy<=radius;dy++) {
            if(Math.Abs(dx)+Math.Abs(dy)!=radius)continue;
            int x=c.X+dx,y=c.Y+dy;if(!WorldGen.InWorld(x,y,12))continue;
            var tile=Main.tile[x,y];if(tile.HasTile||tile.LiquidAmount>0)continue;
            if(tile.WallType==WallID.None&&!WorldGen.SolidTile(x-1,y)&&!WorldGen.SolidTile(x+1,y)&&!WorldGen.SolidTile(x,y+1))continue;
            string result=SafeConstruction.TryPlace(p,center,materials,x,y,selected.type);
            if(result.StartsWith(SafeConstruction.SuccessPrefix)) {
                (s.Cat?.ModProjectile as CompanionCat)?.FaceWork(new Vector2(x*16+8,y*16+8));return result;
            }
            refusal=CompanionText.Message("CompanionLighting.LightingPrefix")+result;
        }
        return refusal;
    }
}
