using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
namespace PalicoCompanion.Systems;

// Native UIText draws buttons; compensate its fixed line box using visible
// glyph bounds so Chinese and Latin labels both sit at the visual centre.
internal static class CenteredButtonText {
    static readonly System.Collections.Generic.Dictionary<(Texture2D,Rectangle),Rectangle> inkBounds=new();
    static UIText nativeButtonText;
    static ReLogic.Graphics.DynamicSpriteFont measuredFont;
    internal static void Clear(){inkBounds.Clear();measuredFont=null;nativeButtonText=null;}
    // Run during update, outside every UI SpriteBatch. Decode compressed native
    // font pages through the GPU once per font, then retain only small bounds.
    // No textures/readbacks are allocated by Layout or Draw.
    internal static void PrepareFont() {
        if(Main.dedServ||FontAssets.MouseText?.IsLoaded!=true)return;
        var font=FontAssets.MouseText.Value;
        if(ReferenceEquals(font,measuredFont))return;
        Clear();
        var device=Main.graphics.GraphicsDevice;var targets=device.GetRenderTargets();var viewport=device.Viewport;var scissor=device.ScissorRectangle;
        using var batch=new SpriteBatch(device);
        try {
            foreach(var page in font.SpriteCharacters.Values.Append(font.DefaultCharacterData).GroupBy(g=>g.Texture)) {
                var texture=page.Key;if(texture==null)continue;
                using var rgba=new RenderTarget2D(device,texture.Width,texture.Height);
                device.SetRenderTarget(rgba);device.Clear(Color.Transparent);
                batch.Begin(SpriteSortMode.Deferred,BlendState.Opaque,SamplerState.PointClamp,null,null,null,Matrix.Identity);
                batch.Draw(texture,Vector2.Zero,Color.White);batch.End();
                device.SetRenderTargets(targets);
                var pixels=new Color[texture.Width*texture.Height];rgba.GetData(pixels);
                foreach(var glyph in page) {
                    var rect=glyph.Glyph;int left=rect.Width,top=rect.Height,right=-1,bottom=-1;
                    for(int y=0;y<rect.Height;y++)for(int x=0;x<rect.Width;x++) {
                        var pixel=pixels[(rect.Y+y)*texture.Width+rect.X+x];
                        if(pixel.A<128)continue;
                        left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);
                    }
                    inkBounds[(texture,rect)]=right<0?Rectangle.Empty:new Rectangle(left,top,right-left+1,bottom-top+1);
                }
            }
            measuredFont=font;
        }finally{device.SetRenderTargets(targets);device.Viewport=viewport;device.ScissorRectangle=scissor;}
    }
    static Rectangle Ink(ReLogic.Graphics.DynamicSpriteFont.SpriteCharacterData glyph) =>
        inkBounds.TryGetValue((glyph.Texture,glyph.Glyph),out var bounds)?bounds:new Rectangle(0,0,glyph.Glyph.Width,glyph.Glyph.Height);
    internal static Vector4 MeasureInk(string text) {
        var font=FontAssets.MouseText.Value;
        float pen=0,left=float.MaxValue,right=float.MinValue,top=float.MaxValue,bottom=float.MinValue;
        bool first=true;
        foreach(char c in text) {
            var glyph=font.SpriteCharacters.TryGetValue(c,out var data)?data:font.DefaultCharacterData;
            pen+=first?Math.Max(0,glyph.Kerning.X):font.CharacterSpacing+glyph.Kerning.X;
            if(!char.IsWhiteSpace(c)&&glyph.Glyph.Width>0&&glyph.Glyph.Height>0) {
                var ink=Ink(glyph);
                if(ink.Width>0){
                    left=Math.Min(left,pen+glyph.Padding.X+ink.Left);right=Math.Max(right,pen+glyph.Padding.X+ink.Right);
                    top=Math.Min(top,glyph.Padding.Y+ink.Top);bottom=Math.Max(bottom,glyph.Padding.Y+ink.Bottom);
                }
            }
            pen+=glyph.Kerning.Y+glyph.Kerning.Z;first=false;
        }
        return left==float.MaxValue?Vector4.Zero:new Vector4(left,top,right,bottom);
    }
    internal static (Vector2 Position,float Scale) Layout(string text,Rectangle box,float maxScale=.85f) {
        var ink=MeasureInk(text);float left=ink.X,top=ink.Y,right=ink.Z,bottom=ink.W;
        if(ink==Vector4.Zero)return (box.Center.ToVector2(),maxScale);
        float scale=Math.Min(maxScale,Math.Min((box.Width-16)/Math.Max(1,right-left),(box.Height-8)/Math.Max(1,bottom-top)));
        return (new Vector2(box.X+box.Width/2f-(left+right)*scale/2f,box.Y+box.Height/2f-(top+bottom)*scale/2f),scale);
    }
    internal static bool Close(SpriteBatch batch,Rectangle bounds,Point mouse,bool clicked) {
        bool hover=bounds.Contains(mouse);
        Draw(batch,"X",bounds,hover?Main.OurFavoriteColor:Color.White,.9f);
        if(!hover)return false;
        Main.LocalPlayer.mouseInterface=true;
        Main.instance.MouseText(Terraria.Localization.Language.GetTextValue("LegacyInterface.52"));
        if(!clicked)return false;
        Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuClose);
        return true;
    }
    internal static float NativeVerticalOffset(string text,float scale){
        var ink=MeasureInk(text);
        return ink==Vector4.Zero?0:(10f-(ink.Y+ink.W)/2f)*scale;
    }
    internal static void Draw(SpriteBatch batch,string text,Rectangle box,Color color,float maxScale=.85f) {
        // UIText positions the baseline 10 scaled units above the box centre
        // (half its 16-unit line plus its 2-unit offset). Correct only that Y.
        var ui=nativeButtonText??=new UIText(""){TextOriginX=.5f,TextOriginY=.5f};
        float textWidth=Terraria.UI.Chat.ChatManager.GetStringSize(FontAssets.MouseText.Value,text,Vector2.One).X;
        float scale=Math.Min(maxScale,Math.Min((box.Width-16)/Math.Max(1,textWidth),(box.Height-8)/16f));
        ui.SetPadding(0);ui.SetText(text,scale,false);ui.TextColor=color;
        float verticalOffset=NativeVerticalOffset(text,scale);
        ui.Left.Set(box.X,0);ui.Top.Set(box.Y+verticalOffset,0);ui.Width.Set(box.Width,0);ui.Height.Set(box.Height,0);
        ui.Recalculate();ui.Draw(batch);
    }
}

