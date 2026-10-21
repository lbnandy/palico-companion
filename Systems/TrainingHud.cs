using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace PalicoCompanion.Systems;

// Stable equipment order, compact like native buffs; no empty/locked placeholders.
// Timers are the same synchronized state used to activate skills, never a UI clock.
internal static class TrainingHud {
    const float TimerInset=2f;
    // Skill state has a stable palette across loadouts. Tint the native slot
    // background only; its silhouette, texture detail and item draw stay native.
    // Shift the accepted grey-violet slightly toward purple, retaining S/V.
    static readonly Color SkillTint=new(109,84,148);
    // Vanilla InventoryBack edge/fill ratio, normalized for tint multiplication:
    // (18/63,18/65,38/151)*255. SkillTint then produces about (31,23,37).
    static readonly Color SkillEdgeRatio=new(73,71,64);
    static Texture2D slotBackground;
    internal static Texture2D SlotBackground {
        get {Shape(Main.graphics.GraphicsDevice,TextureAssets.InventoryBack13.Width(),TextureAssets.InventoryBack13.Height());return slotBackground;}
    }
    static readonly Color CooldownVeil=new Color(13,15,26)*.64f;
    static readonly Color ActiveEdge=new(222,190,117);
    static readonly Dictionary<(int width,int height),(Texture2D fill,Texture2D edge)> Shapes=new();
    // Keep the native inventory silhouette instead of approximating its stepped
    // corners. Both active outline and veil use the native pixel regions.
    static (Texture2D fill,Texture2D edge) Shape(GraphicsDevice device,int width,int height){
        var key=(width,height);if(Shapes.TryGetValue(key,out var cached))return cached;
        var native=TextureAssets.InventoryBack13.Value;var source=new Color[native.Width*native.Height];native.GetData(source);
        // Native vanity equipment uses InventoryBack13 plus the current
        // loadout tint. Preserve the full native border and interior proportions.
        var slotTexture=native;
        var slotPixels=source;
        Color BackgroundAt(int x,int y){
            int sx=Math.Min(slotTexture.Width-1,(int)((x+.5f)*slotTexture.Width/width));
            int sy=Math.Min(slotTexture.Height-1,(int)((y+.5f)*slotTexture.Height/height));
            return slotPixels[sy*slotTexture.Width+sx];
        }
        // Identify the native outline colour at the top-centre of the slot.
        // Classify before scaling so gold replaces exactly the original pixels,
        // and the veil covers every interior pixel without a synthetic inset.
        Color border=Color.Transparent;
        for(int y=0;y<native.Height;y++){
            var c=source[y*native.Width+native.Width/2];
            if(c.A>0){border=c;break;}
        }
        if(slotBackground==null){
            var recolored=(Color[])source.Clone();
            for(int i=0;i<recolored.Length;i++){
                var c=recolored[i];
                if(c.A>0&&c.R==border.R&&c.G==border.G&&c.B==border.B)
                    recolored[i]=new Color(SkillEdgeRatio.R,SkillEdgeRatio.G,SkillEdgeRatio.B,c.A);
            }
            slotBackground=new Texture2D(device,native.Width,native.Height);slotBackground.SetData(recolored);
        }
        var fill=new Color[width*height];var edge=new Color[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){
            var c=BackgroundAt(x,y);int i=y*width+x;
            if(c.A==0)continue;
            bool isBorder=c.R==border.R&&c.G==border.G&&c.B==border.B;
            if(isBorder)edge[i]=Color.White*(c.A/255f);
            else fill[i]=Color.White*(c.A/255f);
        }
        var fillTexture=new Texture2D(device,width,height);fillTexture.SetData(fill);
        var edgeTexture=new Texture2D(device,width,height);edgeTexture.SetData(edge);
        return Shapes[key]=(fillTexture,edgeTexture);
    }
    internal static void Release(){foreach(var shape in Shapes.Values){shape.fill.Dispose();shape.edge.Dispose();}Shapes.Clear();slotBackground?.Dispose();slotBackground=null;Array.Clear(LegendItems);}
    static void NativeItem(SpriteBatch batch,CompanionPlayer s,Item item,Vector2 position,float scale){
        float oldScale=Main.inventoryScale;
        try{
            Main.inventoryScale=scale;
            var background=SlotBackground;
            var tint=new Color(SkillTint.ToVector4()*Main.inventoryBack.ToVector4());
            // One native-shaped background pass: only source border RGB differs.
            // Drawing an opaque border mask on top changes filtered alpha coverage.
            batch.Draw(background,position,null,tint,0,Vector2.Zero,scale,SpriteEffects.None,0);
            // These skill items are non-stacking. Reuse the exact native icon
            // routine, including item lighting and Pre/PostDrawInInventory hooks.
            Terraria.UI.ItemSlot.DrawItemIcon(item,Terraria.UI.ItemSlot.Context.EquipAccessoryVanity,batch,
                position+background.Size()*scale/2f,scale,32f,Color.White);
        }
        finally{Main.inventoryScale=oldScale;}
    }
    static readonly Item[] LegendItems=new Item[2];
    internal static void DrawLegend(SpriteBatch batch,CompanionPlayer s,Vector2 at){
        var native=TextureAssets.InventoryBack13.Value;var shape=Shape(batch.GraphicsDevice,native.Width,native.Height);
        string[] labels=[CompanionText.Get("TrainingHud.Ready"),CompanionText.Get("TrainingHud.Cooldown"),CompanionText.Get("TrainingHud.Active"),CompanionText.Get("TrainingHud.Triggered")];
        for(int i=0;i<4;i++){
            int icon=i==3?1:0;var item=LegendItems[icon]??=new Item(TrainingCatalog.Type(i==3?9:0));
            var pos=at+new Vector2(i*118+35,0);const float scale=.8f;
            NativeItem(batch,s,item,pos,scale);
            int height=i==1?34:i==2?17:0;
            if(height>0)batch.Draw(shape.fill,pos,new Rectangle(0,0,native.Width,height),CooldownVeil,0,Vector2.Zero,scale,SpriteEffects.None,0);
            float glow=i==2?1:i==3?TrainingHudFeedback.Pulse(Main.GameUpdateCount%90):0;
            if(glow>0){batch.Draw(shape.fill,pos,null,new Color(240,211,145)*(.12f*glow),0,Vector2.Zero,scale,SpriteEffects.None,0);batch.Draw(shape.edge,pos,null,ActiveEdge*glow,0,Vector2.Zero,scale,SpriteEffects.None,0);}
            if(i is 1 or 2)Utils.DrawBorderString(batch,i==1?"18s":"3s",pos+new Vector2(40-TimerInset,45),i==1?Color.White:new Color(255,220,128),CompanionStatusBar.DetailTextScale,1,1);
            CenteredButtonText.Draw(batch,labels[i],new Rectangle((int)at.X+i*118,(int)at.Y+47,112,20),Color.LightGray,.68f);
        }
    }
    internal static IEnumerable<int> Slots(CompanionPlayer s){
        for(int i=0;i<4;i++)if((i==3||i<s.Training.Count(s.Player))&&TrainingCatalog.Id(s.Training.Slots[i]) is >=0 and <=10)yield return i;
    }
    internal static bool HasEquipment(CompanionPlayer s){foreach(int _ in Slots(s))return true;return false;}
    // Supports pack from the left; work gear stays anchored to the right even
    // when support slots are empty or locked. Icon, recharge veil and timer
    // form one compact skill tile, rather than an inventory slot plus a label.
    const int SupportStride=49;
    static Rectangle Cell(Rectangle r,int slot,int index)=>slot==3
        ?new(r.Right-58,r.Y+74,42,42):new(r.X+16+SupportStride*index,r.Y+74,42,42);
    internal static int LeftEdge(CompanionPlayer s,float scale,Vector2 offset){
        var r=CompanionStatusBar.Bounds(s.Player);
        int margin=(int)MathF.Round(r.Right*scale+offset.X)-CompanionStatusBar.BarPixels(s.Player,scale,offset).Right;
        return (int)MathF.Round(r.X*scale+offset.X)+margin;
    }
    static bool Available(CompanionPlayer s,int kind)=>s.Training.Active(s);
    internal static int HitSkill(CompanionPlayer s,Rectangle r){
        int index=0;foreach(int slot in Slots(s)){if(Cell(r,slot,index++).Contains(Main.mouseX,Main.mouseY)){int kind=TrainingCatalog.Id(s.Training.Slots[slot]);return CompanionTraining.ManualSkill(kind)?kind:-1;}}return -1;
    }
    internal static string HoverText(CompanionPlayer s,Rectangle r){
        string hint=null;int index=0;
        foreach(int slot in Slots(s)){
            var cell=Cell(r,slot,index++);int kind=TrainingCatalog.Id(s.Training.Slots[slot]);
            if(cell.Contains(Main.mouseX,Main.mouseY))hint=TrainingCatalog.Entries[kind].Name+" · "+TrainingStatus.Describe(s,slot)+"\n"+string.Join("\n",TrainingItem.TooltipLines(TrainingGuideCopy.Detail(kind,s.Training.Tier)))+(CompanionTraining.ManualSkill(kind)?CompanionText.Get("TrainingHud.ClickUse"):"");
        }
        return hint;
    }
    internal static void DrawIcons(SpriteBatch batch,CompanionPlayer s,Rectangle r,float scale,Vector2 offset){
        int index=0;if(!HasEquipment(s))return;
        foreach(int slot in Slots(s)){
            var cell=Cell(r,slot,index++);var item=s.Training.Slots[slot];int kind=TrainingCatalog.Id(item);
            bool active=s.Training.Active(s)&&TrainingStatus.Effect(s,kind)>0;
            bool available=Available(s,kind);
            int rim=Math.Max(1,(int)MathF.Round(scale));
            float slotScale=.8f*scale;
            var native=TextureAssets.InventoryBack13.Value;
            float extent=native.Width*slotScale;
            int right=CompanionStatusBar.BarPixels(s.Player,scale,offset).Right;
            var position=new Vector2(slot==3?right-extent:LeftEdge(s,scale,offset)+SupportStride*(index-1)*scale,
                cell.Y*scale+offset.Y);
            var inside=new Rectangle((int)position.X,(int)position.Y,(int)MathF.Round(extent),(int)MathF.Round(native.Height*slotScale));
            inside.Inflate(-rim,-rim);
            // Native source silhouette and item-icon path at inventory scale.
            NativeItem(batch,s,item,position,slotScale);
            var shape=Shape(batch.GraphicsDevice,native.Width,native.Height);
            float ready=active?Math.Clamp(TrainingStatus.Effect(s,kind)/(float)Math.Max(1,s.Training.EffectDuration(kind)),0,1):available?TrainingStatus.Recharge(s,slot):0;
            int darkHeight=Math.Clamp((int)MathF.Round(native.Height*(1-ready)),0,native.Height);
            if(darkHeight>0)batch.Draw(shape.fill,position,new Rectangle(0,0,native.Width,darkHeight),CooldownVeil,0,Vector2.Zero,slotScale,SpriteEffects.None,0);
            float glow=slot==3?TrainingWorkFeedback.Strength(s,kind):TrainingHudFeedback.Strength(s,slot);
            if(glow>0){
                batch.Draw(shape.fill,position,null,new Color(240,211,145)*(.12f*glow),0,Vector2.Zero,slotScale,SpriteEffects.None,0);
                batch.Draw(shape.edge,position,null,ActiveEdge*glow,0,Vector2.Zero,slotScale,SpriteEffects.None,0);
            }
            // Show one coherent state: gold duration while active, then white
            // cooldown. The cooldown continues to run underneath the effect.
            int seconds=active?(TrainingStatus.Effect(s,kind)+59)/60:(TrainingStatus.Remaining(s,slot)+59)/60;
            // MouseText's line box includes lower leading. Compensate so the
            // visible digits, rather than that empty leading, sit at the corner.
            if(seconds>0)Utils.DrawBorderString(batch,seconds+"s",new Vector2(inside.Right-rim-(int)MathF.Round(TimerInset*scale),inside.Bottom+(int)MathF.Round(4*scale)),active?new Color(255,220,128):new Color(242,245,250),CompanionStatusBar.DetailTextScale*scale,1,1);
        }
    }
}
