using System.Globalization;
using System.Text;
using Terraria;
using Terraria.GameContent.UI.States;
using Terraria.Localization;
using Terraria.UI;
using Terraria.GameInput;
using Terraria.GameContent.UI.Elements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace PalicoCompanion.Systems;
public static class CompanionNaming {
    public static bool Rename(CompanionPlayer state,string text) {
        var clean=new StringBuilder();int count=0;
        var chars=StringInfo.GetTextElementEnumerator((text??"").Trim());
        while(chars.MoveNext()&&count<24) {
            string part=chars.GetTextElement();if(char.IsControl(part[0]))continue;
            clean.Append(part.Replace('[','［').Replace(']','］'));count++;
        }
        if(clean.Length==0)return false;
        state.CatName=clean.ToString();return true;
    }
    public static void Open(CompanionPlayer state,bool first=false)=>CompanionAppearance.Open(state,first);
}
