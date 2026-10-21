using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace PalicoCompanion.Systems;

internal static class CompanionLoadoutButtons {
    // Reuse the full vanilla icon, numeral, selected frame and hover outline.
    // The only addition is two ears on its existing two-pixel grid.
    static Color[][] palettes;
    // The normal icon (including ears) occupies rows 0..29 of its 34px frame.
    // Centred in the 32px hit target, its visible centre is at y+14, not y+16.
    internal const int VisibleCenterOffsetY=14;
    internal static Rectangle Bounds(int x,int y,int index)=>new(x+36*index,y,32,32);
    internal static void Draw(SpriteBatch batch,CompanionPlayer state,int x,int y,bool clicked) {
        var texture=TextureAssets.Extra[ExtrasID.Loadouts].Value;
        if(palettes==null){
            var pixels=new Color[texture.Width*texture.Height];texture.GetData(pixels);palettes=new Color[6][];
            for(int row=0;row<3;row++)for(int col=0;col<2;col++){
                var f=texture.Frame(3,3,col,row);
                Color At(int px,int py)=>pixels[(f.Y+py)*texture.Width+f.X+px];
                palettes[row*2+col]=new[]{At(12,2),At(12,6)};
            }
        }
        for(int i=0;i<3;i++) {
            var box=Bounds(x,y,i);bool hover=box.Contains(Main.mouseX,Main.mouseY);
            int selected=state.Loadouts.Current==i?1:0;var frame=texture.Frame(3,3,selected,i);
            var origin=(box.Center.ToVector2()-frame.Size()/2).Floor();var colors=palettes[i*2+selected];
            void Pixel(int px,int py,int w,int h,Color color)=>batch.Draw(TextureAssets.MagicPixel.Value,new Rectangle((int)origin.X+px,(int)origin.Y+py,w,h),color);
            if(hover){
                batch.Draw(texture,origin,texture.Frame(3,3,2,i),Main.OurFavoriteColor);
                Pixel(7,-1,6,4,Main.OurFavoriteColor);Pixel(7,1,2,6,Main.OurFavoriteColor);
                Pixel(17,-1,6,4,Main.OurFavoriteColor);Pixel(21,1,2,6,Main.OurFavoriteColor);
            }
            // Draw the native icon over its halo so neither the face nor the
            // numeral can be painted over by hover highlighting.
            batch.Draw(texture,origin,frame,Color.White);
            // Stay inside the native head (x=8..21). III's first stroke starts
            // at x=22. Fill through both old circular shoulders at y=4, so the
            // ears and head share one outline with no seam across either root.
            Pixel(8,0,4,2,colors[0]);Pixel(8,2,2,4,colors[0]);Pixel(10,2,2,4,colors[1]);
            Pixel(18,0,4,2,colors[0]);Pixel(20,2,2,4,colors[0]);Pixel(18,2,2,4,colors[1]);
            if(!hover)continue;
            state.Player.mouseInterface=true;
            Main.instance.MouseText(CompanionText.Get("CompanionLoadoutButtons.Loadout" ,i+1)+(state.Loadouts.Current==i?CompanionText.Get("CompanionLoadoutButtons.Current"):""));
            if(clicked&&Main.mouseItem.IsAir&&!CompanionNetwork.Pending)
                CompanionControl.Request(state,CompanionControlOp.SwitchLoadout,i);
        }
    }
}
