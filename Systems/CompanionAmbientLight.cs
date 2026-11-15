using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// A small native tile-light pass excludes held items and projectiles without
// switching off their visible light or changing the world's lighting engine.
internal sealed class CompanionAmbientLight:ModSystem {
    static readonly Action<LightingEngine> Decay=typeof(LightingEngine).GetMethod("UpdateLightDecay",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate<Action<LightingEngine>>();
    static LightingEngine engine;
    static LightMap map;
    static TileLightScanner scanner;
    internal static float Brightness(Point cell){
        if(engine==null){
            engine=new LightingEngine();scanner=new TileLightScanner();
            map=(LightMap)typeof(LightingEngine).GetField("_workingLightMap",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine);
        }
        // Native padding also covers nearby daylight, lava and modded lamps.
        int radius=LightingEngine.AREA_PADDING;
        int left=Math.Max(2,cell.X-radius),top=Math.Max(2,cell.Y-radius);
        int right=Math.Min(Main.maxTilesX-3,cell.X+radius),bottom=Math.Min(Main.maxTilesY-3,cell.Y+radius);
        var area=new Rectangle(left,top,right-left+1,bottom-top+1);
        map.SetSize(area.Width,area.Height);map.NonVisiblePadding=0;
        scanner.Update();
        scanner.ExportTo(area,map,new TileLightScannerOptions{DrawInvisibleWalls=Main.ShouldShowInvisibleWalls()});
        // Keep native water styles, vision effects and mod lighting hooks.
        Decay(engine);map.Blur();
        var color=map[cell.X-left,cell.Y-top];
        return (color.X+color.Y+color.Z)/3;
    }
    public override void OnWorldUnload(){engine=null;map=null;scanner=null;}
    public override void Unload()=>OnWorldUnload();
}
