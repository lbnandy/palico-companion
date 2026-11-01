using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class CompanionHudLayout : ModSystem {
    static bool previousLeft,dragging,moved;
    static Vector2 offset,position,origin;
    internal static bool Capturing { get; private set; }
    internal static bool InputBlocked=>PlayerInput.WritingText||Main.drawingPlayerChat||Main.editSign||Main.editChest||Main.blockInput||Main.ingameOptionsWindow;
    static CompanionConfig Config=>ModContent.GetInstance<CompanionConfig>();

    // Reserve the expanded card and the adjacent vanilla-sized food buff, even
    // while absent, so equipment and feeding never push the card around.
    static Vector2 Travel {
        get {
            var viewport=Main.graphics.GraphicsDevice.PresentationParameters;
            return new Vector2(Math.Max(0,viewport.BackBufferWidth/Main.UIScale-288-16),
                Math.Max(0,viewport.BackBufferHeight/Main.UIScale-126-16));
        }
    }
    static float Unit(float value)=>float.IsFinite(value)?Math.Clamp(value,0,1):0;
    static Vector2 Clamp(Vector2 value)=>Vector2.Clamp(value,new Vector2(8),Travel+new Vector2(8));
    internal static Rectangle Place(Rectangle automatic) {
        Vector2 point=automatic.TopLeft();
        if(dragging)point=position;
        else if(Config.HudPosition is Vector2 saved)point=new Vector2(8)+Travel*new Vector2(Unit(saved.X),Unit(saved.Y));
        point=Clamp(point);
        return new Rectangle((int)MathF.Round(point.X),(int)MathF.Round(point.Y),automatic.Width,automatic.Height);
    }
    internal static void Toggle() {
        Finish();
        Config.CompanionHud=!Config.CompanionHud;
        Config.SaveDisplayChanges();
    }
    static void Finish() {
        if(!dragging)return;
        dragging=false;
        if(!moved)return;
        var travel=Travel;var point=Clamp(position)-new Vector2(8);
        Config.HudPosition=new Vector2(travel.X>0?point.X/travel.X:0,travel.Y>0?point.Y/travel.Y:0);
        Config.SaveDisplayChanges();
    }
    public override void PostUpdateInput() {
        if(Main.dedServ)return;
        var input=PlayerInput.MouseInfo;
        bool left=input.LeftButton==ButtonState.Pressed;
        bool pressed=left&&!previousLeft;previousLeft=left;
        // If focus or visibility changes mid-drag, keep owning the held click
        // until release rather than turning it into a world item action.
        Capturing=dragging||Capturing&&left;
        if(!CompanionStatusBar.Visible||!Main.hasFocus||InputBlocked) {Finish();return;}
        var mouse=new Vector2(input.X,input.Y)/Main.UIScale;
        if(dragging) {
            position=Clamp(mouse-offset);
            moved|=Vector2.DistanceSquared(position,origin)>=4;
            Main.LocalPlayer.mouseInterface=true;
            if(!left)Finish();
            return;
        }
        bool alt=Main.keyState.IsKeyDown(Keys.LeftAlt)||Main.keyState.IsKeyDown(Keys.RightAlt);
        var card=CompanionStatusBar.Bounds(Main.LocalPlayer);
        if(pressed&&alt&&Main.mouseItem.IsAir&&card.Contains(mouse.ToPoint())) {
            position=origin=card.TopLeft();offset=mouse-position;
            dragging=Capturing=true;moved=false;
            Main.LocalPlayer.mouseInterface=true;
        }
    }
    static void Reset(){dragging=moved=Capturing=false;previousLeft=true;}
    public override void OnWorldLoad()=>Reset();
    public override void OnWorldUnload(){Finish();Reset();}
    public override void Unload()=>Reset();
}
