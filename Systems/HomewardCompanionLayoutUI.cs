using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using ContinentOfJourney.Items.Accessories.MechArms;

namespace PalicoCompanion.Systems;

[ExtendsFromMod("ContinentOfJourney")]
[JITWhenModsEnabled("ContinentOfJourney")]
public sealed class HomewardCompanionLayoutUI:ModSystem {
    // Reuse Journey's grid and claw rules; inventory receipts retain ownership.
    Action<SpriteBatch,Vector2,int> fill,item;
    Action<SpriteBatch,Vector2,int,int> borders;
    bool open,positioned,dragging,justOpened;
    Vector2 position,dragOffset;
    Rectangle window,anchor;
    int radius,columns,gridRows;
    float scale;
    static string L(string key)=>Language.GetTextValue("Mods.PalicoCompanion.Arms."+key);
    static T Bind<T>(string method) where T:Delegate=>typeof(MechArmUI).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).CreateDelegate<T>();
    static bool Available(CompanionPlayer s)=>HomewardCompanionBackpack.Arms(s) is {ArmsList:not null} mech&&(mech.MechPackTier>0||s.ArmLayout.Count>0);
    bool Visible=>open&&CompanionUIFlow.Visible&&Main.LocalPlayer.GetModPlayer<CompanionPlayer>() is {} state&&state.PanelOpen&&Available(state);
    public override void Load(){
        if(Main.dedServ)return;
        fill=Bind<Action<SpriteBatch,Vector2,int>>("DrawFill");item=Bind<Action<SpriteBatch,Vector2,int>>("DrawItem");
        borders=Bind<Action<SpriteBatch,Vector2,int,int>>("DrawBorders");
        CompanionCrossMod.ArmLayoutUI=DrawEntry;CompanionCrossMod.ArmLayoutAvailable=Available;
        CompanionCrossMod.ArmLayoutBounds=Bounds;
        CompanionCrossMod.ArmLayoutBlocks=pointer=>Visible&&(dragging||Bounds().Contains(pointer));
        CompanionCrossMod.ArmLayoutOverlay=DrawWindow;
    }
    public override void Unload(){
        CompanionCrossMod.ArmLayoutUI=null;CompanionCrossMod.ArmLayoutAvailable=null;CompanionCrossMod.ArmLayoutBounds=null;
        CompanionCrossMod.ArmLayoutBlocks=null;CompanionCrossMod.ArmLayoutOverlay=null;fill=null;item=null;borders=null;
    }
    public override void OnWorldUnload(){open=positioned=dragging=false;window=Rectangle.Empty;}
    public override void UpdateUI(GameTime gameTime){
        if(!Main.mouseLeft)dragging=false;
        if(Main.gameMenu||!Main.LocalPlayer.GetModPlayer<CompanionPlayer>().PanelOpen){open=dragging=false;}
    }
    Rectangle Bounds(){if(!Visible)return Rectangle.Empty;Arrange(Main.LocalPlayer.GetModPlayer<CompanionPlayer>());return window;}
    void Arrange(CompanionPlayer state){
        var mech=HomewardCompanionBackpack.Arms(state);
        int selected=!Main.mouseItem.IsAir&&MechArmPlayer.MechArmIDs.ContainsKey(Main.mouseItem.type)?Main.mouseItem.type:0;
        radius=selected>0?mech.GetShowRangeFromType(selected):1;
        for(int key=0;key<state.ArmLayout.Items.Length;key++)if(!state.ArmLayout.Items[key].IsAir)
            radius=Math.Max(radius,Math.Max(Math.Abs(key/13-6),Math.Abs(key%13-6)-1));
        radius=Math.Clamp(radius,1,6);columns=Math.Min(13,radius*2+3);gridRows=radius*2+1;
        float fit=ModContent.GetInstance<CompanionPanel>().Fit;
        var buffer=Main.graphics.GraphicsDevice.PresentationParameters;
        int sw=(int)(buffer.BackBufferWidth/(Main.UIScale*fit)),sh=(int)(buffer.BackBufferHeight/(Main.UIScale*fit));
        scale=Math.Min(1,Math.Min((sw-52)/(columns*45f+44),(sh-28)/(gridRows*45f+20)));
        int width=(int)Math.Ceiling((columns*45+44)*scale),height=(int)Math.Ceiling((gridRows*45+20)*scale);
        if(!positioned){
            position=new Vector2(anchor.Right+width+12<=sw?anchor.Right+8:anchor.Left-width-8,anchor.Top+56);
            positioned=true;
        }
        position.X=Math.Clamp(position.X,4,Math.Max(4,sw-width-4));position.Y=Math.Clamp(position.Y,4,Math.Max(4,sh-height-4));
        window=new Rectangle((int)position.X,(int)position.Y,width,height);
    }
    bool DrawEntry(SpriteBatch sb,CompanionPlayer state,Rectangle area,Rectangle entry,Point mouse,bool clicked,bool rightClicked){
        anchor=area;
        if(!Available(state)){open=false;return false;}
        var texture=MechArmUI.buttonTex;
        sb.Draw(texture,entry,new Rectangle(0,open?texture.Height/2:0,texture.Width,texture.Height/2),Color.White);
        CompanionGamepad.Add("arms-open",entry);
        if(entry.Contains(mouse)){
            Main.LocalPlayer.mouseInterface=true;Main.instance.MouseText(L("Title")+"\n"+L("Hint")+"\n"+L("ItemsHint"));
            if(clicked){open=!open;dragging=false;justOpened=open;if(open){Main.playerInventory=true;Arrange(state);}}
        }
        return false;
    }
    void DrawWindow(SpriteBatch sb,CompanionPlayer s,Point mouse,bool clicked){
        if(!Visible)return;
        if(justOpened){clicked=false;justOpened=false;}
        if(!Main.mouseLeft)dragging=false;
        if(dragging)position=mouse.ToVector2()-dragOffset;
        Arrange(s);
        var center=new Vector2(window.X+(10+columns*45/2f)*scale,window.Y+(10+gridRows*45/2f)*scale);
        var handle=new Rectangle((int)(center.X+(columns*45/2f+4)*scale),(int)(center.Y-32*scale),(int)(28*scale),(int)(64*scale));
        if(window.Contains(mouse)||dragging)Main.LocalPlayer.mouseInterface=true;
        if(handle.Contains(mouse)){
            Main.instance.MouseText(L("DragHint"));
            if(clicked&&Main.mouseItem.IsAir){dragging=true;dragOffset=mouse.ToVector2()-position;}
            if(Main.mouseRight&&Main.mouseRightRelease){positioned=false;dragging=false;}
        }
        var mech=HomewardCompanionBackpack.Arms(s);
        var display=new SortedDictionary<byte,int>();
        for(int key=0;key<s.ArmLayout.Items.Length;key++)if(!s.ArmLayout.Items[key].IsAir)display[(byte)key]=s.ArmLayout.Items[key].type;
        int selected=!Main.mouseItem.IsAir&&MechArmPlayer.MechArmIDs.ContainsKey(Main.mouseItem.type)?Main.mouseItem.type:0;
        bool ready=!dragging&&!CompanionNetwork.Pending&&!CompanionSession.Closing&&CompanionNetwork.CanMoveInventory(s.Player);
        float fit=ModContent.GetInstance<CompanionPanel>().Fit;
        void Begin(float factor)=>sb.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,null,null,null,Matrix.CreateScale(factor*fit)*Main.UIScaleMatrix);
        float opacity=MechArmUI.Opacity;
        sb.End();Begin(scale);
        try{
            MechArmUI.Opacity=1;
            DrawBackground(sb,center/scale,columns*45,gridRows*45);
            for(int y=-radius;y<=radius;y++)for(int x=-Math.Min(6,radius+1);x<=Math.Min(6,radius+1);x++){
                if(x==0&&y==0)continue;
                byte key=(byte)((y+6)*13+x+6);
                var point=center+new Vector2(x,y)*45*scale;
                var rect=new Rectangle((int)(point.X-22*scale),(int)(point.Y-22*scale),(int)(44*scale),(int)(44*scale));
                bool hover=rect.Contains(mouse),occupied=display.TryGetValue(key,out int type);
                bool legal=selected>0&&HomewardCompanionBackpack.Accepts(s,Main.mouseItem,key);
                fill(sb,point/scale,hover&&legal?3:occupied?2:legal?0:1);
                bool active=occupied&&mech.ArmsList.TryGetValue(key,out int installed)&&installed==type;
                if(occupied){Main.instance.LoadItem(type);MechArmUI.Opacity=active?1:.35f;item(sb,point/scale,type);MechArmUI.Opacity=1;}
                CompanionGamepad.Add("arm-cell-"+key,rect);
                if(!hover)continue;
                if(occupied){Main.HoverItem=s.ArmLayout.Items[key].Clone();Main.instance.MouseText(Lang.GetItemNameValue(type)+"\n"+L(active?"MoveHint":"Inactive"));}
                if(!ready||!clicked)continue;
                Main.playerInventory=true;
                if(Utils.PressingShift(Main.keyState)&&occupied){
                    if(CompanionNetwork.Client)CompanionNetwork.Transfer(s,false,key,CompanionArmLayout.Group);
                    else CompanionInventoryOperations.Take(s,s.ArmLayout.Items,key).Present(s);
                }else if(HomewardCompanionBackpack.Accepts(s,Main.mouseItem,key)){
                    if(CompanionNetwork.Client)CompanionNetwork.Click(s,s.ArmLayout.Items,key,false);
                    else CompanionInventoryOperations.Click(s.ArmLayout.Items,key,ref Main.mouseItem,false,false,CompanionArmLayout.Group).Present(s);
                }

            }
            borders(sb,center/scale,columns,gridRows);
            var head=CompanionSprite.Head;
            if(head!=null)sb.Draw(head,center/scale,null,Color.White,0,head.Size()/2,Math.Min(1,30f/Math.Max(head.Width,head.Height)),SpriteEffects.None,0);
        }finally{MechArmUI.Opacity=opacity;sb.End();Begin(1);}
    }
    // Journey's DrawPanel also moves the real player's UI. Reuse its texture and
    // nine-slice geometry while keeping the companion window's position separate.
    static void DrawBackground(SpriteBatch sb,Vector2 center,float width,float height){
        var texture=MechArmUI.panelTex;var half=new Vector2(width,height)/2;
        for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
            sb.Draw(texture,center+half*new Vector2(x,y),new Rectangle(10+10*x,10+10*y,10,10),Color.White*.8f,0,new Vector2(5-5*x,5-5*y),new Vector2(x==0?width/10:1,y==0?height/10:1),SpriteEffects.None,0);
        sb.Draw(texture,center+new Vector2(half.X,0),new Rectangle(32,0,24,64),Color.White*.8f,0,new Vector2(0,32),1,SpriteEffects.None,0);
    }
}
