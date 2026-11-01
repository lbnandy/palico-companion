using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace PalicoCompanion.Systems;

// UIElement owns the immediate batch and restores it for later UI/cursor layers,
// just like vanilla UICharacter. Art uses the companion's two-pixel grid.
internal sealed class CompanionCartVisual : UIElement {
    internal CompanionCartPlayer Cart;
    int soundAge=-1;
    internal CompanionCartVisual(){UseImmediateMode=true;OverrideSamplerState=SamplerState.PointClamp;}
    internal static int Frame(int age)=>Math.Max(0,age)/9%6;
    internal static float Progress(int age){float t=Math.Clamp(age/(float)CompanionCartPlayer.Duration,0,1);return t*t*(3-2*t);}
    protected override void DrawSelf(SpriteBatch batch){
        // Catch inside DrawSelf so UIElement can always restore its batch.
        try{DrawCart(batch);}
        catch(Exception e){Cart?.Cancel();ModContent.GetInstance<CompanionCart>().Report(e);}
        finally{Main.pixelShader.CurrentTechnique.Passes[0].Apply();}
    }
    void DrawCart(SpriteBatch batch){
        if(CompanionSprite.CartCat==null||CompanionSprite.Cart==null||Cart?.Passenger==null)return;
        // InterfaceScaleType.UI has already converted these to UI coordinates.
        float width=Main.screenWidth,height=Main.screenHeight;
        float desiredScale=Math.Min(1.5f,Math.Min(width/640f,height/480f));
        // A source pixel is two world pixels, as in the companion and vanilla
        // sprites. Keep it an integer number of screen pixels at any UI scale.
        float pixel=Math.Max(1,(float)Math.Round(2*desiredScale*Main.UIScale))/Main.UIScale;
        float scale=pixel/2;
        float Snap(float value)=>(float)Math.Round(value/pixel)*pixel;
        int age=Cart.Age;
        float progress=Progress(age),opacity=Math.Min(Math.Min(age/12f,(CompanionCartPlayer.Duration-age)/18f),1);
        // UI scaling changes sprite size, not the screen-space route or its
        // distance below the death message.
        float travel=(progress-.5f)*Math.Min(420,width*Main.UIScale*.45f)/Main.UIScale;
        Vector2 ground=new(Snap(width/2+travel),Snap(Math.Min(height-40,height/2+185/Main.UIScale)));
        int frame=Frame(age);
        var source=new Rectangle(0,frame*32,64,32);
        var spriteOrigin=new Vector2(32,24);
        // The passenger marker in the source sits at (40,12). Frames 1..3
        // have a one-pixel shift relative to the normalized wheel anchor.
        float seatX=frame is 1 or 2 or 3?39:40;
        var passenger=Cart.Passenger;
        using(new Main.CurrentPlayerOverride(passenger)){
            passenger.opacityForAnimation=opacity;
            Vector2 center=ground+new Vector2(seatX-32,12-24)*pixel;
            var at=center-new Vector2(passenger.width/2f,passenger.height)+new Vector2(0,passenger.height/2f)*scale;
            Main.PlayerRenderer.DrawPlayer(Main.Camera,passenger,at+Main.screenPosition,MathHelper.PiOver2,new Vector2(passenger.width/2f,passenger.height/2f),1-opacity,scale);
        }
        // Near sideboard occludes the real player's body, exactly where the
        // separate marker was drawn. Only the cat receives outfit color/dye.
        batch.Draw(CompanionSprite.Cart,ground,source,Color.White*opacity,0,spriteOrigin,pixel,SpriteEffects.None,0);
        var texture=CompanionPalette.CartTexture(Cart.Player.whoAmI,Cart.HoodColor);
        bool clothesOnly=CompanionProfile.Appearance(Cart.Player.GetModPlayer<CompanionPlayer>()).DyeClothesOnly;
        var data=new DrawData(texture,ground,source,Color.White*opacity,0,spriteOrigin,pixel,SpriteEffects.None);
        CompanionDye.DrawImmediate(batch,passenger,data,Cart.Dye,clothesOnly,CompanionSprite.CartCat,Cart.HoodColor);
        if(Cart.Caption!=null&&ModContent.GetInstance<CompanionConfig>().Chatter){
            string line=Language.GetTextValue(Cart.Caption);
            float textScale=Math.Min(.8f,(width-40)/Math.Max(1,FontAssets.MouseText.Value.MeasureString(line).X));
            Utils.DrawBorderString(batch,line,new Vector2(width/2,ground.Y+20),new Color(245,224,183)*opacity,textScale,.5f);
        }
        if(age<soundAge)soundAge=-1;
        if(age>=20&&soundAge<20&&ModContent.GetInstance<CompanionConfig>().CatSounds)
            SoundEngine.PlaySound(CompanionFeedback.VoiceStyle("Pet") with {Volume=.10f});
        soundAge=age;
    }
}
