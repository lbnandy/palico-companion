using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Localization;
using Terraria.UI;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
namespace PalicoCompanion.Systems;

// Read-only reference: equipment and item use stay in the normal backpack flow.
public sealed class TrainingPanel:ModSystem {
    internal static bool Open;internal static int Category,Entry;
    int recipeVariant,scrollLine,lastEntry=-1,lastCategory=-1;bool previous,dragging,compareTiers;Vector2 dragOffset;static Vector2? position;readonly UIPanel panel=new();
    internal bool CompareTiers=>compareTiers;
    readonly GuideBackdrop backdrop=new();
    internal void DrawBackdrop(SpriteBatch batch,Rectangle bounds)=>backdrop.Draw(batch,bounds);
    static readonly string[] CategoryKeys=["TrainingPanel.Basics", "TrainingPanel.Support", "TrainingPanel.Work", "TrainingPanel.Upgrades"];
    static string CategoryLabel(int index)=>CompanionText.Get(CategoryKeys[index]);
    readonly UITextPanel<string>[] categoryTabs=new UITextPanel<string>[4];
    internal static Rectangle CategoryBounds(int index) {
        int left=Bounds.X+22;
        for(int i=0;i<index;i++)left+=CompanionButtonLayout.ContentWidth(CategoryLabel(i))+CompanionButtonLayout.Gap;
        return new Rectangle(left,Bounds.Y+54,CompanionButtonLayout.ContentWidth(CategoryLabel(index)),32);
    }
    UIScrollbar bodyScroll;bool scrollDragging;float scrollOffset;
    internal int ScrollLine {get=>scrollLine;set=>scrollLine=value;}
    internal int RowCount {get;private set;}
    static readonly string[] TopicKeys=["TrainingPanel.Progress", "TrainingPanel.Meet", "TrainingPanel.Combat", "TrainingPanel.Mining", "TrainingPanel.Exploring", "TrainingPanel.Fishing", "TrainingPanel.Storage", "TrainingPanel.Equipment", "TrainingPanel.Multiplayer"];
    static string Topic(int index)=>CompanionText.Get(TopicKeys[index]);
    internal static void OpenGuide(){Open=true;Main.LocalPlayer.GetModPlayer<CompanionPlayer>().PanelOpen=false;Main.playerInventory=true;}
    // Main.screenWidth is already zoomed inside a UI layer; use the backbuffer
    // so both hit-testing and rendering divide by UI scale exactly once.
    static Vector2 ViewSize=>new(Main.graphics.GraphicsDevice.PresentationParameters.BackBufferWidth/Main.UIScale,Main.graphics.GraphicsDevice.PresentationParameters.BackBufferHeight/Main.UIScale);
    internal static float Fit=>Math.Min(1,Math.Min((ViewSize.X-24)/736f,(ViewSize.Y-24)/510f));
    internal static Rectangle Bounds {get{
        float fit=Fit;var size=new Vector2(736,510)*fit;
        var at=position??new Vector2(ViewSize.X-size.X-14,ViewSize.Y-size.Y-18);
        at.X=Math.Clamp(at.X,4,Math.Max(4,ViewSize.X-size.X-4));at.Y=Math.Clamp(at.Y,4,Math.Max(4,ViewSize.Y-size.Y-4));
        if(position.HasValue)position=at;
        return new((int)(at.X/fit),(int)(at.Y/fit),736,510);
    }}
    internal static Rectangle HitBounds {get{var b=Bounds;return new((int)(b.X*Fit),(int)(b.Y*Fit),(int)(b.Width*Fit),(int)(b.Height*Fit));}}
    public override void Load()=>On_Main.DrawInventory+=Inventory;
    public override void PostSetupContent(){if(!Main.dedServ)Main.QueueMainThreadAction(backdrop.Create);}
    public override void Unload(){On_Main.DrawInventory-=Inventory;Open=false;if(!Main.dedServ)Main.QueueMainThreadAction(backdrop.Dispose);}
    public override void OnWorldLoad(){Open=false;dragging=false;}
    void Inventory(On_Main.orig_DrawInventory orig,Main self){int x=Main.mouseX,y=Main.mouseY;try{if(Open&&HitBounds.Contains(x,y)){Main.mouseX=Main.mouseY=-10000;}orig(self);}finally{Main.mouseX=x;Main.mouseY=y;}}
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers){int i=layers.FindIndex(l=>l.Name=="Vanilla: Mouse Text");if(i>=0)layers.Insert(i,new LegacyGameInterfaceLayer("PalicoCompanion: Guide",Draw,InterfaceScaleType.UI));}
    internal bool Draw(){bool click=Main.mouseLeft&&!previous;previous=Main.mouseLeft;if(!Main.playerInventory)Open=false;if(!Open||!CompanionUIFlow.Visible){dragging=false;return true;}
        var s=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();
        var pointer=new Vector2(Main.mouseX,Main.mouseY);
        if(!Main.mouseLeft)dragging=false;
        if(dragging)position=pointer-dragOffset;
        var b=Bounds;int x=b.X,y=b.Y;var mouse=new Point((int)(Main.mouseX/Fit),(int)(Main.mouseY/Fit));var sb=Main.spriteBatch;
        var title=new Rectangle(x+8,y+4,440,42);
        if(click&&title.Contains(mouse)&&Main.mouseItem.IsAir){dragging=true;dragOffset=pointer-new Vector2(x,y)*Fit;}
        if(dragging)s.Player.mouseInterface=true;
        if(title.Contains(mouse))Main.instance.MouseText(CompanionText.Get("TrainingPanel.DragHint"));
        bool scaled=Fit<1;if(scaled){sb.End();sb.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,null,null,null,Matrix.CreateScale(Fit)*Main.UIScaleMatrix);}
        if(b.Contains(mouse)){s.Player.mouseInterface=true;s.Player.cursorItemIconEnabled=false;}
        void Box(Rectangle r,Color color){panel.Left.Set(r.X,0);panel.Top.Set(r.Y,0);panel.Width.Set(r.Width,0);panel.Height.Set(r.Height,0);panel.BackgroundColor=color;panel.BorderColor=Color.Black;panel.Recalculate();panel.Draw(sb);}
        void Text(string text,int tx,int ty,Color? color=null,float scale=.8f)=>Utils.DrawBorderString(sb,text,new Vector2(tx,ty),color??Color.White,scale);
        void Button(string text,int tx,int ty,int width,Action action,bool on=false){var r=new Rectangle(tx,ty,width,CompanionButtonLayout.Height);Box(r,on?new Color(82,105,177):r.Contains(mouse)?new Color(68,87,151):new Color(43,57,102));CenteredButtonText.Draw(sb,text,r,Color.White,CompanionButtonLayout.TextScale);if(click&&r.Contains(mouse))action();}
        DrawBackdrop(sb,b);
        var titlePosition=CompanionPanelHeader.TitlePosition(CompanionText.Get("TrainingPanel.Title"),b);
        Text(CompanionText.Get("TrainingPanel.Title"),titlePosition.X,titlePosition.Y,Color.Gold,1);
        var returnButton=CompanionPanelHeader.ButtonBounds(b,CompanionButtonLayout.ContentWidth(CompanionText.Get("TrainingPanel.Back")));
        var settingsButton=CompanionPanelHeader.ButtonBounds(b,CompanionButtonLayout.ContentWidth(CompanionText.Get("TrainingPanel.Settings")),returnButton.Width+CompanionButtonLayout.Gap);
        Button(CompanionText.Get("TrainingPanel.Settings"),settingsButton.X,settingsButton.Y,settingsButton.Width,()=>{Open=false;ModContent.GetInstance<CompanionConfig>().Open(()=>OpenGuide());});
        Button(CompanionText.Get("TrainingPanel.Back"),returnButton.X,returnButton.Y,returnButton.Width,()=>{Open=false;s.PanelOpen=true;});
        if(CenteredButtonText.Close(sb,CompanionPanelHeader.CloseBounds(b),mouse,click)){Open=false;dragging=false;}
        // Magic Storage's UIPanelTab is a thin UITextPanel wrapper: use the
        // actual native control, compact label-sized widths, and yellow selection.
        for(int i=0;i<CategoryKeys.Length;i++) {
            var tab=categoryTabs[i]??=new UITextPanel<string>(CategoryLabel(i),.8f);
            tab.SetText(CategoryLabel(i),.8f,false);
            var r=CategoryBounds(i);bool hover=r.Contains(mouse);
            tab.SetPadding(0);tab.TextHAlign=.5f;
            tab.PaddingLeft=tab.PaddingRight=12;
            tab.PaddingTop=tab.PaddingBottom=(r.Height-16*.8f)/2;
            float textOffset=CenteredButtonText.NativeVerticalOffset(CategoryLabel(i),.8f);
            tab.PaddingTop+=textOffset;tab.PaddingBottom-=textOffset;
            tab.Left.Set(r.X,0);tab.Top.Set(r.Y,0);tab.Width.Set(r.Width,0);tab.Height.Set(r.Height,0);
            var background=tab.BackgroundColor;background.A=255;tab.BackgroundColor=background;
            tab.TextColor=Category==i?Color.Yellow:hover?Main.OurFavoriteColor:Color.White;
            tab.Recalculate();tab.Draw(sb);
            if(click&&hover&&Category!=i){Category=i;Entry=new[]{0,0,8,13}[i];recipeVariant=0;scrollDragging=false;Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);}
        }
        int start=new[]{0,0,8,13}[Category],end=new[]{9,8,11,17}[Category];Entry=Math.Clamp(Entry,start,end-1);
        int Actual(int index)=>Category==3&&index==16?19:index;
        for(int i=start;i<end;i++) {
            var r=new Rectangle(x+28,y+103+(i-start)*40,156,32);bool selected=Entry==i,hover=r.Contains(mouse);
            if(selected||hover)Box(r,selected?new Color(70,90,150):new Color(49,65,111));
            CenteredButtonText.Draw(sb,Category==0?Topic(i):TrainingCatalog.Entries[Actual(i)].Name,r,selected?Color.White:hover?Main.OurFavoriteColor:new Color(195,207,231),.8f);
            if(click&&hover&&Entry!=i){Entry=i;recipeVariant=0;scrollDragging=false;Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);}
        }
        if(lastEntry!=Entry||lastCategory!=Category){scrollLine=0;compareTiers=false;lastEntry=Entry;lastCategory=Category;}
        var rows=new List<(string text,Color color,Action action)>();
        // The heading shares the first directory row's center (119); body text
        // starts at the second row's center (159). Keep wrapped lines on a
        // common font baseline rather than centering each sentence separately.
        const int lineHeight=23;
        bool legend=Category==0&&Entry==2;
        int bodyTop=143+(legend?78:0);
        int bodyTextY=(int)Math.Round(CenteredButtonText.Layout(CompanionText.Get("TrainingPanel.Baseline"),new Rectangle(x+204,y+bodyTop,504,32),.78f).Position.Y);
        bool body=new Rectangle(x+204,y+bodyTop,504,327-(legend?78:0)).Contains(mouse);
        if(body&&Terraria.GameInput.PlayerInput.ScrollWheelDeltaForUI!=0){scrollLine=Math.Max(0,scrollLine-Terraria.GameInput.PlayerInput.ScrollWheelDeltaForUI/120*3);Terraria.GameInput.PlayerInput.ScrollWheelDeltaForUI=0;}
        void Wrapped(string str,Color? color=null){foreach(var paragraph in str.Split('\n')){
            var ink=color??((paragraph.StartsWith("【")||paragraph.StartsWith("["))?Color.LightSkyBlue:Color.White);
            if(paragraph.Length==0){rows.Add(("",ink,null));continue;}
            foreach(var row in CompanionCopy.Wrap(paragraph,(int)(476/.78f)))rows.Add((row,ink,null));
        }}
        int titleX=x+204;
        if(Category!=0){var item=new Item(TrainingCatalog.Type(Actual(Entry)));Main.instance.LoadItem(item.type);var texture=TextureAssets.Item[item.type].Value;var frame=Main.itemAnimations[item.type]?.GetFrame(texture)??texture.Frame();float scale=Math.Min(1f,30f/Math.Max(frame.Width,frame.Height));sb.Draw(texture,new Vector2(x+219,y+119),frame,Color.White,0,frame.Size()/2,scale,SpriteEffects.None,0);titleX+=40;}
        string heading=Category==0?Topic(Entry):TrainingCatalog.Entries[Actual(Entry)].Name;
        var headingLayout=CenteredButtonText.Layout(heading,new Rectangle(titleX,y+103,480,32),1);
        Utils.DrawBorderString(sb,heading,new Vector2(titleX,headingLayout.Position.Y),Color.Gold,headingLayout.Scale);
        if(legend)TrainingHud.DrawLegend(sb,s,new Vector2(x+204,y+143));
        if(Category==0&&Entry==0){
            Wrapped(CompanionText.Get("TrainingPanel.ProgressSummary" ,TrainingCatalog.TierName(s.Training.Tier), s.Training.Count(s.Player)),Color.LightGreen);
            Wrapped(s.Player.extraAccessory?(Main.expertMode?CompanionText.Get("TrainingPanel.DemonHeartActive"):CompanionText.Get("TrainingPanel.DemonHeartExpert")):(Main.expertMode?CompanionText.Get("TrainingPanel.DemonHeartAvailable"):CompanionText.Get("TrainingPanel.DemonHeartClassic")));
            int row=bodyTextY+rows.Count*lineHeight+18;
            Text(CompanionText.Get("TrainingPanel.NextSteps"),x+204,row,Color.LightSkyBlue,.8f);row+=29;
            var suggestions=TrainingProgress.Suggestions(s).Take(3).ToArray();
            foreach(int id in suggestions){
                var item=new Item(TrainingCatalog.Type(id));Main.instance.LoadItem(item.type);var texture=TextureAssets.Item[item.type].Value;var frame=Main.itemAnimations[item.type]?.GetFrame(texture)??texture.Frame();
                sb.Draw(texture,new Vector2(x+220,row+19),frame,Color.White,0,frame.Size()/2,Math.Min(1,28f/Math.Max(frame.Width,frame.Height)),SpriteEffects.None,0);
                Text(item.Name,x+246,row+1,Color.White,.8f);
                Text(TrainingProgress.Status(s,id),x+246,row+25,Color.LightGray,.65f);
                int selected=id,detailsWidth=CompanionButtonLayout.ContentWidth(CompanionText.Get("TrainingPanel.Details"));Button(CompanionText.Get("TrainingPanel.Details"),x+694-detailsWidth,row+7,detailsWidth,()=>{Category=selected<8?1:selected<11?2:3;Entry=selected==19?16:selected;recipeVariant=0;});
                row+=56;
            }
            if(suggestions.Length==0)Text(CompanionText.Get("TrainingPanel.AllReady"),x+204,row,Color.LightGray,.75f);
        }
        else if(Category==0)Wrapped(TrainingGuideCopy.Page(Entry));
        else {
            int id=Actual(Entry);
            if(id<11){
                string label=compareTiers?CompanionText.Get("TrainingPanel.CurrentTier"):CompanionText.Get("TrainingPanel.CompareTiers");int width=CompanionButtonLayout.ContentWidth(label);
                Button(label,x+694-width,y+103,width,()=>{compareTiers=!compareTiers;scrollLine=0;});
                Wrapped(compareTiers?CompanionText.Get("TrainingPanel.TierLegend"):CompanionText.Get("TrainingPanel.CurrentEffect" ,TrainingCatalog.TierName(s.Training.Tier)),Color.LightGray);
            }
            Wrapped(TrainingGuideCopy.Detail(id,s.Training.Tier,compareTiers).Replace("。","。\n").TrimEnd());Wrapped("");
            var recipes=TrainingProgress.Recipes(id);
            if(recipes.Length>0){recipeVariant=Math.Clamp(recipeVariant,0,recipes.Length-1);var recipe=recipes[recipeVariant];
                Wrapped(CompanionText.Get("TrainingPanel.Ingredients"),Color.LightSkyBlue);
                if(recipes.Length>1){rows.Add((CompanionText.Get("TrainingPanel.SwitchRecipe" ,recipeVariant+1, recipes.Length),Color.White,()=>recipeVariant=(recipeVariant+1)%recipes.Length));Wrapped("");}
                foreach(var ingredient in recipe.requiredItem.Where(i=>!i.IsAir)){
                    var group=recipe.acceptedGroups.Select(g=>RecipeGroup.recipeGroups[g]).FirstOrDefault(g=>g.ValidItems.Contains(ingredient.type));
                    int owned=TrainingProgress.Owned(s.Player,recipe,ingredient);
                    Wrapped($"{(group==null?ingredient.Name:group.GetText())}   {owned} / {ingredient.stack}",owned>=ingredient.stack?Color.LightGreen:Color.LightGray);
                }
                foreach(int tile in recipe.requiredTile.Where(t=>t>=0))Wrapped(CompanionText.Get("TrainingPanel.Station")+Lang.GetMapObjectName(Terraria.Map.MapHelper.TileToLookup(tile,0)),s.Player.adjTile[tile]?Color.LightGreen:Color.LightGray);
                foreach(var condition in recipe.Conditions)Wrapped(condition.Description.Value,condition.IsMet()?Color.LightGreen:Color.LightGray);
                string status=TrainingProgress.Status(s,id,recipe);
                if(status!=CompanionText.Get("TrainingPanel.MissingMaterials")){Wrapped("");Wrapped(status,Color.LightSkyBlue);}
            }
        }
        RowCount=rows.Count;
        int visibleRows=legend?10:14;
        scrollLine=Math.Clamp(scrollLine,0,Math.Max(0,rows.Count-visibleRows));
        if(rows.Count>visibleRows){
            bodyScroll??=new UIScrollbar();bodyScroll.Left.Set(x+706,0);bodyScroll.Top.Set(y+bodyTop+2,0);bodyScroll.Height.Set(303-(legend?78:0),0);bodyScroll.SetView(visibleRows,rows.Count);bodyScroll.Recalculate();
            // Use the native scrollbar artwork with this panel's scaled pointer coordinates.
            float track=293-(legend?78:0),thumb=track*visibleRows/rows.Count;float handle=y+bodyTop+7+track*scrollLine/rows.Count;
            if(click&&new Rectangle(x+701,y+bodyTop-1,26,314-(legend?78:0)).Contains(mouse)){scrollDragging=true;scrollOffset=mouse.Y>=handle&&mouse.Y<=handle+thumb?mouse.Y-handle:thumb/2;}
            if(!Main.mouseLeft)scrollDragging=false;
            if(scrollDragging)scrollLine=(int)Math.Clamp((mouse.Y-y-bodyTop-7-scrollOffset)/track*rows.Count,0,rows.Count-visibleRows);
            bodyScroll.ViewPosition=scrollLine;bodyScroll.Draw(sb);
        }else scrollDragging=false;
        for(int i=scrollLine;i<Math.Min(rows.Count,scrollLine+visibleRows);i++) {
            int rowY=bodyTextY+(i-scrollLine)*lineHeight;
            if(rows[i].action is {} action)Button(rows[i].text,x+204,rowY,CompanionButtonLayout.ContentWidth(rows[i].text),action);
            else Text(rows[i].text,x+204,rowY,rows[i].color,.78f);
        }
        if(scaled){sb.End();sb.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,null,null,null,Main.UIScaleMatrix);}
        return true;
    }
}

