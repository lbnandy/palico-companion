using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;

namespace PalicoCompanion.Systems;

public sealed class CompanionControlUI:ModSystem {
    static bool previousLeft;
    static bool directHeld;
    static string result="";
    static int resultTicks;
    static readonly UIPanel toast=new();
    internal static void Notify(string text){if(Main.dedServ)return;result=text;resultTicks=180;}
    public override void PostUpdateInput(){directHeld=false;if(resultTicks>0)resultTicks--;}
    internal static bool Aiming=>!Main.gameMenu&&!Main.dedServ&&!Main.inFancyUI&&!Main.mapFullscreen&&!PlayerInput.WritingText&&directHeld;
    internal static Vector2 WorldPointer(){
        int mx=Main.mouseX,my=Main.mouseY,lx=Main.lastMouseX,ly=Main.lastMouseY,sw=Main.screenWidth,sh=Main.screenHeight;
        try{PlayerInput.SetZoom_Unscaled();PlayerInput.SetZoom_MouseInWorld();return Main.MouseWorld;}
        finally{Main.mouseX=mx;Main.mouseY=my;Main.lastMouseX=lx;Main.lastMouseY=ly;Main.screenWidth=sw;Main.screenHeight=sh;}
    }
    internal static void Input(CompanionPlayer s) {
        // Called from ProcessTriggers, after tModLoader prepares mod keybinds.
        // Drawing can run before that first input update (e.g. while unfocused).
        directHeld=PalicoCompanion.DirectKey?.Current==true;
        bool click=Main.mouseLeft&&!previousLeft;previousLeft=Main.mouseLeft;
        if(!Aiming)return;
        if(s.Player.mouseInterface)return;
        s.Player.controlUseItem=false;s.Player.controlUseTile=false;
        if(click&&Main.mouseItem.IsAir) {
            var kind=CompanionDirective.Identify(s,WorldPointer(),out var cell);
            if(kind!=DirectiveKind.None)CompanionControl.Request(s,CompanionControlOp.Direct,(int)kind,cell);
            else Notify(CompanionText.Get("CompanionControlUI.AimHint"));
        }
    }
    public override void OnWorldLoad(){directHeld=false;previousLeft=true;resultTicks=0;}
    public override void OnWorldUnload()=>directHeld=false;
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
        int index=layers.FindIndex(l=>l.Name=="Vanilla: Mouse Text");
        if(index>=0)layers.Insert(index,new LegacyGameInterfaceLayer("PalicoCompanion: Direction",()=>{
            if(!Main.gameMenu&&!Main.inFancyUI&&resultTicks>0) {
                string visible=CompanionCopy.Status(result);
                var device=Main.graphics.GraphicsDevice.PresentationParameters;
                int width=(int)(device.BackBufferWidth/Main.UIScale),height=(int)(device.BackBufferHeight/Main.UIScale);
                float scale=System.Math.Min(.8f,(width-60)/System.Math.Max(1,FontAssets.MouseText.Value.MeasureString(visible).X));
                int size=(int)(FontAssets.MouseText.Value.MeasureString(visible).X*scale)+24;
                var box=new Rectangle((width-size)/2,height-106,size,34);
                toast.Left.Set(box.X,0);toast.Top.Set(box.Y,0);toast.Width.Set(box.Width,0);toast.Height.Set(box.Height,0);toast.Recalculate();toast.Draw(Main.spriteBatch);
                CenteredButtonText.Draw(Main.spriteBatch,visible,box,Color.White,scale);
            }
            if(Aiming&&!Main.LocalPlayer.mouseInterface) {
                var s=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();
                // Interface layers use UI-scaled coordinates. Match vanilla
                // DrawMouseOver's world-space conversion before hit testing.
                var kind=CompanionDirective.Identify(s,WorldPointer(),out _);
                string text=CompanionDirective.Label(kind)+(kind==DirectiveKind.None?"":CompanionText.Get("CompanionControlUI.ConfirmHint"));
                // Native cursor tooltip: no opaque wheel or permanent marker.
                Main.instance.MouseText(text,null,noOverride:true);
                Main.mouseText=true; // Native world-item labels must not overwrite the command.
            }
            return true;
        },InterfaceScaleType.UI));
    }
}
