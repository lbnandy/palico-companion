using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.UI;
using Terraria.UI;
using Terraria.UI.Chat;

namespace PalicoCompanion.Systems;

// Keep the native enum selector, popup, input handling and save flow. Only its
// labels differ: our settings use the same ItemStack font and .8 scale as the
// surrounding ConfigElement labels. tML does not expose a font option here.
public sealed class ConfigTypography : ModSystem {
    Hook bindHook, optionsHook;
    PropertyInfo boundItem;
    delegate void Bind(ConfigElement self);
    delegate void BindDetour(Bind original, ConfigElement self);
    delegate List<UIAutoScaleTextTextPanel<string>> Options(ConfigElement self);
    delegate List<UIAutoScaleTextTextPanel<string>> OptionsDetour(Options original, ConfigElement self);

    public override void Load() {
        if(Main.dedServ)return;
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var type=typeof(ConfigElement).Assembly.GetType("Terraria.ModLoader.Config.UI.EnumElement2");
        var bind=type?.GetMethod("OnBind",flags);
        var options=type?.GetMethod("CreateDefinitionOptionElementList",flags);
        boundItem=typeof(ConfigElement).GetProperty("Item",flags);
        if(bind==null||options==null||boundItem==null) {
            Mod.Logger.Warn("Settings typography adapter unavailable; using native dropdown typography.");return;
        }
        bindHook=new Hook(bind,(BindDetour)OnBind);
        optionsHook=new Hook(options,(OptionsDetour)CreateOptions);
    }
    public override void Unload(){bindHook?.Dispose();optionsHook?.Dispose();boundItem=null;}
    bool IsOurs(ConfigElement element)=>boundItem.GetValue(element) is CompanionConfig;
    void OnBind(Bind original,ConfigElement self) {
        original(self);
        if(IsOurs(self))foreach(var panel in self.Children.OfType<UIAutoScaleTextTextPanel<string>>())Apply(panel);
    }
    List<UIAutoScaleTextTextPanel<string>> CreateOptions(Options original,ConfigElement self) {
        var options=original(self);
        if(IsOurs(self))foreach(var panel in options)Apply(panel);
        return options;
    }
    static void Apply(UIAutoScaleTextTextPanel<string> panel) {
        panel.TextColor=Color.Transparent;
        panel.Append(new ConfigChoiceLabel(panel));
    }
}

// A noninteractive label; native parent receives all clicks, including popup dismissal.
internal sealed class ConfigChoiceLabel : UIElement {
    readonly UIAutoScaleTextTextPanel<string> choice;
    internal const float Scale=.8f;
    internal ConfigChoiceLabel(UIAutoScaleTextTextPanel<string> choice) {
        this.choice=choice;IgnoresMouseInteraction=true;
        Width.Set(0,1);Height.Set(0,1);
    }
    protected override void DrawSelf(SpriteBatch batch) {
        var rect=choice.UseInnerDimensions?choice.GetInnerDimensions():choice.GetDimensions();
        var font=FontAssets.ItemStack.Value;
        float width=font.MeasureString(choice.Text).X*Scale;
        // Match ConfigElement's eight-unit baseline inset. All native rows are 30 high.
        var at=new Vector2(rect.X+(rect.Width-width)/2,rect.Y+8);
        ChatManager.DrawColorCodedStringWithShadow(batch,font,choice.Text,at,Color.White,0,Vector2.Zero,new Vector2(Scale));
    }
}
