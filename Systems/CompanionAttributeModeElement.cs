using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.UI;

namespace PalicoCompanion.Systems;

// This row belongs visually to abilities, but uses the local config's own save
// and preference synchronization. A guest never edits the host's balance file.
public sealed class CompanionAttributeModeElement : ConfigElement {
    public override void OnBind(){
        base.OnBind();Height.Set(110,0);
        TextDisplayFunction=()=>Language.GetTextValue("Mods.PalicoCompanion.Settings.PersonalMode");
        TooltipFunction=()=>Language.GetTextValue("Mods.PalicoCompanion.Settings.ModeHelp");
        int row=0;
        foreach(var mode in new[]{CompanionAttributeMode.Inherited,CompanionAttributeMode.Independent}){
            var button=new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("Mods.PalicoCompanion.Equipment."+mode),.8f);
            button.SetPadding(4);button.Left.Set(8,0);button.Top.Set(34+row++*36,0);button.Width.Set(-16,1);button.Height.Set(32,0);
            ConfigTypography.Apply(button);
            button.OnLeftClick+=(_,_)=>{
                var config=ModContent.GetInstance<CompanionConfig>();
                if(config.AttributeMode==mode)return;
                config.SelectAttributeMode(mode);
                SoundEngine.PlaySound(SoundID.MenuTick);
            };
            button.OnUpdate+=_=>{
                bool selected=ModContent.GetInstance<CompanionConfig>().AttributeMode==mode;
                button.BackgroundColor=selected?new Color(48,85,108):button.IsMouseHovering?UICommon.DefaultUIBlueMouseOver:UICommon.DefaultUIBlue;
                button.BorderColor=selected?new Color(143,203,207):Color.Black;
            };
            Append(button);
        }
    }
}
