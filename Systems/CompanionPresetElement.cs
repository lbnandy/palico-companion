using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.UI;

namespace PalicoCompanion.Systems;

public sealed class CompanionPresetElement : ConfigElement<CompanionStrengthPreset> {
    readonly List<(CompanionStrengthPreset preset,UIAutoScaleTextTextPanel<string> button)> choices=new();
    static string Text(CompanionStrengthPreset preset,string part) => Language.GetTextValue($"Mods.PalicoCompanion.StrengthPresets.{preset}.{part}");

    public override void OnBind() {
        base.OnBind();
        Height.Set(146,0);
        TextDisplayFunction=()=>Label+": "+Text(Value,"Label");
        var explanation=TooltipFunction;
        TooltipFunction=()=>{
            foreach(var choice in choices)if(choice.button.IsMouseHovering)return Text(choice.preset,"Tooltip");
            return explanation?.Invoke()??"";
        };
        foreach(var preset in new[]{CompanionStrengthPreset.Assist,CompanionStrengthPreset.Standard,CompanionStrengthPreset.Strong}) {
            var button=new UIAutoScaleTextTextPanel<string>(Text(preset,"Label"),0.8f);
            button.SetPadding(4);
            button.Top.Set(32+choices.Count*36,0);
            button.Left.Set(8,0);
            button.Width.Set(-16,1);
            button.Height.Set(32,0);
            ConfigTypography.Apply(button);
            button.OnLeftClick+=(_,_)=>{
                if(Value==preset)return;
                Value=preset;
                SoundEngine.PlaySound(SoundID.MenuTick);
            };
            button.OnUpdate+=_=>{
                bool selected=Value==preset;
                button.BackgroundColor=selected?new Color(48,85,108):button.IsMouseHovering?UICommon.DefaultUIBlueMouseOver:UICommon.DefaultUIBlue;
                button.BorderColor=selected?new Color(143,203,207):Color.Black;
            };
            choices.Add((preset,button));
            Append(button);
        }
    }
}
