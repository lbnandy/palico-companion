using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace PalicoCompanion.Systems;

// Change only the native slot-colour query during this exact draw. Never
// switch the human player's CurrentLoadoutIndex or equipment to render the cat.
public sealed class CompanionLoadoutTint:ModSystem {
    static int drawingIndex=-1;
    static Color? drawingTint;
    public override void Load()=>On_ItemSlot.GetColorByLoadout+=ColorFor;
    public override void Unload(){On_ItemSlot.GetColorByLoadout-=ColorFor;drawingIndex=-1;drawingTint=null;}
    static Color ColorFor(On_ItemSlot.orig_GetColorByLoadout orig,int slot,int context) {
        if(drawingTint is {} tint)return new Color(tint.ToVector4()*Main.inventoryBack.ToVector4());
        if(drawingIndex>=0&&ItemSlot.TryGetSlotColor(drawingIndex,context,out var color))
            return new Color(color.ToVector4()*Main.inventoryBack.ToVector4());
        return orig(slot,context);
    }
    internal static void Draw(SpriteBatch batch,CompanionPlayer state,Item[] items,int context,int slot,Vector2 position,Color? tint=null) {
        int previous=drawingIndex;var previousTint=drawingTint;
        try{drawingIndex=state.Loadouts.Current;drawingTint=tint;ItemSlot.Draw(batch,items,context,slot,position,Color.White);}
        finally{drawingIndex=previous;drawingTint=previousTint;}
    }
}
