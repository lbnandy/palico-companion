using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using ReLogic.Graphics;

namespace PalicoCompanion.Systems;

internal static class CompanionFoodDisplay {
    internal static bool Active(CompanionPlayer s)=>s.Vitals.FoodTime>0;
    internal static float Remaining(CompanionPlayer s)=>Math.Clamp(s.Vitals.FoodTime/(float)Math.Max(1,s.Vitals.FoodDuration),0,1);
    internal static string Time(CompanionPlayer s)=>Lang.LocalizedDuration(
        TimeSpan.FromSeconds(Math.Max(1,(s.Vitals.FoodTime+59)/60)),abbreviated:true,showAllAvailableUnits:false);
    // Keep the native-sized buff outside the card, centered on its name line.
    internal static Rectangle HudBounds(Rectangle card)=>new(card.Right+12,card.Y+4,32,50);

    // Reuse the vanilla food icon and buff tooltip, without borrowing a slot
    // from the player's buffs or enabling vanilla's right-click removal.
    internal static bool Draw(SpriteBatch batch,CompanionPlayer s,Rectangle bounds,bool compact=false){
        if(!Active(s))return false;
        bool hover=bounds.Contains(Main.mouseX,Main.mouseY);
        var icon=TextureAssets.Buff[BuffID.WellFed].Value;
        int size=compact?bounds.Width:32;
        batch.Draw(icon,new Rectangle(bounds.X,bounds.Y,size,size),Color.White);
        int darkHeight=(int)MathF.Round(size*(1-Remaining(s)));
        if(darkHeight>0){
            // Sample the native icon itself so its transparent corners remain
            // intact. Match the skills' elapsed-time veil and fill direction.
            int sourceHeight=Math.Max(1,(int)MathF.Round(icon.Height*darkHeight/(float)size));
            batch.Draw(icon,new Rectangle(bounds.X,bounds.Y,size,darkHeight),new Rectangle(0,0,icon.Width,sourceHeight),TrainingHud.CooldownVeil);
        }
        string name=CompanionFeeding.Text("BuffName");
        if(compact){
            string time=Time(s);var font=FontAssets.ItemStack.Value;var measured=font.MeasureString(time);
            float scale=Math.Min(.8f,size/Math.Max(1,measured.X));
            batch.DrawString(font,time,new Vector2(bounds.Center.X,bounds.Y+size+2),Color.White,0,new Vector2(measured.X/2,0),scale,SpriteEffects.None,0);
        }else{
            NativeLabelText.Draw(batch,name,new Rectangle(bounds.X+42,bounds.Y-2,bounds.Width-42,20),Color.White,.72f);
            Utils.DrawBorderString(batch,Time(s),new Vector2(bounds.X+42,bounds.Y+18),new Color(190,200,230),.62f);
        }
        if(hover){
            s.Player.mouseInterface=true;
            Main.instance.MouseText(name,CompanionFeeding.Status(s));
        }
        return hover;
    }
}
