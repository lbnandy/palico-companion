using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.UI;

namespace PalicoCompanion.Systems;

// The native config page owns the pending value and its Save/Cancel behavior.
public sealed class CompanionHudPositionElement : ConfigElement<Vector2?> {
    public override void OnBind() {
        base.OnBind();
        Height.Set(40,0);
        TextDisplayFunction=()=>"";
        var button=new UIAutoScaleTextTextPanel<string>(Label,.8f);
        button.SetPadding(4);
        button.Left.Set(8,0);button.Top.Set(4,0);
        button.Width.Set(-16,1);button.Height.Set(32,0);
        ConfigTypography.Apply(button);
        button.OnLeftClick+=(_,_)=>{
            if(Value==null)return;
            Value=null;
            SoundEngine.PlaySound(SoundID.MenuTick);
        };
        button.OnUpdate+=_=>{
            button.TextColor=Value.HasValue?Color.White:Color.Gray;
            button.BackgroundColor=Value.HasValue&&button.IsMouseHovering?UICommon.DefaultUIBlueMouseOver:UICommon.DefaultUIBlue;
        };
        Append(button);
    }
}