public sealed class TrainingGuide:ModPlayer {
    int seen,timer,quietUntil;
    internal bool KnowsBench=>(seen&2)!=0;
    internal void UsedBench()=>seen|=3;
    public override void SaveData(TagCompound tag)=>tag["seen"]=seen;
    public override void LoadData(TagCompound tag)=>seen=tag.GetInt("seen");
    public override void PostUpdate(){if(Player.whoAmI!=Main.myPlayer||Main.dedServ||Main.gameMenu||ReferenceEquals(Player,CompanionNativeUse.Current?.Actor))return;if(++timer%300!=0||!ModContent.GetInstance<CompanionConfig>().TrainingHints)return;var s=Player.GetModPlayer<CompanionPlayer>();if(timer<quietUntil)return;if(s.Training.Expanded)seen|=8;if(s.Training.Tier>=1)seen|=16;if(s.Training.Tier>=2)seen|=32;if(s.Training.Slots.Any(i=>!i.IsAir))seen|=5;if(!s.Bonded||!s.Enabled||s.Stowed||Player.dead||s.Vitals.Downed||CompanionThreat.WorkDanger(Player,Player.Center))return;
        bool Has(int type)=>Player.inventory.Any(i=>!i.IsAir&&i.type==type);
        string line=null,context="";int bit=0;
        if((seen&1)==0){bit=1;line="Workbench";}
        else if((seen&2)==0&&Has(TrainingCatalog.Type(19))){bit=2;line="PlaceBench";}
        else if((seen&4)==0&&Player.inventory.Any(i=>TrainingCatalog.Id(i)>=0&&TrainingCatalog.Id(i)<11)&&s.Training.Slots.All(i=>i.IsAir)){bit=4;line="Equip";}
        else if((seen&8)==0&&!s.Training.Expanded&&(Has(ItemID.Bone)||Has(ItemID.ShadowScale)||Has(ItemID.TissueSample))){bit=8;line="Belt";}
        else if((seen&16)==0&&s.Training.Tier==0&&Has(ItemID.HallowedBar)){bit=16;line="Improved";}
        else if((seen&32)==0&&s.Training.Tier==1&&Has(ItemID.Ectoplasm)){bit=32;line="Master";}
        if(line==null&&(seen&64)==0){int ready=Enumerable.Range(0,11).FirstOrDefault(id=>TrainingCatalog.Current(id)&&TrainingProgress.Recipes(id).Any(r=>TrainingProgress.Materials(Player,r)),-1);if(ready>=0){bit=64;line="Ready";context=TrainingCatalog.Entries[ready].Name;}}
        if(line!=null&&s.Expression.TryHint(s,line,context)){seen|=bit;quietUntil=timer+1800;}
    }
}