// Both companion windows use the same title row. The X has an invisible
// 32px target with 8px edge inset; every header control shares center Y=24.
internal static class CompanionPanelHeader {
    internal static Rectangle CloseBounds(Rectangle panel)=>new(panel.Right-40,panel.Y+8,32,32);
    internal static Rectangle ButtonBounds(Rectangle panel,int width,int rightOffset=0)=>new(panel.Right-48-rightOffset-width,panel.Y+9,width,30);
    internal static Point TitlePosition(string text,Rectangle panel)=>new(panel.X+16,(int)Math.Round(CenteredButtonText.Layout(text,new Rectangle(0,panel.Y+5,4096,38),1f).Position.Y));
}

internal static class CompanionButtonLayout {
    internal const float TextScale=.8f;
    internal const int Height=30,Gap=8;
    internal static int ContentWidth(string text)=>(int)Math.Ceiling(FontAssets.MouseText.Value.MeasureString(text).X*TextScale)+24;
}

// Native single-line placement for paired labels and values. A common line
// box preserves the baseline even when one word contains g, p or y.
internal static class NativeLabelText {
    static readonly UIText label=new(""){TextOriginY=.5f};
    internal static void Draw(SpriteBatch batch,string text,Rectangle box,Color color,float scale,float align=0){
        scale=Math.Min(scale,box.Width/Math.Max(1,Terraria.UI.Chat.ChatManager.GetStringSize(FontAssets.MouseText.Value,text,Vector2.One).X));
        label.TextOriginX=align;label.SetPadding(0);label.SetText(text,scale,false);label.TextColor=color;
        label.Left.Set(box.X,0);label.Top.Set(box.Y,0);label.Width.Set(box.Width,0);label.Height.Set(box.Height,0);
        label.Recalculate();label.Draw(batch);
    }
}

internal sealed class CenteredTextButton:UIPanel {
    readonly Terraria.Localization.LocalizedText label;
    internal CenteredTextButton(Terraria.Localization.LocalizedText label)=>this.label=label;
    protected override void DrawSelf(SpriteBatch batch) {
        base.DrawSelf(batch);CenteredButtonText.Draw(batch,label.Value,GetDimensions().ToRectangle(),Color.White,1f);
    }
}

internal sealed class CenteredButtonMetrics:Terraria.ModLoader.ModSystem {
    public override void PostUpdateEverything()=>CenteredButtonText.PrepareFont();
}
