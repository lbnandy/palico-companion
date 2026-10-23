using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;

namespace PalicoCompanion.Systems;

internal static class CompanionEnvironmentButton
{
    internal const int Size=32;
    const int Border=2;
    static Texture2D inactive,highlight;

    static void PrepareFrames(Texture2D icon)
    {
        if(inactive!=null)return;
        var pixels=new Color[icon.Width*icon.Height];
        icon.GetData(pixels);
        var gray=new Color[pixels.Length];
        int width=icon.Width+Border*2,height=icon.Height+Border*2;
        var outline=new Color[width*height];
        for(int y=0;y<icon.Height;y++)for(int x=0;x<icon.Width;x++){
            var color=pixels[y*icon.Width+x];
            int luminance=(color.R*77+color.G*150+color.B*29)>>8;
            gray[y*icon.Width+x]=new Color(luminance,luminance,luminance,(int)color.A);
            if(color.A==0)continue;
            for(int dy=-Border;dy<=Border;dy++)for(int dx=-Border;dx<=Border;dx++){
                int index=(y+Border+dy)*width+x+Border+dx;
                if(outline[index].A<color.A)outline[index]=new Color(color.A,color.A,color.A,color.A);
            }
        }
        inactive=new Texture2D(icon.GraphicsDevice,icon.Width,icon.Height);
        inactive.SetData(gray);
        highlight=new Texture2D(icon.GraphicsDevice,width,height);
        highlight.SetData(outline);
    }

    internal static void Unload()
    {
        var gray=inactive;var outline=highlight;
        inactive=highlight=null;
        if(gray!=null||outline!=null)Main.QueueMainThreadAction(()=>{gray?.Dispose();outline?.Dispose();});
    }

    internal static void Draw(SpriteBatch spriteBatch,CompanionPlayer state,Rectangle bounds,Point mouse,bool clicked)
    {
        Main.instance.LoadItem(ItemID.CopperPickaxe);
        var icon=TextureAssets.Item[ItemID.CopperPickaxe].Value;
        PrepareFrames(icon);
        float scale=System.Math.Min(1f,24f/System.Math.Max(icon.Width,icon.Height));
        var center=bounds.Center.ToVector2();
        bool enabled=!state.Player.dead&&!CompanionNetwork.Pending&&(!CompanionNetwork.Client||state.NetReady);
        bool hover=bounds.Contains(mouse);
        // Like vanilla's equipment/housing tabs: separate inactive, active and hover frames.
        if(hover&&enabled)
            spriteBatch.Draw(highlight,center,null,Main.OurFavoriteColor,0f,highlight.Size()/2f,scale,SpriteEffects.None,0f);
        var texture=state.EnvironmentPaused?inactive:icon;
        spriteBatch.Draw(texture,center,null,enabled?Color.White:Color.Gray,0f,texture.Size()/2f,scale,SpriteEffects.None,0f);
        if(!hover)return;
        state.Player.mouseInterface=true;
        string action=Language.GetTextValue("Mods.PalicoCompanion.UI."+(state.EnvironmentPaused?"EnvironmentResume":"EnvironmentPause"));
        string hint=action+"\n"+Language.GetTextValue("Mods.PalicoCompanion.UI.EnvironmentHint");
        Main.instance.MouseText(string.Join("\n",CompanionCopy.Wrap(hint,System.Math.Min(520,Main.screenWidth-32))));
        if(enabled&&clicked&&Main.mouseItem.IsAir)CompanionEnvironment.RequestToggle(state);
    }
}
