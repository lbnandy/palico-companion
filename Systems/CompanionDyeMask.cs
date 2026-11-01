using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Classify the original pixels, before custom clothing colors are substituted.
// Keep both layers at the source dimensions so native dye coordinates, frame
// rectangles and all existing animation anchors remain unchanged.
internal sealed class CompanionDyeMask : ModSystem {
    internal sealed class Layers {
        internal Texture2D Cloth,Original;
        internal bool[] Mask;
        internal int Color=-1;
    }
    static readonly Dictionary<Texture2D,Layers> cache=new();
    // Dye scope includes the hood/shirt outline and the worn sword sheath.
    // These colors are deliberately NOT part of the cloth-color picker: that
    // picker changes fabric color, while an equipped dye covers the outfit.
    internal static bool Clothing(Color pixel)=>CompanionPalette.Cloth(pixel)||
        pixel.A>0&&CompanionPalette.Pack(pixel) is 0x1E2033 or 0x354053 or 0x606F84;
    static bool Sheath(Texture2D source,int index,Color pixel){
        if(pixel.A==0||CompanionPalette.Pack(pixel)!=0x1E1E1E)return false;
        int y=index/source.Width,x=index%source.Width;
        if(source==CompanionSprite.CartCat)return y%32>=16;
        if(source==CompanionBarrelVisual.RecoilTexture)return CompanionBarrelVisual.RecoilSheath(index);
        if(source!=CompanionSprite.Atlas)return false;
        int pose=y/CompanionSprite.Height,row=y%CompanionSprite.Height;
        // The artist's prone/get-up faces sit lower than the standing face.
        // Keep their eyes and facial linework out of the outfit dye mask.
        if(pose==CompanionSpriteLayout.Prone)return x>=16&&row>=18;
        if(pose==CompanionSpriteLayout.GetUp0)return x>=19&&row>=22;
        if(pose==CompanionSpriteLayout.GetUp1)return x<=10&&row>=21;
        return row>=18;
    }
    internal static Layers Get(Texture2D source,Texture2D texture,int color) {
        if(!cache.TryGetValue(texture,out var layers)){
            var pixels=new Color[source.Width*source.Height];source.GetData(pixels);
            layers=new(){Mask=new bool[pixels.Length]};
            for(int i=0;i<pixels.Length;i++){
                layers.Mask[i]=Clothing(pixels[i])||Sheath(source,i,pixels[i]);
                if(layers.Mask[i])pixels[i]=Color.Transparent;
            }
            layers.Original=new Texture2D(source.GraphicsDevice,source.Width,source.Height);
            layers.Original.SetData(pixels);
            layers.Cloth=new Texture2D(source.GraphicsDevice,source.Width,source.Height);
            cache.Add(texture,layers);
        }
        if(layers.Color!=color){
            var pixels=new Color[texture.Width*texture.Height];texture.GetData(pixels);
            for(int i=0;i<pixels.Length;i++)if(!layers.Mask[i])pixels[i]=Color.Transparent;
            layers.Cloth.SetData(pixels);layers.Color=color;
        }
        return layers;
    }
    public override void Unload(){
        var owned=new List<Layers>(cache.Values);cache.Clear();
        Main.QueueMainThreadAction(()=>{foreach(var layers in owned){layers.Cloth.Dispose();layers.Original.Dispose();}});
    }
}
