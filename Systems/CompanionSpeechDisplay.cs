using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace PalicoCompanion.Systems;

// Presentation only: the existing expression/pacing and owner-only network
// delivery decide what is said. Never borrow a real player's overhead message.
public sealed class CompanionSpeechDisplay : ModSystem {
    Player.OverheadMessage message;
    Projectile anchor;
    int identity;
    string chatLine;
    bool logged;
    static CompanionSpeechMode Mode=>ModContent.GetInstance<CompanionConfig>().SpeechDisplay;

    internal static void Show(CompanionPlayer owner,string text) {
        if(!CompanionExpression.IsLocalListener(owner)||!CompanionExpression.ChatEnabled(owner))return;
        var display=ModContent.GetInstance<CompanionSpeechDisplay>();
        display.Clear();
        display.anchor=owner.Cat;
        display.identity=display.anchor?.identity??-1;
        display.chatLine=owner.CatName+"："+text;
        display.message.NewMessage(text,Main.PlayerOverheadChatMessageDisplayTime);
        display.message.color=Color.SandyBrown;
        // Vanilla player chat has no width limit. Reuse its chat wrapping for
        // longer localized callouts, keeping the full text and native snippets.
        if(display.message.messageSize.X>360f){
            var lines=Utils.WordwrapStringSmart(text,Color.White,FontAssets.MouseText.Value,360,int.MaxValue);
            var snippets=new List<TextSnippet>();
            foreach(var line in lines){
                if(snippets.Count>0)snippets.Add(new TextSnippet("\n"));
                snippets.AddRange(line);
            }
            display.message.snippets=snippets.ToArray();
            display.message.messageSize=ChatManager.GetStringSize(FontAssets.MouseText.Value,display.message.snippets,Vector2.One);
        }
        if(Mode is CompanionSpeechMode.Chat or CompanionSpeechMode.Both ||
            Mode==CompanionSpeechMode.Automatic&&!display.HasAnchor(owner))display.WriteChat();
    }

    public override void Load(){if(!Main.dedServ)On_Main.DrawPlayerChatBubbles+=Draw;}
    public override void Unload(){On_Main.DrawPlayerChatBubbles-=Draw;Clear();}
    public override void OnWorldUnload()=>Clear();
    void Clear(){message=default;anchor=null;chatLine=null;logged=false;}

    bool HasAnchor(CompanionPlayer owner)=>anchor is {active:true} && anchor.identity==identity &&
        owner.Cat==anchor && !CompanionRestVisual.Hidden(owner.Vitals);

    void WriteChat(){
        // One receipt per utterance, shared by every display mode. Moving back
        // and forth, multiple draws, and changing modes must not copy it again.
        if(logged||chatLine==null)return;
        logged=true;
        Main.NewText(chatLine,Color.SandyBrown);
    }

    public override void PostUpdateEverything(){
        if(Main.dedServ||message.timeLeft<=0)return;
        var owner=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();
        if(Main.gameMenu||!ModContent.GetInstance<CompanionConfig>().Chatter||!owner.Enabled||owner.Stowed){Clear();return;}
        if(Mode is CompanionSpeechMode.Chat or CompanionSpeechMode.Both)WriteChat();
        if(!HasAnchor(owner)){
            if(Mode==CompanionSpeechMode.Automatic)WriteChat();
            Clear();return;
        }
        if(--message.timeLeft==0)Clear();
    }

    Vector2 Position(){
        // The same UI-scale batch and world projection used by player chat.
        // Reserve a stable lane for emotes so text never jumps when one closes.
        Vector2 head=anchor.Bottom+new Vector2(0,anchor.gfxOffY-CompanionSpriteLayout.StandingHeight);
        Vector2 at=head.ToScreenPosition();
        at-=new Vector2(message.messageSize.X/2f,message.messageSize.Y+2f+
            (ModContent.GetInstance<CompanionConfig>().Emotes?36f:0f));
        if(Main.LocalPlayer.gravDir==-1f)at.Y=Main.screenHeight/Main.UIScale-at.Y-message.messageSize.Y;
        return at.Floor();
    }

    bool FitsOnScreen(Vector2 at){
        Vector2 size=new(Main.screenWidth/Main.UIScale,Main.screenHeight/Main.UIScale);
        Vector2 center=anchor.Center.ToScreenPosition();
        if(Main.LocalPlayer.gravDir==-1f)center.Y=size.Y-center.Y;
        // Test the actual projected text, including its outline, rather than
        // distance from the player: zoom, camera movement and UI scale matter.
        const float border=2f;
        return center.X>=0&&center.Y>=0&&center.X<size.X&&center.Y<size.Y &&
            at.X>=border&&at.Y>=border&&at.X+message.messageSize.X+border<=size.X&&
            at.Y+message.messageSize.Y+border<=size.Y;
    }

    bool Present(out Vector2 at){
        at=default;
        if(message.timeLeft<=0||Main.gameMenu||!ModContent.GetInstance<CompanionConfig>().Chatter)return false;
        var owner=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();
        if(!owner.Enabled||owner.Stowed)return false;
        if(Mode is CompanionSpeechMode.Chat or CompanionSpeechMode.Both)WriteChat();
        if(!HasAnchor(owner)){
            if(Mode==CompanionSpeechMode.Automatic)WriteChat();
            return false;
        }
        at=Position();
        if(Mode==CompanionSpeechMode.Chat)return false;
        if(Mode==CompanionSpeechMode.Automatic&&!FitsOnScreen(at)){WriteChat();return false;}
        return true;
    }

    void Draw(On_Main.orig_DrawPlayerChatBubbles orig,Main self){
        orig(self);
        // Evaluate visibility with the finalized drawing camera, not the
        // previous frame's projection during a simulation update.
        if(!Present(out Vector2 at))return;
        ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch,FontAssets.MouseText.Value,
            message.snippets,at,0f,message.color,Vector2.Zero,Vector2.One,out _);
    }
}
