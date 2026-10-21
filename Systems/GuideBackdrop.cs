using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;

namespace PalicoCompanion.Systems;

// Cache the native panel backgrounds with independent alpha. The page replaces
// the outer fill beneath it instead of accumulating another translucent layer.
internal sealed class GuideBackdrop:IDisposable {
    RenderTarget2D texture;
    readonly UIPanel panel=new();
    internal void Create(){
        var device=Main.graphics.GraphicsDevice;
        var targets=device.GetRenderTargets();var view=device.Viewport;var scissor=device.ScissorRectangle;
        texture=new RenderTarget2D(device,736,510);
        using var batch=new SpriteBatch(device);
        try{
            device.SetRenderTarget(texture);device.Clear(Color.Transparent);
            batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,null,null,null,Matrix.Identity);
            Panel(batch,new Rectangle(0,0,736,510),CompanionPanelStyle.Background,Color.Transparent);
            // Mask the outer panel behind the native rounded page, then lay
            // down its own alpha. This avoids compounding .80 and .88.
            batch.End();
            using var erase=new BlendState {ColorSourceBlend=Blend.Zero,ColorDestinationBlend=Blend.InverseSourceAlpha,AlphaSourceBlend=Blend.Zero,AlphaDestinationBlend=Blend.InverseSourceAlpha};
            batch.Begin(SpriteSortMode.Deferred,erase,SamplerState.LinearClamp,null,null,null,Matrix.Identity);
            Panel(batch,new Rectangle(18,94,700,398),Color.White,Color.Transparent);
            batch.End();
            batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,null,null,null,Matrix.Identity);
            Panel(batch,new Rectangle(18,94,700,398),new Color(39,52,92)*CompanionPanelStyle.PageOpacity,Color.Transparent);
            batch.End();
        }finally{device.SetRenderTargets(targets);device.Viewport=view;device.ScissorRectangle=scissor;}
    }
    void Panel(SpriteBatch batch,Rectangle r,Color background,Color border){
        panel.Left.Set(r.X,0);panel.Top.Set(r.Y,0);panel.Width.Set(r.Width,0);panel.Height.Set(r.Height,0);
        panel.BackgroundColor=background;panel.BorderColor=border;panel.Recalculate();panel.Draw(batch);
    }
    internal void Draw(SpriteBatch batch,Rectangle bounds){
        if(texture!=null)batch.Draw(texture,bounds,Color.White);
        Panel(batch,bounds,Color.Transparent,Color.Black);
        Panel(batch,new Rectangle(bounds.X+18,bounds.Y+94,700,398),Color.Transparent,Color.Black);
    }
    public void Dispose(){texture?.Dispose();texture=null;}
}
