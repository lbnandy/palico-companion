using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

internal static class CompanionStatsPanel {
    internal const int Height=464;
    const float Scale=.78f;
    static readonly string[] RowKeys={"Health","Defense","Regen","Reduction","Damage","Crit","Speed","Penetration","FinalDamage","Capacity"};
    static string L(string key,params object[] args)=>Language.GetTextValue("Mods.PalicoCompanion.Attributes."+key,args);
    static float LabelWidth(){
        float width=0;
        foreach(string key in RowKeys)width=Math.Max(width,FontAssets.MouseText.Value.MeasureString(L(key)).X*Scale);
        return width;
    }
    internal static int Width=>Math.Max(360,(int)Math.Ceiling(LabelWidth())+148);
    internal static Rectangle Beside(Rectangle panel,int width,int screenWidth,int screenHeight){
        const int gap=12,margin=4;
        int height=Math.Max(panel.Height,Height);
        int y=Math.Clamp(panel.Y,margin,Math.Max(margin,screenHeight-height-margin));
        if(panel.Right+gap+width<=screenWidth-margin)return new Rectangle(panel.Right+gap,y,width,height);
        if(panel.Left-gap-width>=margin)return new Rectangle(panel.Left-gap-width,y,width,height);
        return Rectangle.Empty;
    }
    internal static void Draw(SpriteBatch batch,CompanionPlayer state,Rectangle area,Point mouse){
        var stats=state.Stats;bool available=stats.Available(state),independent=CompanionEquipment.Independent(state);
        int y=area.Y;const float scale=Scale;
        // Size the label column from localized text, not the backpack width.
        float valueX=area.X+LabelWidth()+20;
        void Text(string text,int atY,Color color){
            while(text.Length>1&&FontAssets.MouseText.Value.MeasureString(text).X*scale>area.Width)text=text[..^2]+"…";
            Utils.DrawBorderString(batch,text,new Vector2(area.X,atY),color,scale);
        }
        void Row(string key,string value,string hint=""){
            var bounds=new Rectangle(area.X,y,area.Width,26);
            Utils.DrawBorderString(batch,L(key),new Vector2(area.X,y),Color.LightSteelBlue,scale);
            Utils.DrawBorderString(batch,value,new Vector2(valueX,y),Color.White,scale);
            if(hint.Length>0){CompanionGamepad.Add("stats-"+key,bounds);if(bounds.Contains(mouse))Main.instance.MouseText(hint);}
            y+=26;
        }
        Text(Language.GetTextValue("Mods.PalicoCompanion.Equipment."+(independent?"Independent":"Inherited")),y,Color.Gold);y+=30;
        string source=L(independent?"EquipmentSource":"InheritedSource",CompanionBalanceConfig.Current.CombatInheritancePercent);
        foreach(string line in CompanionCopy.Wrap(source,(int)(area.Width/scale))){Text(line,y,Color.LightSteelBlue);y+=20;}
        y+=8;
        Row("Health",$"{state.Vitals.Health} / {state.Vitals.MaxHealth}",independent?Language.GetTextValue("Mods.PalicoCompanion.Equipment.HealthHint"):Language.GetTextValue("Mods.PalicoCompanion.UI.HealthHint",CompanionBalanceConfig.Current.HealthRatio));
        Row("Defense",$"{state.Vitals.EffectiveDefense}",L(independent?"EquipmentDefense":"InheritedDefense",CompanionBalanceConfig.Current.DefenseRatio));
        Row("Regen",available?$"{stats.Regen:0.##} / s":"—",L("RegenHint"));
        Row("Reduction",available?$"{stats.Endurance:P0}":"—",L("ReductionHint"));
        string weapon=available&&stats.Weapon>0?Lang.GetItemNameValue(stats.Weapon):L("Unavailable");
        Text(L("Weapon",weapon),y+4,Color.Gold);y+=32;
        var type=available&&stats.Class>=0&&stats.Class<DamageClassLoader.DamageClassCount?DamageClassLoader.GetDamageClass(stats.Class):null;
        Row("Damage",available?$"×{stats.Additive*stats.Multiplicative:0.##}":"—",L("DamageHint",type?.DisplayName.Value??"",stats.Additive,stats.Multiplicative,stats.Base,stats.Flat));
        Row("Crit",available&&type?.UseStandardCritCalcs==true?$"{stats.Crit:0.#}%":"—",L("CritHint"));
        Row("Speed",available?$"×{stats.Speed:0.##}":"—",L("SpeedHint"));
        Row("Penetration",available?$"{stats.Penetration:0.#}":"—",L("PenetrationHint"));
        Row("FinalDamage",$"×{CompanionBalanceConfig.Current.DamageScale:0.##}",L("FinalHint"));
        Row("Capacity",available?$"{stats.Minions} / {stats.Sentries}":"—",L("CapacityHint"));
        if(y+24<area.Bottom)Text(L("DetailsHint"),y+8,Color.LightSteelBlue);
    }
}
