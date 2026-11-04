using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace PalicoCompanion.Systems;

// Change only the native slot-colour query during this exact draw. Never
// switch the human player's CurrentLoadoutIndex or equipment to render the cat.
public sealed class CompanionLoadoutTint:ModSystem {
    static int drawingIndex=-1;
    static Color? drawingTint;
    static Texture2D favoritePattern;
    public override void Load()=>On_ItemSlot.GetColorByLoadout+=ColorFor;
    public override void Unload(){
        On_ItemSlot.GetColorByLoadout-=ColorFor;drawingIndex=-1;drawingTint=null;
        var pattern=favoritePattern;favoritePattern=null;
        if(pattern!=null)Main.QueueMainThreadAction(pattern.Dispose);
    }
    static Color ColorFor(On_ItemSlot.orig_GetColorByLoadout orig,int slot,int context) {
        if(drawingTint is {} tint)return new Color(tint.ToVector4()*Main.inventoryBack.ToVector4());
        if(drawingIndex>=0&&ItemSlot.TryGetSlotColor(drawingIndex,context,out var color))
            return new Color(color.ToVector4()*Main.inventoryBack.ToVector4());
        return orig(slot,context);
    }
    static void PrepareFavoritePattern(){
        if(favoritePattern!=null)return;
        var source=TextureAssets.InventoryBack10.Value;var normal=TextureAssets.InventoryBack.Value;
        var pixels=new Color[source.Width*source.Height];var background=new Color[normal.Width*normal.Height];
        source.GetData(pixels);normal.GetData(background);
        // Extract only vanilla's favorite decoration, leaving the loadout's
        // own slot background and gamepad selection highlight untouched.
        for(int i=0;i<pixels.Length;i++){
            var c=pixels[i];int brightness=Math.Max(c.R,Math.Max(c.G,c.B));
            pixels[i]=source.Width==normal.Width&&source.Height==normal.Height&&c==background[i]
                ?Color.Transparent:new Color(brightness,brightness,brightness,(int)c.A);
        }
        favoritePattern=new Texture2D(source.GraphicsDevice,source.Width,source.Height);favoritePattern.SetData(pixels);
    }
    internal static void Draw(SpriteBatch batch,CompanionPlayer state,Item[] items,int context,int slot,Vector2 position,Color? tint=null,bool shared=false) {
        int previous=drawingIndex;var previousTint=drawingTint;
        try{drawingIndex=state.Loadouts.Current;drawingTint=tint;ItemSlot.Draw(batch,items,context,slot,position,Color.White);
            if(shared){
                PrepareFavoritePattern();
                batch.Draw(favoritePattern,position,null,Main.OurFavoriteColor,0f,Vector2.Zero,Main.inventoryScale,SpriteEffects.None,0f);
            }}
        finally{drawingIndex=previous;drawingTint=previousTint;}
    }
}
