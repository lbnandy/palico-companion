using System;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.UI;
using Terraria.UI.Gamepad;
using Terraria.Initializers;
using Terraria.ModLoader;
using Terraria.ModLoader.UI;

namespace PalicoCompanion.Systems;

public sealed class CompanionAppearance : UIState {
    internal static string L(string key)=>Language.GetTextValue("Mods.PalicoCompanion.UI."+key);
    readonly CompanionPlayer state;
    Vector3 hsl;
    readonly bool first;
    public string Text;
    bool nameFocused,previousLeft;
    UIText nameError;NameField nameField;
    readonly List<(string key,UIElement element,bool slider)> navigation=new();
    internal int Draft {get;private set;}
    CompanionAttributeMode draftMode;
    int frames;
    internal static CompanionAppearance Current {get;private set;}
    internal static readonly int[] Presets=[CompanionPalette.Default,0x8C639D,0x5B886E,0xAC6F53,0x9D626C,0x9E9058];
    public CompanionAppearance(CompanionPlayer state,bool first=false){this.state=state;this.first=first;Text=state.CatName;SetColor(state.HoodColor);draftMode=ModContent.GetInstance<CompanionConfig>().AttributeMode;}
    internal static void Open(CompanionPlayer state,bool first=false){state.PanelOpen=false;TrainingPanel.Open=false;var ui=new CompanionAppearance(state,first);Current=ui;IngameFancyUI.OpenUIState(ui);}
    internal void SetColor(int rgb){Draft=CompanionPalette.Valid(rgb);hsl=Main.rgbToHsl(CompanionPalette.ColorOf(Draft));hsl.Z=Math.Clamp((hsl.Z-.15f)/.85f,0,1);}
    // Same lightness range as vanilla UICharacterCreation, avoiding crushed blacks.
    static Color Rgb(Vector3 hsl)=>Main.hslToRgb(hsl.X,hsl.Y,hsl.Z*.85f+.15f);
    static float Get(Vector3 v,int i)=>i==0?v.X:i==1?v.Y:v.Z;
    static void Put(ref Vector3 v,int i,float x){if(i==0)v.X=x;else if(i==1)v.Y=x;else v.Z=x;}
    internal void Slide(int index,float value){Put(ref hsl,index,Math.Clamp(value,0,1));Draft=CompanionPalette.Pack(Rgb(hsl));}
    internal void Confirm()=>Submit();
    public void Submit(){
        if(!CompanionNaming.Rename(state,Text)){nameError?.SetText(L("NameEmpty"));FocusName();return;}
        state.HoodColor=Draft;state.Bonded=true;
        if(first)ModContent.GetInstance<CompanionConfig>().SelectAttributeMode(draftMode);
        CompanionNetwork.UpdateIdentity(state);Finish();
    }
    public void Cancel(){if(first){state.Bonded=true;CompanionNetwork.UpdateIdentity(state);}Finish();}
    internal void Close()=>Cancel();
    void Finish(){PlayerInput.WritingText=false;IngameFancyUI.Close();if(first){state.PanelOpen=false;Main.playerInventory=false;}else CompanionUIFlow.Bag(state);}
    internal void FocusName(){
        if(PlayerInput.UsingGamepad){
            nameFocused=false;PlayerInput.WritingText=false;
            void Return(){Current=this;IngameFancyUI.OpenUIState(this);}
            var keyboard=new UIVirtualKeyboard(L("AppearanceName"),Text,value=>{Text=value;Return();},Return);
            keyboard.SetMaxInputLength(24);IngameFancyUI.OpenUIState(keyboard);return;
        }
        nameFocused=true;Main.clrInput();
    }
    public override void OnDeactivate(){PlayerInput.WritingText=false;if(Current==this)Current=null;base.OnDeactivate();}
    public override void OnInitialize(){
        var panel=new UIPanel();panel.Width.Set(520,0);panel.Height.Set(first?470:316,0);panel.HAlign=panel.VAlign=.5f;Append(panel);
        var title=new UIText(Language.GetText("Mods.PalicoCompanion.UI."+(first?"AppearanceMeetTitle":"AppearanceTitle")),1f);title.Top.Set(7,0);title.Left.Set(8,0);panel.Append(title);
        void Button(string label,float x,float y,float width,Action action){var b=new CenteredTextButton(Language.GetText("Mods.PalicoCompanion.UI."+label));b.Left.Set(x,0);b.Top.Set(y,0);b.Width.Set(width,0);b.Height.Set(34,0);b.OnLeftClick+=(_,_)=>action();panel.Append(b);navigation.Add((label,b,false));}
        void Label(string text,int row){var label=new RowLabel(Language.GetText("Mods.PalicoCompanion.UI."+text));label.Left.Set(180,0);label.Top.Set(40+row*36,0);label.Width.Set(112,0);label.Height.Set(34,0);panel.Append(label);}
        Label("AppearanceName",0);
        // Match ColorBar's visible border (its texture has two transparent
        // edge pixels); UIPanel's own border begins one pixel inside its box.
        int barWidth=Terraria.GameContent.TextureAssets.ColorBar.Width();
        nameField=new NameField(this);nameField.Left.Set(480-barWidth+1,0);nameField.Top.Set(40,0);nameField.Width.Set(barWidth-2,0);nameField.Height.Set(34,0);panel.Append(nameField);
        navigation.Add(("name",nameField,false));
        nameError=new UIText("",.65f){TextColor=Color.Salmon};nameError.Left.Set(300,0);nameError.Top.Set(19,0);panel.Append(nameError);
        var preview=new Preview(this);preview.Left.Set(8,0);preview.Top.Set(40,0);preview.Width.Set(144,0);preview.Height.Set(187,0);panel.Append(preview);
        string[] labels=["AppearanceHue","AppearanceSaturation","AppearanceLightness"];
        for(int i=0;i<3;i++){
            int index=i;Label(labels[i],i+1);
            var slider=new UIColoredSlider(LocalizedText.Empty,()=>Get(hsl,index),v=>Slide(index,v),()=>Slide(index,UILinksInitializer.HandleSliderHorizontalInput(Get(hsl,index),0,1,PlayerInput.CurrentProfile.InterfaceDeadzoneX,.35f)),v=>{
                var value=hsl;if(index==0)value=new(v,1,.5f);else Put(ref value,index,v);return Rgb(value);
            },Color.Transparent);
            slider.Left.Set(180,0);slider.Top.Set(75+i*36,0);slider.Width.Set(310,0);slider.Height.Set(30,0);slider.SetSnapPoint("Cloth",i);panel.Append(slider);
            navigation.Add(("color-"+i,slider,true));
        }
        for(int i=0;i<Presets.Length;i++){
            int rgb=Presets[i];var swatch=new UIPanel{BackgroundColor=CompanionPalette.ColorOf(rgb),BorderColor=new Color(24,29,49)};
            swatch.Left.Set(180+i*52,0);swatch.Top.Set(195,0);swatch.Width.Set(40,0);swatch.Height.Set(32,0);swatch.OnLeftClick+=(_,_)=>SetColor(rgb);panel.Append(swatch);
            navigation.Add(("preset-"+i,swatch,false));
        }
        if(first)AddModeChoice(panel);
        int bottom=first?399:245;
        Button("AppearanceReset",8,bottom,152,()=>SetColor(CompanionPalette.Default));
        Button(first?"AppearanceLater":"AppearanceCancel",172,bottom,152,Cancel);Button(first?"AppearanceStart":"AppearanceConfirm",336,bottom,152,Confirm);
        previousLeft=Main.mouseLeft;Main.clrInput();
    }
    void AddModeChoice(UIPanel panel) {
        var heading=new UIText(Language.GetText("Mods.PalicoCompanion.Settings.MeetMode"),.85f);
        heading.Left.Set(8,0);heading.Top.Set(245,0);panel.Append(heading);
        var description=new UIText("",.75f){IsWrapped=true};
        description.Left.Set(8,0);description.Top.Set(319,0);description.Width.Set(480,0);description.Height.Set(42,0);panel.Append(description);
        void Describe()=>description.SetText(Language.GetTextValue("Mods.PalicoCompanion.Settings.Meet"+draftMode));
        Describe();
        int column=0;
        foreach(var mode in new[]{CompanionAttributeMode.Inherited,CompanionAttributeMode.Independent}) {
            var button=new CenteredTextButton(Language.GetText("Mods.PalicoCompanion.Equipment."+mode));
            button.Left.Set(8+column++*244,0);button.Top.Set(273,0);button.Width.Set(236,0);button.Height.Set(34,0);
            button.OnLeftClick+=(_,_)=>{
                if(draftMode!=mode)Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
                draftMode=mode;nameFocused=false;Describe();
            };
            void Style(){
                bool selected=draftMode==mode;
                button.BackgroundColor=selected?new Color(48,85,108):button.IsMouseHovering?UICommon.DefaultUIBlueMouseOver:UICommon.DefaultUIBlue;
                button.BorderColor=selected?new Color(143,203,207):Color.Black;
            }
            button.OnUpdate+=_=>Style();Style();
            panel.Append(button);navigation.Add(("mode-"+mode,button,false));
        }
        var hint=new UIText(Language.GetText("Mods.PalicoCompanion.Settings.MeetModeHint"),.7f){TextColor=new Color(190,200,220),IsWrapped=true};
        hint.Left.Set(8,0);hint.Top.Set(365,0);hint.Width.Set(480,0);panel.Append(hint);
    }
    public override void Update(GameTime gameTime){
        base.Update(gameTime);frames++;Main.LocalPlayer.mouseInterface=true;
        if(Main.mouseLeft&&!previousLeft)nameFocused=nameField.GetDimensions().ToRectangle().Contains(Main.mouseX,Main.mouseY);
        previousLeft=Main.mouseLeft;PlayerInput.WritingText=nameFocused;
        if(nameFocused){
            Main.instance.HandleIME();Text=Main.GetInputText(Text);
            int[] letters=StringInfo.ParseCombiningCharacters(Text);if(letters.Length>24)Text=Text[..letters[24]];
            if(frames>1&&Main.inputTextEnter){nameFocused=false;PlayerInput.WritingText=false;Main.clrInput();}
            if(!string.IsNullOrWhiteSpace(Text))nameError.SetText("");
        }
        if(frames>1&&Main.keyState.IsKeyDown(Keys.Escape))Cancel();
    }
    public override void Draw(SpriteBatch batch){
        using var links=CompanionGamepad.Begin("appearance");
        base.Draw(batch);
        foreach(var entry in navigation){
            var bounds=entry.element.GetDimensions().ToRectangle();
            if(entry.slider){bounds.X=bounds.Right-178;bounds.Width=168;CompanionGamepad.Slider(entry.key,bounds);}
            else CompanionGamepad.Add(entry.key,bounds);
        }
    }
    sealed class RowLabel(LocalizedText label):UIElement {
        protected override void DrawSelf(SpriteBatch batch){
            string text=label.Value;var r=GetDimensions().ToRectangle();var layout=CenteredButtonText.Layout(text,r,.85f);
            Utils.DrawBorderString(batch,text,new Vector2(r.X,layout.Position.Y),Color.White,layout.Scale);
        }
    }
    sealed class NameField:UITextBox {
        readonly CompanionAppearance ui;
        internal NameField(CompanionAppearance ui):base("",.85f){
            this.ui=ui;TextHAlign=0;ShowInputTicker=false;SetTextMaxLength(int.MaxValue);
            PaddingTop=PaddingBottom=6;
            BackgroundColor=new Color(33,43,79);OnLeftClick+=(_,_)=>ui.FocusName();
        }
        protected override void DrawSelf(SpriteBatch batch){
            var r=GetDimensions();var size=Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(ui.Text);
            float scale=Math.Min(.85f,(r.Width-PaddingLeft-PaddingRight-10)/Math.Max(1,size.X));
            // UITextBox owns the panel, text and blinking caret. Only fit this
            // fixed-width field; UISearchBar would trim the actual long name.
            var centered=CenteredButtonText.Layout(string.IsNullOrWhiteSpace(ui.Text)?CompanionLife.L("AppearanceName"):ui.Text,r.ToRectangle(),scale);
            PaddingTop=centered.Position.Y-r.Y+2*scale;PaddingBottom=0;
            SetText(ui.Text,scale,false);ShowInputTicker=ui.nameFocused;
            BorderColor=ui.nameFocused?new Color(180,194,228):Color.Black;
            // UIPanel caches its inner rectangle. Apply the padding before the
            // native text/caret draw, centering visible glyphs, not line spacing.
            Recalculate();
            base.DrawSelf(batch);
        }
    }
    sealed class Preview:UIPanel {
        readonly CompanionAppearance ui;
        internal Preview(CompanionAppearance ui){this.ui=ui;BackgroundColor=new Color(26,36,67);BorderColor=new Color(17,24,43);}
        protected override void DrawSelf(SpriteBatch batch){base.DrawSelf(batch);var rect=GetDimensions().ToRectangle();
            // Preview the draft cloth colour on the full-body idle sprite.
            // Equipped dye must not obscure the colour being edited.
            var source=new Rectangle(4,5,20,21);
            int zoom=Math.Max(1,(int)(5*Main.UIScale));
            var dest=new Rectangle((int)MathF.Round(rect.Center.X*Main.UIScale)-source.Width*zoom/2,
                (int)MathF.Round(rect.Center.Y*Main.UIScale)-source.Height*zoom/2,source.Width*zoom,source.Height*zoom);
            batch.End();batch.Begin(SpriteSortMode.Immediate,BlendState.AlphaBlend,SamplerState.PointClamp,null,null,null,Matrix.Identity);
            batch.Draw(CompanionPalette.Texture(256,ui.Draft),dest,source,Color.White);
            batch.End();batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,null,null,null,Main.UIScaleMatrix);
        }
    }
    internal static void DrawPortraitPixels(SpriteBatch batch,CompanionPlayer state,Rectangle dest,int? previewColor=null){
        batch.End();batch.Begin(SpriteSortMode.Immediate,BlendState.AlphaBlend,SamplerState.LinearClamp,null,null,null,Matrix.Identity);
        try {CompanionDye.Portrait(batch,state,dest,CompanionStatusBar.PortraitSource,Color.White,previewColor);}
        finally {batch.End();batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,null,null,null,Main.UIScaleMatrix);}
    }
    internal static void DrawPortrait(SpriteBatch batch,Texture2D texture,Rectangle dest,Rectangle source){
        batch.End();batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,null,null,null,Main.UIScaleMatrix);
        batch.Draw(texture,dest,source,Color.White);batch.End();batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,null,null,null,Main.UIScaleMatrix);
    }
}
