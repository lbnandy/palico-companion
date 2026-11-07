using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.GameInput;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using ReLogic.Graphics;
using Terraria.Localization;
using PalicoCompanion.Systems;
namespace PalicoCompanion;

public sealed class CompanionPanel : ModSystem
{
    bool showStats;
    bool previousMouse, previousRight, dragging, mineVein, inventoryWasOpen;
    readonly Texture2D[] slotPlaceholders=new Texture2D[3];
    readonly Item[] weaponPlaceholderItems=new Item[2];
    Vector2 position = new(900, 250), dragOffset;
    int tab, length = 3, direction = 1, targeting;
    static readonly Color Ink = Color.White, Accent = Color.Gold, Muted = new(190, 200, 230);
    readonly UIPanel nativePanel = new();
    readonly Item[] slotDisplay = InventoryTransfer.Empty(50);
    static readonly Color PanelBackground = CompanionPanelStyle.Background;
    bool HasHeaderRow(CompanionPlayer state)=>ModContent.GetInstance<CompanionConfig>().AdvancedOrders;
    internal static bool ActivityVisible=>ModContent.GetInstance<CompanionConfig>().ShowActivityStatus;
    internal static int ActivityOffset=>ActivityVisible?0:52;
    internal const float DetailScale=.78f;
    internal static int EquipmentSlotLeft {
        get {
            var font=FontAssets.MouseText.Value;
            var labels=new[]{Language.GetTextValue("Mods.PalicoCompanion.UI.Weapon"),CompanionText.Get("CompanionPanel.Work"),CompanionText.Get("CompanionPanel.Support"),Language.GetTextValue("Mods.PalicoCompanion.UI.DyeSlot")};
            if(IndependentLayout)labels=labels.Concat(new[]{Language.GetTextValue("Mods.PalicoCompanion.Equipment.Armor"),Language.GetTextValue("Mods.PalicoCompanion.Equipment.Accessories")}).ToArray();
            int labelSpace=(int)Math.Ceiling(labels.Max(t=>font.MeasureString(t).X)*DetailScale)+12;
            int headerSpace=(int)Math.Ceiling(font.MeasureString(CompanionText.Get("CompanionPanel.Equipment")).X*.85f)+10-(SlotOffset(1)+SlotSize/2-52);
            return 328+Math.Max(44,Math.Max(labelSpace,headerSpace));
        }
    }
    static bool IndependentLayout=>CompanionEquipment.Independent(Main.LocalPlayer.GetModPlayer<CompanionPlayer>());
    internal static int BackpackWidth=>Math.Max(544,EquipmentSlotLeft+SlotOffset(IndependentLayout?CompanionEquipmentSlots.Visible(Main.LocalPlayer.GetModPlayer<CompanionPlayer>())-1:2)+SlotSize+16);
    const int BackpackHeaderSpacing=10;
    // Main.DrawInventory uses this scale and 56px source-grid pitch. Both
    // regions use it, including hit bounds and ItemSlot's native item fitting.
    internal const float SlotScale=.85f;
    internal static int SlotOffset(int index)=>(int)(index*56*SlotScale);
    static int SlotSize=>(int)Math.Ceiling(TextureAssets.InventoryBack.Width()*SlotScale);
    internal Rectangle HitBounds=>Layout(Main.LocalPlayer.GetModPlayer<CompanionPlayer>()).Body;
    internal bool Covers(Rectangle bounds){var layout=Layout(Main.LocalPlayer.GetModPlayer<CompanionPlayer>());return layout.Body.Intersects(bounds)||layout.Details.Intersects(bounds);}
    (Rectangle Body,Rectangle Details,bool Inline) Layout(CompanionPlayer state){
        var buffer=Main.graphics.GraphicsDevice.PresentationParameters;
        int screenW=(int)(buffer.BackBufferWidth/Main.UIScale),screenH=(int)(buffer.BackBufferHeight/Main.UIScale);
        int width=tab==1?510:BackpackWidth,height=PanelHeight(state);
        int x=(int)Math.Clamp(position.X,4,Math.Max(4,screenW-width-4));
        int y=(int)Math.Clamp(position.Y,4,Math.Max(4,screenH-height-4));
        var body=new Rectangle(x,y,width,height);
        var details=showStats?CompanionStatsPanel.Beside(body,CompanionStatsPanel.Width,screenW,screenH):Rectangle.Empty;
        bool inline=showStats&&details.IsEmpty;
        if(inline){body.Width=510;body.Height=Math.Max(height,CompanionStatsPanel.Height);body.Y=Math.Clamp(y,4,Math.Max(4,screenH-body.Height-4));}
        return (body,details,inline);
    }
    internal int PanelHeight(CompanionPlayer state)=>(tab==1?410:536+BackpackHeaderSpacing+(CompanionEquipment.Independent(state)?42:0)-(HasHeaderRow(state)?0:36))-ActivityOffset+16;
    internal static Rectangle HeaderPortraitPixels(Rectangle area,float scale){
        // Scale continuously with the header, retaining its shared centre at
        // 24 and eight UI pixels of space above the portrait.
        var source=CompanionStatusBar.PortraitSource;
        int width=(int)MathF.Round(36*scale),height=(int)MathF.Round(36*scale*source.Height/source.Width);
        return new Rectangle((int)MathF.Round((area.X+16)*scale),
            (int)MathF.Round((area.Y+24)*scale-height/2f),width,height);
    }
    string L(string key,params object[] args) => Language.GetTextValue("Mods.PalicoCompanion.UI." + key,args);
    public override void OnWorldLoad() { targeting = 0; dragging = false; previousMouse = true; showStats=false; }
    public override void UpdateUI(GameTime gameTime) {
        if(!Main.gameMenu&&!Main.dedServ){
            if(inventoryWasOpen&&!Main.playerInventory){Main.LocalPlayer.GetModPlayer<CompanionPlayer>().PanelOpen=false;TrainingPanel.Open=false;targeting=0;dragging=false;}
            inventoryWasOpen=Main.playerInventory;
            if(!Main.LocalPlayer.GetModPlayer<CompanionPlayer>().PanelOpen)showStats=false;
        }
        if(targeting != 0 && !Main.gameMenu) Main.LocalPlayer.mouseInterface = true;
    }
    public override void PostDrawTiles() {
        if(Main.netMode == NetmodeID.Server || Main.gameMenu) return;
        var order = Main.LocalPlayer.GetModPlayer<CompanionPlayer>().Order;
        if(!order.HasPreview && !order.Active && targeting == 0) return;
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        if(targeting != 0) {
            var tile = Main.MouseWorld.ToTileCoordinates();
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(tile.X*16-(int)Main.screenPosition.X,tile.Y*16-(int)Main.screenPosition.Y,16,16), new Color(230,190,100,140));
        }
        for(int i=order.Completed; (order.HasPreview || order.Active) && i<order.Length; i++) {
            var pos = new Vector2((order.Start.X+order.Step.X*i)*16,(order.Start.Y+order.Step.Y*i)*16)-Main.screenPosition;
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle((int)pos.X,(int)pos.Y,16,16),order.HasPreview?new Color(80,200,240,95):new Color(240,180,60,95));
        }
        Main.spriteBatch.End();
    }
    void DrawEmptyIcon(SpriteBatch batch,int icon,Vector2 position){
        if(slotPlaceholders[icon]==null){
            Texture2D source;
            if(icon==2){source=CompanionSprite.Head;if(source==null)return;}
            else {
                int type=icon==0?ItemID.CopperBroadsword:ItemID.BabyBirdStaff;
                weaponPlaceholderItems[icon]=new Item(type);Main.instance.LoadItem(type);source=TextureAssets.Item[type].Value;
            }
            var pixels=new Color[source.Width*source.Height];source.GetData(pixels);
            // Reuse the authored art with vanilla EquipIcons' four grey levels.
            for(int n=0;n<pixels.Length;n++){
                var c=pixels[n];float l=.299f*c.R+.587f*c.G+.114f*c.B;
                int grey=l<80?52:l<125?96:l<185?147:218;pixels[n]=new Color(grey,grey,grey)*(c.A/255f);
            }
            slotPlaceholders[icon]=new Texture2D(source.GraphicsDevice,source.Width,source.Height);slotPlaceholders[icon].SetData(pixels);
        }
        var texture=slotPlaceholders[icon];var frame=texture.Frame();float scale;
        if(icon==2)scale=SlotScale*Math.Min(1f,28f/Math.Max(frame.Width,frame.Height));
        else {var color=Color.White;ItemSlot.DrawItem_GetColorAndScale(weaponPlaceholderItems[icon],SlotScale,ref color,32f,ref frame,out _,out scale);}
        batch.Draw(texture,position+TextureAssets.InventoryBack.Value.Size()*SlotScale/2,frame,Color.White*.35f,0,frame.Size()/2,scale,SpriteEffects.None,0);
    }
    public override void Load() => On_Main.DrawInventory += DrawVanillaInventory;
    public override void Unload() {
        On_Main.DrawInventory -= DrawVanillaInventory;
        foreach(var placeholder in slotPlaceholders)if(placeholder!=null)Main.QueueMainThreadAction(placeholder.Dispose);
        Array.Clear(slotPlaceholders);
        Array.Clear(weaponPlaceholderItems);
        CompanionEnvironmentButton.Unload();CompanionCombatButton.Unload();
    }
    void DrawVanillaInventory(On_Main.orig_DrawInventory orig,Main self) {
        int mx=Main.mouseX,my=Main.mouseY;
        var state=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();
        bool covered=CompanionNetwork.Pending || state.PanelOpen && Main.netMode!=NetmodeID.Server && Covers(new Rectangle(mx,my,1,1));
        // Run after vanilla's per-layer zoom reset; keep slot rendering, suppress covered hit tests.
        try {if(covered){Main.mouseX=-10000;Main.mouseY=-10000;}orig(self);}
        finally {Main.mouseX=mx;Main.mouseY=my;}
    }
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
        int index=layers.FindIndex(l=>l.Name=="Vanilla: Mouse Text");
        if(index>=0) layers.Insert(index,new LegacyGameInterfaceLayer("PalicoCompanion: Companion",Draw,InterfaceScaleType.UI));
    }
    internal bool Draw() {
        CompanionItemTooltip.Clear();
        bool clicked=Main.mouseLeft&&!previousMouse; previousMouse=Main.mouseLeft;
        bool rightClicked=Main.mouseRight&&!previousRight;previousRight=Main.mouseRight;
        if(!CompanionUIFlow.Visible) {dragging=false;return true;}
        var s=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();if(s.PanelOpen)TrainingPanel.Open=false;
        bool advanced=!CompanionNetwork.Client&&ModContent.GetInstance<CompanionConfig>().AdvancedOrders;
        if(tab==1&&!advanced)tab=0;
        var sb=Main.spriteBatch; var mouse=new Point(Main.mouseX,Main.mouseY);
        void Box(Rectangle r,Color c) {
            nativePanel.Left.Set(r.X,0);nativePanel.Top.Set(r.Y,0);nativePanel.Width.Set(r.Width,0);nativePanel.Height.Set(r.Height,0);
            nativePanel.BackgroundColor=c;nativePanel.BorderColor=Color.Black;nativePanel.Recalculate();nativePanel.Draw(sb);
        }
        void Text(string value,int x,int y,Color color,float scale=0.85f,int maxWidth=480) {
            // Ellipsis keeps failure reasons and modded item names inside the panel.
            string text=value??""; var font=FontAssets.MouseText.Value;
            while(text.Length>1 && font.MeasureString(text).X*scale>maxWidth) text=text[..^1];
            if(text!=value && text.Length>3) text=text[..^3]+"...";
            Utils.DrawBorderString(sb,text,new Vector2(x,y),color,scale);
        }
        void Button(string key,string label,int x,int y,int width,Action action,bool selected=false,bool enabled=true) {
            var r=new Rectangle(x,y,width,CompanionButtonLayout.Height);CompanionGamepad.Add("button-"+key,r);bool hover=r.Contains(mouse);
            Box(r,!enabled?new Color(37,43,71):selected?new Color(83,109,190):hover?new Color(73,94,171):new Color(48,63,113));

            CenteredButtonText.Draw(sb,label,r,enabled?(selected?Accent:Ink):Muted,CompanionButtonLayout.TextScale);
            if(hover){ Main.LocalPlayer.mouseInterface=true;if(enabled&&clicked){string before=s.Status;action();if(s.Status!=before)s.Report(s.Status);} }
        }
        // The UI layer already zooms Main.screenWidth/Height. Start from the physical backbuffer.
        var backbuffer=Main.graphics.GraphicsDevice.PresentationParameters;
        int screenW=(int)(backbuffer.BackBufferWidth/Main.UIScale),screenH=(int)(backbuffer.BackBufferHeight/Main.UIScale);
        // Open from the companion itself or the remappable panel key; no permanent HUD button.
        if(targeting!=0) {
            Main.LocalPlayer.mouseInterface=true;
            Box(new Rectangle(164,190,Math.Min(540,screenW-174),54),new Color(63,82,151,240));
            Text(L("TargetHint"),176,195,Accent,0.85f,screenW-190);
            Text(L(targeting==1?"MineTarget":"BuildTarget"),176,218,Ink,0.8f,screenW-190);
            if(Main.mouseRight||Main.keyState.IsKeyDown(Keys.Escape)){targeting=0;s.PanelOpen=true;return true;}
            if(clicked && mouse.Y>250) {
                if(targeting==1){s.Order.Cancel();s.Status=s.Mining.Request(Main.LocalPlayer,Main.MouseWorld,mineVein,ownerRequested:true);}
                else s.Status=s.Order.Preview(Main.LocalPlayer,Main.MouseWorld,direction==0?ItemID.Rope:ItemID.WoodPlatform,direction,direction==0?1:0,length,s.Bag);
                s.Report(s.Status);targeting=0;s.PanelOpen=true;
            }
            return true;
        }
        if(!s.PanelOpen) return true;
        if(!Main.mouseLeft)dragging=false;
        if(dragging)position=new Vector2(mouse.X,mouse.Y)-dragOffset;
        var layout=Layout(s);
        using var navigation=CompanionGamepad.Begin(layout.Inline?"attributes":"bag-"+tab);
        var area=layout.Body;
        int width=area.Width,height=area.Height,x=area.X,y=area.Y;
        if(!layout.Inline)position=area.Location.ToVector2();
        // Magic Storage's draggable panel inherits UIPanel's native background.
        Box(area,PanelBackground);
        if(area.Contains(mouse))Main.LocalPlayer.mouseInterface=true;
        var portrait=HeaderPortraitPixels(area,Main.UIScale);
        CompanionAppearance.DrawPortraitPixels(sb,s,portrait);
        var headerArea=area;
        var guideButton=CompanionPanelHeader.ButtonBounds(headerArea,CompanionButtonLayout.ContentWidth(CompanionText.Get("CompanionPanel.Guide")));
        int statsWidth=CompanionButtonLayout.ContentWidth(Language.GetTextValue("Mods.PalicoCompanion.Attributes.Button"));
        int statsX=guideButton.X-statsWidth-8;
        int nameX=(int)MathF.Ceiling(portrait.Right/Main.UIScale)+8;
        var title=new Rectangle(nameX,y+3,statsX-20-CompanionEnvironmentButton.Size-CompanionCombatButton.Size-nameX,38);
        var namePosition=CompanionPanelHeader.TitlePosition(s.CatName,headerArea);
        Text(s.CatName,nameX,namePosition.Y,Accent,1f,title.Width);
        int nameRight=Math.Min(title.Right,nameX+(int)Math.Ceiling(FontAssets.MouseText.Value.MeasureString(s.CatName).X));
        int identityRight=nameRight+4;
        var environmentButton=new Rectangle(nameRight+8,y+8,CompanionEnvironmentButton.Size,CompanionEnvironmentButton.Size);
        var combatButton=new Rectangle(environmentButton.Right+4,y+8,CompanionCombatButton.Size,CompanionCombatButton.Size);
        var identity=new Rectangle(x+12,y,identityRight-x-12,48);
        CompanionGamepad.Add("identity",identity);
        if(identity.Contains(mouse)) {
            Main.instance.MouseText(CompanionAppearance.L("AppearanceEntry"));
            if(clicked&&Main.mouseItem.IsAir){dragging=false;CompanionAppearance.Open(s);return true;}
        }
        else if(!environmentButton.Contains(mouse)&&!combatButton.Contains(mouse)&&title.Contains(mouse)&&Main.mouseLeft&&!dragging&&Main.mouseItem.IsAir){dragging=true;dragOffset=new Vector2(mouse.X,mouse.Y)-position;}
        CompanionEnvironmentButton.Draw(sb,s,environmentButton,mouse,clicked);
        CompanionCombatButton.Draw(sb,s,combatButton,mouse,clicked);
        Button("Attributes",Language.GetTextValue("Mods.PalicoCompanion.Attributes.Button"),statsX,guideButton.Y,statsWidth,()=>{showStats=!showStats;Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);},showStats);
        Button("Guide",CompanionText.Get("CompanionPanel.Guide"),guideButton.X,guideButton.Y,guideButton.Width,TrainingPanel.OpenGuide);
        if(CenteredButtonText.Close(sb,CompanionPanelHeader.CloseBounds(headerArea),mouse,clicked)){s.PanelOpen=false;showStats=false;return true;}
        if(layout.Inline){CompanionStatsPanel.Draw(sb,s,new Rectangle(x+16,y+56,width-32,height-72),mouse);return true;}
        if(!layout.Details.IsEmpty){
            var details=layout.Details;
            Box(details,PanelBackground);
            if(details.Contains(mouse))Main.LocalPlayer.mouseInterface=true;
            Text(Language.GetTextValue("Mods.PalicoCompanion.Attributes.Button"),details.X+16,details.Y+12,Accent,.85f,details.Width-64);
            var close=CompanionPanelHeader.CloseBounds(details);CompanionGamepad.Add("stats-close",close);
            if(CenteredButtonText.Close(sb,close,mouse,clicked))showStats=false;
            CompanionStatsPanel.Draw(sb,s,new Rectangle(details.X+16,details.Y+48,details.Width-32,details.Height-64),mouse);
        }
        y+=16;
        if(!HasHeaderRow(s))y-=36;
        if(advanced) {
            int companionTabWidth=CompanionButtonLayout.ContentWidth(L("CompanionTab"));
            Button("CompanionTab",L("CompanionTab"),x+16,y+48,companionTabWidth,()=>tab=0,tab==0);
            Button("WorkTab",L("WorkTab"),x+16+companionTabWidth+CompanionButtonLayout.Gap,y+48,CompanionButtonLayout.ContentWidth(L("WorkTab")),()=>tab=1,tab==1);
        }

        if(ActivityVisible&&tab==1) {
            Box(new Rectangle(x+12,y+87,width-24,43),new Color(33,43,79));
            string status=s.NoticeTicks>0?s.Notice:s.Status;
            status=CompanionCopy.Status(status);
            Text(status,x+20,y+94,Ink,0.8f,width-36);
            if(new Rectangle(x+12,y+87,width-24,43).Contains(mouse))Main.instance.MouseText(status);
        }
        y-=tab==0?52:ActivityOffset;
        if(tab==1) {
            Button("Stow",L(s.Stowed?"CallOut":"Stow"),x+12,y+139,132,()=>CompanionLife.Toggle(s));
            Button("RecallAction",L("RecallAction"),x+156,y+139,132,s.Recall,false,!s.Vitals.Downed&&!s.Stowed);
            Button("Pet",L("Pet"),x+300,y+139,132,s.Pet,false,s.PetCooldown==0&&!s.Stowed&&!s.Vitals.Downed);
        }

        bool shift=Main.keyState.IsKeyDown(Keys.LeftShift)||Main.keyState.IsKeyDown(Keys.RightShift);
        void Slot(Item[] items,int i,int bx,int by,bool equipment=false,bool dye=false,bool training=false) {
            using var navigation=CompanionGamepad.Slot(s,items,i,new Rectangle(bx,by,SlotSize,SlotSize));
            byte group=CompanionInventoryOperations.Group(s,items);
            bool canFavorite=CompanionInventoryOperations.CanFavorite(group,i);
            float oldScale=Main.inventoryScale;
            try {
                Main.inventoryScale=SlotScale;
                if(new Rectangle(bx,by,SlotSize,SlotSize).Contains(mouse)) {
                    Main.LocalPlayer.mouseInterface=true;
                    // Vanilla returns mouseItem to the player each tick when inventory is closed.
                    // Open it before moving a slot item so the cursor can carry it between panels.
                    if((clicked||Main.mouseRight)&&!Main.playerInventory)Main.playerInventory=true;
                    ItemSlot.MouseHover(items,ItemSlot.Context.BankItem,i);
                    if(!items[i].IsAir)CompanionItemTooltip.Hover(s,CompanionInventoryOperations.SharedSlot(group,i),ReferenceEquals(items,s.Gear)&&i is >=1 and <=3,group==0);
                    bool favorite=Main.keyState.IsKeyDown(Main.FavoriteKey);
                    bool favoriteOnly=!canFavorite&&favorite&&!shift&&!Main.mouseRight&&Main.mouseItem.IsAir&&!items[i].IsAir&&!Main.drawingPlayerChat;
                    if(ReferenceEquals(items,s.Gear)&&CompanionEquipment.IsSlot(i)&&!CompanionEquipmentSlots.Unlocked(s,i)&&items[i].IsAir)Main.instance.MouseText(Language.GetTextValue("Mods.PalicoCompanion.Attributes.LockedSlot"));
                    if(favoriteOnly){if(Main.cursorOverride==3)Main.cursorOverride=0;}
                    else if(CompanionNetwork.Client) {if(clicked&&CompanionNetwork.QuickTrashHeld)CompanionNetwork.Trash(s,items,i);else if(clicked&&shift)CompanionNetwork.Transfer(s,false,i,group);else if(clicked||rightClicked)CompanionNetwork.Click(s,items,i,rightClicked,favorite);}
                    else if(shift) {
                        if(clicked&&!items[i].favorited) {
                            CompanionInventoryOperations.Take(s,items,i).Present(s,true);
                        }
                    } else if(CompanionInventoryOperations.Accepts(s,Main.mouseItem,CompanionInventoryOperations.Group(s,items),i)) {
                        PickupCredit.Clear(Main.mouseItem);
                        // Share the server's cursor transaction: native BankItem
                        // clears favorites when placing an item into its slot.
                        if(Main.drawingPlayerChat||CompanionNetwork.QuickTrashHeld)ItemSlot.LeftClick(items,ItemSlot.Context.BankItem,i);
                        else if(clicked&&CompanionNetwork.CanMoveInventory(s.Player))CompanionInventoryOperations.Click(items,i,ref Main.mouseItem,false,favorite,group).Present(s);
                        if(CompanionInventoryOperations.CanTogglePickupStone(items[i],group)){
                            if(rightClicked&&CompanionNetwork.CanMoveInventory(s.Player))CompanionInventoryOperations.Click(items,i,ref Main.mouseItem,true,false,group).Present(s);
                        }else if(CompanionInventoryOperations.CanSplit(items[i],group,i))ItemSlot.RightClick(items,ItemSlot.Context.BankItem,i);
                    }
                }
                // Native backpack slots (indices >=10 avoid hotbar labels); native
                // functional equipment tint. Slot 3 avoids a helmet placeholder.
                // These contexts only render slots; cursor rules are shared above.
                bool armor=ReferenceEquals(items,s.Gear)&&CompanionEquipment.IsSlot(i);
                int drawSlot=armor?CompanionEquipment.ArmorIndex(i):dye?0:equipment||training?3:i+10;
                slotDisplay[drawSlot]=items[i];
                // Favorite belongs to the item, but equipment keeps its slot
                // colour. Preserve the actual flag for returning it to a bag.
                if(group!=0&&items[i].favorited){slotDisplay[drawSlot]=items[i].Clone();slotDisplay[drawSlot].favorited=false;}
                bool locked=armor&&!CompanionEquipmentSlots.Unlocked(s,i);
                int equipmentContext=dye?ItemSlot.Context.EquipDye:training||armor&&i>=CompanionEquipment.AccessoryStart?ItemSlot.Context.EquipAccessory:ItemSlot.Context.EquipArmor;
                // Both native contexts share the functional slot background;
                // armor slot 3 has no built-in accessory placeholder.
                if(training&&items[i].IsAir)equipmentContext=ItemSlot.Context.EquipArmor;
                if(equipment||training)CompanionLoadoutTint.Draw(sb,s,slotDisplay,equipmentContext,drawSlot,new Vector2(bx,by),tint:locked?new Color(65,65,75):null,shared:CompanionInventoryOperations.SharedSlot(group,i)&&items[i].favorited);
                else ItemSlot.Draw(sb,slotDisplay,dye?ItemSlot.Context.EquipDye:ItemSlot.Context.InventoryItem,drawSlot,new Vector2(bx,by),Color.White);
                if(training&&items[i].IsAir)DrawEmptyIcon(sb,2,new Vector2(bx,by));
                else if(equipment&&!armor&&!dye&&!training&&(i==0||i==CompanionWeapons.SummonSlot)&&items[i].IsAir)
                    DrawEmptyIcon(sb,i==0?0:1,new Vector2(bx,by));
            } finally {Main.inventoryScale=oldScale;}
        }
        if(tab==0) {
            int top=y+140+BackpackHeaderSpacing;
            bool independent=CompanionEquipment.Independent(s);
            int GearRow(int row)=>row==0?0:row+(independent?2:0);
            const float detailScale=DetailScale;
            // Equipment and stat labels share a fixed scale and left edge.
            // Only vertical placement is centered; a narrow label box must
            // not shrink the font or shift short labels horizontally.
            void DetailLabel(string label,int left,int ry,int height) {
                if(Terraria.Localization.Language.ActiveCulture.Name!="zh-Hans"){
                    NativeLabelText.Draw(sb,label,new Rectangle(x+left,ry,300,height),Muted,detailScale);return;
                }
                float labelY=CenteredButtonText.Layout(label,new Rectangle(0,ry,300,height),detailScale).Position.Y;
                Utils.DrawBorderString(sb,label,new Vector2(x+left,labelY),Muted,detailScale);
            }
            void Stat(string label,string value,int left,int right,int ry,Color color,string hint) {
                var row=new Rectangle(0,ry,300,26);
                DetailLabel(label,left,ry,26);
                float w=FontAssets.MouseText.Value.MeasureString(value).X*detailScale;
                float valueY=CenteredButtonText.Layout(value,row,detailScale).Position.Y;
                if(Terraria.Localization.Language.ActiveCulture.Name!="zh-Hans")NativeLabelText.Draw(sb,value,new Rectangle(x+left,ry,right-left,26),color,detailScale,1);
                else Utils.DrawBorderString(sb,value,new Vector2(x+right-w,valueY),color,detailScale);
                if(new Rectangle(x+left,ry,right-left,26).Contains(mouse))Main.instance.MouseText(hint);
            }
            // Local inventory actions stay directly beneath their 6 x 4 inventory.
            int sectionHeader=top-6;
            int headerCenter=sectionHeader+10;
            void HeaderText(string text,int left,Color color,float scale,int maxWidth){
                var layout=CenteredButtonText.Layout(text,new Rectangle(0,headerCenter-12,maxWidth,24),scale);
                Utils.DrawBorderString(sb,text,new Vector2(left,layout.Position.Y),color,layout.Scale);
            }
            HeaderText(L("BagTab"),x+16,Ink,.85f,150);
            string capacity=$"{s.Bag.Count(i=>!i.IsAir)} / {s.Bag.Length}";
            HeaderText(capacity,x+299-(int)(FontAssets.MouseText.Value.MeasureString(capacity).X*.75f),Muted,.75f,96);
            for(int i=0;i<Math.Min(24,s.Bag.Length);i++)Slot(s.Bag,i,x+16+SlotOffset(i%6),top+26+SlotOffset(i/6));
            Button("QuickStack",L("QuickStack"),x+16,top+228,89,s.QuickStack);
            Button("Take",L("Take"),x+113,top+228,89,s.TakeAll);
            Button("SortBag",L("SortBag"),x+210,top+228,89,()=>CompanionSorting.Request(s));
            if(new Rectangle(x+16,sectionHeader,283,24).Contains(mouse))Main.instance.MouseText(L("NativeBagHint"));

            // Four equipment rows align with the four backpack rows.
            HeaderText(CompanionText.Get("CompanionPanel.Equipment"),x+328,Ink,.85f,190);
            int equipmentLeft=x+EquipmentSlotLeft;
            CompanionLoadoutButtons.Draw(sb,s,equipmentLeft+SlotOffset(1)+SlotSize/2-52,headerCenter-CompanionLoadoutButtons.VisibleCenterOffsetY,clicked);
            void EquipmentLabel(string label,int row)=>DetailLabel(label,328,top+26+SlotOffset(GearRow(row)),SlotSize);
            EquipmentLabel(L("Weapon"),0);
            EquipmentLabel(CompanionText.Get("CompanionPanel.Work"),1);
            EquipmentLabel(CompanionText.Get("CompanionPanel.Support"),2);
            EquipmentLabel(L("DyeSlot"),3);
            Slot(s.Gear,0,equipmentLeft,top+26,true);
            Slot(s.Gear,CompanionWeapons.SummonSlot,equipmentLeft+SlotOffset(1),top+26,true);
            if(new Rectangle(equipmentLeft+SlotOffset(1),top+26,SlotSize,SlotSize).Contains(mouse)&&s.Gear[CompanionWeapons.SummonSlot].IsAir)Main.instance.MouseText(L("SummonWeapon")+"\n"+L("SummonWeaponHint"));
            if(new Rectangle(equipmentLeft,top+26,SlotSize,SlotSize).Contains(mouse)){
                if(s.Gear[0].IsAir)Main.instance.MouseText(L("DefaultWeaponHint")+"\n"+L("GearHint"));
            }
            Slot(s.Training.Slots,3,equipmentLeft,top+26+SlotOffset(GearRow(1)),training:true);
            if(s.Training.Slots[3].IsAir&&new Rectangle(equipmentLeft,top+26+SlotOffset(GearRow(1)),SlotSize,SlotSize).Contains(mouse))Main.instance.MouseText(CompanionText.Get("CompanionPanel.WorkHint"));
            for(int i=0;i<s.Training.Count(s.Player);i++) {
                Slot(s.Training.Slots,i,equipmentLeft+SlotOffset(i),top+26+SlotOffset(GearRow(2)),training:true);
                if(s.Training.Slots[i].IsAir&&new Rectangle(equipmentLeft+SlotOffset(i),top+26+SlotOffset(GearRow(2)),SlotSize,SlotSize).Contains(mouse))Main.instance.MouseText(CompanionText.Get("CompanionPanel.SupportHint"));
            }
            Slot(s.Dyes,0,equipmentLeft,top+26+SlotOffset(GearRow(3)),true,true);
            if(s.Dyes[0].IsAir&&new Rectangle(equipmentLeft,top+26+SlotOffset(GearRow(3)),SlotSize,SlotSize).Contains(mouse))Main.instance.MouseText(L("DyeHint"));

            // Independent equipment uses the right column; move stats beneath the bag.
            int statLeft=independent?16:328,statRight=independent?299:width-32,statOffset=independent?42:0;
            Stat(L("HealthLabel"),$"{s.Vitals.Health} / {s.Vitals.MaxHealth}",statLeft,statRight,top+statOffset+230,s.Vitals.Health<=s.Vitals.MaxHealth/4?Color.LightCoral:Ink,independent?Language.GetTextValue("Mods.PalicoCompanion.Equipment.HealthHint"):L("HealthHint",CompanionBalanceConfig.Current.HealthRatio));
            Stat(L("ManaLabel"),$"{s.Vitals.Mana} / {s.Vitals.MaxMana}",statLeft,statRight,top+statOffset+256,s.Vitals.Mana<=s.Vitals.MaxMana/4?Color.LightSkyBlue:Ink,independent?Language.GetTextValue("Mods.PalicoCompanion.Equipment.ManaHint"):L("ManaHint"));
            Stat(L("Defense"),$"{s.Vitals.EffectiveDefense}",statLeft,statRight,top+statOffset+282,Ink,independent?Language.GetTextValue("Mods.PalicoCompanion.Equipment.DefenseHint"):L("DefenseHint",CompanionBalanceConfig.Current.DefenseRatio));
            bool sentry=CompanionWeaponPolicy.Sentry(CompanionWeapons.Summon(s.Gear));
            var use=(s.Cat?.ModProjectile as CompanionCat)?.Weapons;
            float slots=sentry?use?.SentryCount??0:use?.SummonSlots??0;
            Stat(L(sentry?"SentrySlots":"MinionSlots"),$"{slots:0.#} / {(sentry?CompanionNativeUse.SentryLimit(s.Player):CompanionNativeUse.MinionLimit(s.Player))}",statLeft,statRight,top+statOffset+308,Ink,independent?Language.GetTextValue("Mods.PalicoCompanion.Equipment.SummonHint"):L(sentry?"SentrySlotsHint":"MinionSlotsHint"));
            CompanionFoodDisplay.Draw(sb,s,new Rectangle(independent?equipmentLeft:x+16,top+(independent?332:278),independent?width-EquipmentSlotLeft-16:283,32));

            if(independent){
                void ExtraLabel(string key,int row)=>DetailLabel(Language.GetTextValue("Mods.PalicoCompanion.Equipment."+key),328,top+26+SlotOffset(row),SlotSize);
                ExtraLabel("Armor",1);ExtraLabel("Accessories",2);
                for(int i=1;i<=3;i++)Slot(s.Gear,i,equipmentLeft+SlotOffset(i-1),top+26+SlotOffset(1),true);
                for(int i=5;i<5+CompanionEquipmentSlots.Visible(s);i++)Slot(s.Gear,i,equipmentLeft+SlotOffset(i-5),top+26+SlotOffset(2),true);
            }
            int footer=top+354+(independent?42:0);
            if(ActivityVisible) {
                string status=s.NoticeTicks>0?s.Notice:s.Status;
                status=CompanionCopy.Status(status);
                Box(new Rectangle(x+16,footer,width-32,43),new Color(33,43,79));
                Text(status,x+26,footer+11,Ink,.8f,width-52);
                if(new Rectangle(x+16,footer,width-32,43).Contains(mouse))Main.instance.MouseText(status);
                footer+=52;
            }
            int footerWidth=(width-52)/3;
            Button("Pet",L("Pet"),x+16,footer,footerWidth,s.Pet,false,s.PetCooldown==0&&!s.Stowed&&!s.Vitals.Downed);
            Button("RecallAction",L("RecallAction"),x+26+footerWidth,footer,footerWidth,s.Recall,false,!s.Vitals.Downed&&!s.Stowed);
            Button("Stow",L(s.Stowed?"CallOut":"Stow"),x+36+footerWidth*2,footer,footerWidth,()=>CompanionLife.Toggle(s));
        } else {
            Button("MineTarget",L("MineTarget"),x+12,y+183,154,()=>{targeting=1;s.PanelOpen=false;});
            Button("Left",L("Left"),x+12,y+223,100,()=>direction=-1,direction==-1);
            Button("Right",L("Right"),x+120,y+223,100,()=>direction=1,direction==1);
            Button("Down",L("Down"),x+228,y+223,100,()=>direction=0,direction==0);
            Button("Shorter","-",x+12,y+263,36,()=>length=Math.Max(1,length-1));
            Text(L("Length")+" "+length,x+58,y+269,Ink,0.85f,125);
            Button("Longer","+",x+185,y+263,36,()=>length=Math.Min(24,length+1));
            Button("Preview",L("Preview"),x+230,y+263,132,()=>{targeting=2;s.PanelOpen=false;});
            Button("Confirm",L("Confirm"),x+370,y+263,132,()=>{s.Mining.Cancel();s.Status=s.Order.Confirm(Main.LocalPlayer,s.Bag);},false,s.Order.HasPreview&&!s.Paused&&s.Enabled);
            Button("Vein",L(mineVein?"VeinOn":"VeinOff"),x+12,y+303,154,()=>mineVein=!mineVein,mineVein);
            string progress=s.Order.Active||s.Order.HasPreview?$"{s.Order.Completed} / {s.Order.Length}":L("NoOrder");
            Text(L("Progress")+"  "+progress,x+178,y+309,Accent,0.8f,310);
            string budget=L("WorkHint");
            if(s.Order.HasPreview||s.Order.Active) {
                int required=s.Order.Length-s.Order.Completed;
                int available=s.Bag.Where(i=>!i.IsAir&&i.type==s.Order.ItemType).Sum(i=>i.stack);
                budget=$"{Lang.GetItemNameValue(s.Order.ItemType)}: {L("Required")} {required} / {L("Available")} {available}";
            }
            Text(budget,x+14,y+343,Muted,0.8f,480);
        }
        return true;
    }
}
