using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.UI;
using Terraria.Map;
using Terraria.DataStructures;
using Terraria.GameContent;
namespace PalicoCompanion.Systems;

public sealed class CompanionInteraction:ModSystem {
    public static int HintDraws {get;private set;}
    public static bool Hovered {get;private set;}
    public static Vector2 WorldCursor(Vector2 raw,Vector2 screen,Matrix view)=>Vector2.Transform(raw,Matrix.Invert(view))+screen;
    // Main.HoverOverNPCs uses this player-centered tile-range rectangle,
    // not a circular range or a line-of-sight check.
    public static Rectangle TalkRange(Player p)=>new((int)p.Center.X-Player.tileRangeX*16,(int)p.Center.Y-Player.tileRangeY*16,Player.tileRangeX*32,Player.tileRangeY*32);
    public static bool CanInteract(Player p,Projectile cat,Vector2 cursor)=>cat is {active:true}&&!p.dead&&!p.GetModPlayer<CompanionPlayer>().Stowed&&!CompanionRestVisual.Hidden(p.GetModPlayer<CompanionPlayer>().Vitals)&&
        TalkRange(p).Intersects(cat.Hitbox)&&
        new Rectangle((int)cat.Center.X-21,(int)cat.Bottom.Y-42,42,44).Contains(cursor.ToPoint());
    // UpdateUI precedes this frame's PlayerInput.UpdateInput. Checking the native
    // release latch there sees the previous draw's already-consumed click.
    // Refresh hit testing after input for item suppression; handle clicks in the
    // interface draw pass, alongside vanilla NPC interaction and before its latch reset.
    public override void PostUpdateInput()=>RefreshHover();
    void RefreshHover(){
        Hovered=false;
        if(Main.gameMenu||Main.netMode==NetmodeID.Server||Main.inFancyUI||Main.mapFullscreen||Terraria.GameInput.PlayerInput.WritingText)return;
        var p=Main.LocalPlayer;var s=p.GetModPlayer<CompanionPlayer>();
        if(s.PanelOpen||p.mouseInterface||!Main.mouseItem.IsAir||!CanInteract(p,s.Cat,WorldCursor(new Vector2(Terraria.GameInput.PlayerInput.MouseInfo.X,Terraria.GameInput.PlayerInput.MouseInfo.Y),Main.screenPosition,Main.GameViewMatrix.TransformationMatrix)))return;
        Hovered=true;
    }
    internal void HandleWorldInteraction(){
        RefreshHover();
        if(!Hovered)return;
        var p=Main.LocalPlayer;var s=p.GetModPlayer<CompanionPlayer>();
        if(Main.mouseRight&&Main.npcChatRelease&&!p.tileInteractionHappened){
            Main.npcChatRelease=false;p.tileInteractionHappened=true;p.mouseInterface=true;
            p.SetTalkNPC(-1);Main.npcChatText="";p.sign=-1;Main.editSign=false;
            CompanionUIFlow.Bag(s);Terraria.Audio.SoundEngine.PlaySound(SoundID.Chat);
        }
    }
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers){
        int i=layers.FindIndex(l=>l.Name=="Vanilla: Mouse Text");
        if(i>=0){
            layers.Insert(i,new LegacyGameInterfaceLayer("PalicoCompanion: Location",Draw,InterfaceScaleType.UI));
            layers.Insert(i+1,new LegacyGameInterfaceLayer("PalicoCompanion: Crisp portrait",CompanionStatusBar.DrawPixelLayer,InterfaceScaleType.None));
        }
    }
    public override void Unload()=>CompanionStatusBar.Release();
    public static Vector2 EdgePoint(Vector2 point,Vector2 size){
        Vector2 center=size*.5f,delta=point-center;
        float ratio=Math.Min((center.X-80)/Math.Max(1,Math.Abs(delta.X)),(center.Y-65)/Math.Max(1,Math.Abs(delta.Y)));
        return center+delta*Math.Min(1,Math.Max(0,ratio));
    }
    internal bool Draw(){
        if(Main.gameMenu||Main.netMode==NetmodeID.Server)return true;
        HandleWorldInteraction();
        var s=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();
        CompanionStatusBar.Draw(s);
        if(Hovered){
            Main.instance.MouseText($"{s.CatName}: {s.Vitals.Health}/{s.Vitals.MaxHealth}");
            if(s.Cat is {} near){
                DrawChatBubble(near);
            }
        }
        if(CompanionRestVisual.Hidden(s.Vitals)||!ModContent.GetInstance<CompanionConfig>().CompanionMarker||s.Cat is not {} cat||Main.mapFullscreen||Main.inFancyUI)return true;
        var back=Main.graphics.GraphicsDevice.PresentationParameters;
        Vector2 size=new(back.BackBufferWidth/Main.UIScale,back.BackBufferHeight/Main.UIScale);
        Vector2 at=Vector2.Transform(cat.Center-Main.screenPosition,Main.GameViewMatrix.TransformationMatrix)/Main.UIScale;
        if(at.X>=0&&at.X<=size.X&&at.Y>=0&&at.Y<=size.Y)return true;
        Vector2 edge=EdgePoint(at,size),delta=at-size*.5f;
        string arrow=Math.Abs(delta.X)>Math.Abs(delta.Y)?delta.X<0?"<":">":delta.Y<0?"^":"v";
        string note=s.Vitals.Downed?$" · {(s.Vitals.Recovery+59)/60}s":"";
        Utils.DrawBorderString(Main.spriteBatch,$"{arrow} {s.CatName}{note}",edge,Color.SandyBrown,.75f,.5f,.5f);
        Utils.DrawBorderString(Main.spriteBatch,$"{(int)(Vector2.Distance(cat.Center,s.Player.Center)/16)} {CompanionLife.L("Tiles")}",edge+new Vector2(0,18),Color.White,.65f,.5f,.5f);
        return true;
    }
    // Adapt Main.DrawNPCChatBubble's native texture, side offset, flip and pulse
    // to the companion's visible hood. No dummy NPC or town/housing state.
    static void DrawChatBubble(Projectile cat){
        var texture=TextureAssets.Chat.Value;
        int side=cat.direction<0?-1:1;
        var point=new Vector2(cat.Center.X+side*(cat.width/2+8)-texture.Width/2,
            cat.Bottom.Y-42+cat.gfxOffY-texture.Height)-Main.screenPosition;
        var effects=side<0?SpriteEffects.FlipHorizontally:SpriteEffects.None;
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,null,null,null,Main.GameViewMatrix.TransformationMatrix);
        byte pulse=Main.mouseTextColor;
        Main.spriteBatch.Draw(texture,point,null,new Color(pulse,pulse,pulse,pulse),0,Vector2.Zero,1,effects,0);
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,null,null,null,Main.UIScaleMatrix);
        HintDraws++;
    }
}
// The supported vanilla overlay pipeline handles zoom, clipping and all three map modes.
public sealed class CompanionMapLayer:ModMapLayer {
    public int Draws {get;private set;}
    Texture2D head;int headColor=-1;
    NPCHeadDrawRenderTargetContent outlined;
    internal Texture2D Head {
        get {
            int rgb=Main.LocalPlayer.GetModPlayer<CompanionPlayer>().HoodColor;
            if(head!=null&&headColor==rgb)return head;
            var crop=new Rectangle(7,5,17,16);
            var pixels=new Color[crop.Width*crop.Height];
            CompanionSprite.Atlas.GetData(0,crop,pixels,0,pixels.Length);
            // Close only this map crop under the hood; exclude the shoulder,
            // hand and back equipment. The world/portrait atlas is untouched.
            for(int x=0;x<17;x++){
                if(x<3||x>14)pixels[13*17+x]=Color.Transparent;
                pixels[14*17+x]=x>=3&&x<=14?new Color(48,62,109):Color.Transparent;
                pixels[15*17+x]=x>=4&&x<=13?new Color(32,36,56):Color.Transparent;
            }
            // A 12x11 logical head, doubled to native two-pixel sprite density.
            // The stock renderer adds its own 4px margin on all sides.
            var canvas=new Color[24*22];
            for(int y=0;y<11;y++)for(int x=0;x<12;x++){
                int sx=Math.Min(16,(int)((x+.5f)*17/12)),sy=Math.Min(15,(int)((y+.5f)*16/11));
                var color=CompanionPalette.Recolor(pixels[sy*17+sx],rgb);
                for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)canvas[(y*2+dy)*24+x*2+dx]=color;
            }
            if(head==null){head=new Texture2D(Main.graphics.GraphicsDevice,24,22);outlined=new NPCHeadDrawRenderTargetContent();outlined.SetTexture(head);Main.ContentThatNeedsRenderTargets.Add(outlined);}
            head.SetData(canvas);headColor=rgb;
            return head;
        }
    }
    internal void PreparePreview(GraphicsDevice device,SpriteBatch batch){
        _=Head;outlined.UseColor(Main.GetPlayerHeadBordersColor(Main.LocalPlayer));outlined.Request();outlined.PrepareRenderTarget(device,batch);
    }
    public override void Unload(){var texture=head;var content=outlined;head=null;outlined=null;Main.QueueMainThreadAction(()=>{if(content!=null){Main.ContentThatNeedsRenderTargets.Remove(content);content.GetTarget()?.Dispose();content.Reset();}texture?.Dispose();});}
    public override void Draw(ref MapOverlayDrawContext context,ref string text){
        if(Main.gameMenu||Main.netMode==NetmodeID.Server||!ModContent.GetInstance<CompanionConfig>().CompanionMarker)return;
        var s=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();
        if(CompanionRestVisual.Hidden(s.Vitals)||s.Cat is not {} cat||CompanionSprite.Atlas==null)return;
        _=Head;
        // This is the actual vanilla outlined-head pipeline, shared by player
        // and NPC heads: inner team-coloured 2px ring, outer black 2px ring.
        outlined.UseColor(Main.GetPlayerHeadBordersColor(s.Player));outlined.Request();
        if(!outlined.IsReady)return;
        var result=context.Draw(outlined.GetTarget(),cat.Center/16f,s.Vitals.Downed?Color.LightCoral:Color.White,new SpriteFrame(1,1),1f,Main.mapFullscreen?1.5f:1f,Alignment.Center);
        Draws++;if(result.IsMouseOver)text=s.CatName+" · "+(s.Vitals.Downed?CompanionLife.L("RecoveryWait"):CompanionLife.L("Following"));
    }
}
