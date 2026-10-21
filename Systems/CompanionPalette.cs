using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Palette substitution at draw time. The PNG, outlines, cream fur,
// equipment, kite and lighting remain untouched. Four exact cloth colours.
public sealed class CompanionPalette : ModSystem {
    public const int Default=0x5368A5;
    internal static int Valid(int rgb)=>rgb>=0&&rgb<=0xffffff?rgb:Default;
    internal static Color ColorOf(int rgb)=>new((rgb>>16)&255,(rgb>>8)&255,rgb&255);
    internal static int Pack(Color c)=>(c.R<<16)|(c.G<<8)|c.B;
    internal static bool Cloth(Color c)=>c.A>0&&Pack(c) is 0x5368A5 or 0x40568D or 0x7185BA or 0x303E6D;
    internal static Color Recolor(Color pixel,int rgb){
        rgb=Valid(rgb);if(rgb==Default||!Cloth(pixel))return pixel;
        var source=Main.rgbToHsl(pixel);var basis=Main.rgbToHsl(ColorOf(Default));var target=Main.rgbToHsl(ColorOf(rgb));
        float light=source.Z<=basis.Z?target.Z*source.Z/basis.Z:target.Z+(1-target.Z)*(source.Z-basis.Z)/(1-basis.Z);
        float hue=(target.X+source.X-basis.X+1)%1;
        var result=Main.hslToRgb(hue,Math.Clamp(target.Y*source.Y/basis.Y,0,1),Math.Clamp(light,0,1));result.A=pixel.A;return result;
    }
    sealed class Entry {internal int Color=-1;internal Texture2D Texture;}
    sealed class PaletteCache {
    readonly Dictionary<int,Entry> cache=new();
    Color[] original,pixels;
    internal Texture2D Texture(Texture2D atlas,int owner,int rgb){
        rgb=Valid(rgb);if(atlas==null||rgb==Default||Main.dedServ)return atlas;
        if(original==null){original=new Color[atlas.Width*atlas.Height];atlas.GetData(original);pixels=new Color[original.Length];}
        if(!cache.TryGetValue(owner,out var entry))cache[owner]=entry=new();
        if(entry.Texture==null)entry.Texture=new Texture2D(Main.graphics.GraphicsDevice,atlas.Width,atlas.Height);
        if(entry.Color!=rgb){for(int i=0;i<pixels.Length;i++)pixels[i]=Recolor(original[i],rgb);entry.Texture.SetData(pixels);entry.Color=rgb;}
        return entry.Texture;
    }
    internal void Clear(){var textures=new List<Texture2D>();foreach(var e in cache.Values)if(e.Texture!=null)textures.Add(e.Texture);cache.Clear();original=pixels=null;Main.QueueMainThreadAction(()=>{foreach(var t in textures)t.Dispose();});}
    }
    static readonly PaletteCache bodies=new(),heads=new(),uiHeads=new();
    internal static Texture2D Texture(CompanionPlayer s)=>Texture(s.Player.whoAmI,s.HoodColor);
    internal static Texture2D Texture(int owner,int rgb)=>bodies.Texture(CompanionSprite.Atlas,owner,rgb);
    internal static Texture2D HeadTexture(CompanionPlayer s)=>HeadTexture(s.Player.whoAmI,s.HoodColor);
    internal static Texture2D HeadTexture(int owner,int rgb)=>heads.Texture(CompanionSprite.Head,owner,rgb);
    internal static Texture2D UiHeadTexture(int owner,int rgb)=>uiHeads.Texture(CompanionSprite.HeadUi,owner,rgb);
    // One texture per player plus one isolated preview, never per slider step.
    public override void Unload(){bodies.Clear();heads.Clear();uiHeads.Clear();}
}
