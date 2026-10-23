using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using ReLogic.Graphics;
namespace PalicoCompanion.Systems;

// Native resource UI lays buffs out from (32,76), 11 columns, 50px per row.
// Keep the compact companion entry below the actual last occupied buff row.
public static class CompanionStatusBar {
    static readonly UIPanel panel=new();
    static bool previousLeft;
    internal const float DetailTextScale=.62f;
    // Retain the HUD blue, sharing the backpack's native panel opacity.
    internal static readonly Color Background=CompanionPanelStyle.Tint(new Color(46,60,110));
    internal static readonly Color HoverBackground=CompanionPanelStyle.Tint(new Color(55,73,129));
    public static bool Visible=>!Main.gameMenu&&!Main.inFancyUI&&!Main.mapFullscreen&&!Main.playerInventory&&!Main.LocalPlayer.GetModPlayer<CompanionPlayer>().PanelOpen&&
        Main.netMode!=Terraria.ID.NetmodeID.Server&&ModContent.GetInstance<CompanionConfig>().CompanionHud&&ModContent.GetInstance<CompanionConfig>().Enabled;
    public static Rectangle Bounds(Player player){
        int last=-1;for(int i=0;i<player.buffType.Length;i++)if(player.buffType[i]>0)last=i;
        // Main.GUIHotbarDrawInner anchors slot 1 at 20 UI pixels.
        return new Rectangle(20,80+50*Math.Max(1,last/11+1),244,TrainingHud.HasEquipment(player.GetModPlayer<CompanionPlayer>())?126:76);
    }
    public static bool BlocksInput=>Visible&&Bounds(Main.LocalPlayer).Contains(new Point((int)(PlayerInput.MouseInfo.X/Main.UIScale),(int)(PlayerInput.MouseInfo.Y/Main.UIScale)));
    public static string State(CompanionPlayer s){
        if(s.Stowed)return CompanionLife.L("HudStowed");
        if(s.Vitals.Downed)return CompanionLife.L("HudRecovering")+$" {(s.Vitals.Recovery+59)/60}s";
        if(s.Paused)return CompanionLife.L("PausedState");
        if(s.Cat?.ModProjectile is CompanionCat c){
            if(s.Mining.Active||s.Digging.Active||s.Trees.Active||s.Garden.Active||s.Storage.Active||c.Catching.Active)return CompanionLife.L("HudWorking");
            if(c.Fishing.Active)return CompanionLife.L("HudFishing");
            if(c.Weapons.Busy)return CompanionLife.L("HudCombat");
            if(c.Supplies.Active)return CompanionLife.L("HudDelivering");
            if(c.Animation.Motion==CatMotion.Rest)return CompanionLife.L("RestState");
        }
        return CompanionLife.L("Following");
    }
    public static void Draw(CompanionPlayer s){
        bool clicked=Main.mouseLeft&&!previousLeft;previousLeft=Main.mouseLeft;
        if(!Visible||CompanionSprite.Atlas==null)return;
        Rectangle r=Bounds(s.Player);bool hover=r.Contains(Main.mouseX,Main.mouseY);
        panel.Left.Set(r.X,0);panel.Top.Set(r.Y,0);panel.Width.Set(r.Width,0);panel.Height.Set(r.Height,0);
        panel.BackgroundColor=hover?HoverBackground:Background;
        panel.BorderColor=new Color(16,22,39);panel.Recalculate();panel.Draw(Main.spriteBatch);
        string name=s.CatName;var font=FontAssets.MouseText.Value;
        while(name.Length>1&&font.MeasureString(name).X*.8f>152)name=name[..^1];
        Utils.DrawBorderString(Main.spriteBatch,name,new Vector2(r.X+76,r.Y+10),new Color(243,242,235),.8f);
        string amount=s.Vitals.Downed?$"{(int)(BarFraction(s)*100)}%":$"{s.Vitals.Health} / {s.Vitals.MaxHealth}";
        DrawDetails(s,r,amount);
        string skillHint=TrainingHud.HoverText(s,r);
        if(hover){s.Player.mouseInterface=true;Main.instance.MouseText(skillHint??(CompanionText.Get("CompanionStatusBar.Vitals",s.CatName,s.Vitals.Health,s.Vitals.MaxHealth,s.Vitals.Mana,s.Vitals.MaxMana)+"\n"+CompanionLife.L("HudHint")));if(clicked){int skill=TrainingHud.HitSkill(s,r);if(skill>=0)CompanionControl.Request(s,CompanionControlOp.Skill,skill);else if(skillHint==null)CompanionUIFlow.Toggle(s);}}
    }
    internal static void DrawDetails(CompanionPlayer s,Rectangle r,string amount){
        string state=State(s);var font=FontAssets.MouseText.Value;
        bool chinese=Terraria.Localization.Language.ActiveCulture.Name=="zh-Hans";
        const float amountScale=.70f;
        // A shared native UIText line keeps English numerals and descenders on
        // one baseline. Never resize health according to the current status.
        NativeLabelText.Draw(Main.spriteBatch,amount,new Rectangle(r.X+76,r.Y+47,64,22),new Color(220,227,242),amountScale);
        float scale=Math.Min(chinese?DetailTextScale:amountScale,82/Math.Max(1,font.MeasureString(state).X));
        if(chinese){
            // Keep Chinese status text aligned to its visible glyph bounds.
            var ink=CenteredButtonText.MeasureInk(state);
            Utils.DrawBorderString(Main.spriteBatch,state,new Vector2(r.X+228-ink.Z*scale,r.Y+58-(ink.Y+ink.W)*scale/2),new Color(182,198,224),scale);
        }else NativeLabelText.Draw(Main.spriteBatch,state,new Rectangle(r.X+146,r.Y+47,82,22),new Color(182,198,224),scale,1);
    }
    static SpriteBatch pixelBatch;
    static bool pixelLayerFailed;
    // Native-resolution head exported from the mod icon.
    public static readonly Rectangle PortraitSource=new(0,0,27,24);
    internal static Rectangle FitPortraitPixels(Rectangle bounds,float scale){
        float zoom=Math.Min(bounds.Width*scale/PortraitSource.Width,bounds.Height*scale/PortraitSource.Height);
        int width=(int)MathF.Round(PortraitSource.Width*zoom),height=(int)MathF.Round(PortraitSource.Height*zoom);
        return new Rectangle((int)MathF.Round(bounds.Center.X*scale-width/2f),(int)MathF.Round(bounds.Center.Y*scale-height/2f),width,height);
    }
    public static Rectangle PortraitPixels(Player p,float scale,Vector2 offset) {
        var r=Bounds(p);
        // Fit continuously with UI scale, like native UI images. Compensate
        // for the transparent source margin to retain the visible left edge.
        float width=44*scale,height=width*24/27;
        return new Rectangle(TrainingHud.LeftEdge(p.GetModPlayer<CompanionPlayer>(),scale,offset)+(int)MathF.Round(4*scale-width/27),
            (int)MathF.Round((r.Y+38)*scale+offset.Y-height/2f),(int)MathF.Round(width),(int)MathF.Round(height));
    }
    // Keep health-bar geometry on physical pixels. Portraits and skill slots
    // select native UI filtering separately within this layer.
    public static bool DrawPixelLayer(){
        if(!Visible)return true;
        try {
            pixelBatch??=new SpriteBatch(Main.graphics.GraphicsDevice);
            pixelBatch.Begin(SpriteSortMode.Immediate,BlendState.AlphaBlend,SamplerState.PointClamp,null,null,null,Matrix.Identity);
            try {DrawPixels(pixelBatch,Main.LocalPlayer.GetModPlayer<CompanionPlayer>(),Main.UIScale,Vector2.Zero);}
            finally {pixelBatch.End();}
            pixelLayerFailed=false;
        } catch(Exception error) {
            // A failed HUD pass must not cancel later interface layers, including
            // the cursor. Report once per failure streak and retry next frame.
            if(!pixelLayerFailed)ModContent.GetInstance<PalicoCompanion>().Logger.Error("Unable to draw the companion status card portrait or skill icons.",error);
            pixelLayerFailed=true;
        }
        return true;
    }
    public static void Release(){var old=pixelBatch;pixelBatch=null;pixelLayerFailed=false;Main.QueueMainThreadAction(()=>{old?.Dispose();TrainingHud.Release();});}
    public static float BarFraction(CompanionPlayer s)=>Math.Clamp(s.Vitals.Downed?s.Vitals.RecoveryProgress/(float)Math.Max(1,s.Vitals.RecoveryTotal):s.Vitals.Health/(float)Math.Max(1,s.Vitals.MaxHealth),0,1);
    public static int PixelUnit(float scale)=>Math.Max(1,(int)MathF.Round(2*scale));
    public static Rectangle BarPixels(Player p,float scale,Vector2 offset){
        var r=Bounds(p);int x=(int)MathF.Round((r.X+76)*scale+offset.X),y=(int)MathF.Round((r.Y+32)*scale+offset.Y);
        int unit=PixelUnit(scale);return new Rectangle(x,y,(int)MathF.Round(152*scale/unit)*unit,6*unit);
    }
    public static void DrawPixels(SpriteBatch batch,CompanionPlayer s,float scale,Vector2 offset){
        if(!Visible||CompanionSprite.Atlas==null)return;
        var portrait=PortraitPixels(s.Player,scale,offset);
        batch.End();
        batch.Begin(SpriteSortMode.Immediate,BlendState.AlphaBlend,SamplerState.LinearClamp,null,null,null,Matrix.Identity);
        try {CompanionDye.Portrait(batch,s,portrait,PortraitSource,s.Stowed?Color.Gray:Color.White);}
        finally {batch.End();batch.Begin(SpriteSortMode.Immediate,BlendState.AlphaBlend,SamplerState.PointClamp,null,null,null,Matrix.Identity);}
        var bar=BarPixels(s.Player,scale,offset);
        // Match the visual weight of the native skill slots at 80% inventory
        // scale; keep the bar's existing outer bounds and whole-pixel edges.
        int edge=Math.Max(1,(int)MathF.Round(1.6f*scale));
        void Rect(int x,int y,int width,int height,Color tint){
            if(width>0&&height>0)batch.Draw(TextureAssets.MagicPixel.Value,new Rectangle(bar.X+x,bar.Y+y,width,height),tint);
        }
        // Compute the entire bar in physical pixels first. Mirrored corners use
        // the same integer thickness even at fractional UI scales such as 147%.
        void Rounded(int x,int y,int width,int height,Color tint){
            if(width<=0||height<=0)return;
            int cut=width>=edge*2&&height>=edge*2?edge:0;
            Rect(x+cut,y,width-cut*2,height,tint);
            Rect(x,y+cut,width,height-cut*2,tint);
        }
        Rounded(0,0,bar.Width,bar.Height,new Color(22,29,50));
        int innerWidth=bar.Width-edge*2,innerHeight=bar.Height-edge*2;
        Rounded(edge,edge,innerWidth,innerHeight,new Color(28,38,65));
        float fraction=BarFraction(s);
        Color color=s.Vitals.Downed?new Color(112,170,216):fraction>.5f
            ?Color.Lerp(new Color(218,175,62),new Color(76,190,96),(fraction-.5f)*2)
            :Color.Lerp(new Color(205,70,67),new Color(218,175,62),fraction*2);
        if(s.Stowed)color=new Color(110,128,143);
        int width=fraction<=0?0:fraction>=1?innerWidth:
            Math.Min(innerWidth,Math.Max(edge,(int)MathF.Round(innerWidth/(float)edge*fraction)*edge));
        Rounded(edge,edge,width,innerHeight,color);
        Rect(edge*2,edge,width-edge*2,edge,Color.Lerp(color,Color.White,.25f));
        Rect(edge*2,bar.Height-edge*2,width-edge*2,edge,Color.Lerp(color,Color.Black,.22f));
        // Health geometry keeps nearest-neighbour sampling.
        // Item slots use the same LinearClamp sampler as vanilla inventory UI.
        batch.End();
        batch.Begin(SpriteSortMode.Immediate,BlendState.AlphaBlend,SamplerState.LinearClamp,null,null,null,Matrix.Identity);
        try {TrainingHud.DrawIcons(batch,s,Bounds(s.Player),scale,offset);}
        finally {batch.End();batch.Begin(SpriteSortMode.Immediate,BlendState.AlphaBlend,SamplerState.PointClamp,null,null,null,Matrix.Identity);}
    }
}
