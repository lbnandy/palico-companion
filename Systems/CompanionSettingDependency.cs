using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;

namespace PalicoCompanion.Systems;

// Keep the native control and its pending value in place. A disabled overlay
// prevents input without rebuilding the list or moving the scroll position.
internal sealed class CompanionSettingDependency : UIElement {
    readonly UIElement control;
    readonly Func<string> reason;
    readonly Dictionary<UIElement,bool> interaction=new();
    public CompanionSettingDependency(UIElement control,Func<string> reason){
        this.control=control;this.reason=reason;Width.Set(0,1);Height.Set(0,1);SetPadding(0);
    }
    public override bool ContainsPoint(Vector2 point)=>reason()!=null&&base.ContainsPoint(point);
    public override void Update(GameTime time){
        base.Update(time);
        if(reason()!=null)Block(control);
        else if(interaction.Count>0){
            foreach(var pair in interaction)pair.Key.IgnoresMouseInteraction=pair.Value;
            interaction.Clear();
        }
    }
    void Block(UIElement element){
        interaction.TryAdd(element,element.IgnoresMouseInteraction);
        element.IgnoresMouseInteraction=true;
        foreach(var child in element.Children)Block(child);
    }
    protected override void DrawSelf(SpriteBatch batch){
        string key=reason();if(key==null)return;
        batch.Draw(TextureAssets.MagicPixel.Value,GetDimensions().ToRectangle(),Color.Black*.45f);
        if(IsMouseHovering)Terraria.ModLoader.UI.UICommon.TooltipMouseText(Language.GetTextValue("Mods.PalicoCompanion.Settings."+key));
    }
    public override void LeftClick(UIMouseEvent evt){}
    public override void LeftMouseDown(UIMouseEvent evt){}
    public override void RightClick(UIMouseEvent evt){}
    public override void RightMouseDown(UIMouseEvent evt){}
}

