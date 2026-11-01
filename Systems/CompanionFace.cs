using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;

namespace PalicoCompanion.Systems;

// Recompose the eyelids, not the head or hand pose. Uses replicated
// health/petting state on both peers, without sending another visual event.
internal sealed class CompanionFace {
    int health=-1,hurtTicks;
    internal bool Closed {get;private set;}
    internal void Update(int currentHealth,bool downed,int petTicks){
        if(hurtTicks>0)hurtTicks--;
        if(health>=0&&currentHealth<health&&!downed)hurtTicks=12;
        health=currentHealth;
        Closed=!downed&&(hurtTicks>0||petTicks>0&&petTicks%45<32);
    }
    internal static void Draw(SpriteBatch batch,Vector2 feet,int pose,int direction,Color light){
        // These authored expressions already include their closed eyes.
        if(pose is CompanionSpriteLayout.Pet or CompanionSpriteLayout.Downed or CompanionSpriteLayout.RestBlink)return;
        if(!CompanionSpriteLayout.TryFaceOffset(pose,out var offset))return;
        var source=new Rectangle(15,CompanionSpriteLayout.Blink*CompanionSprite.Height+14,6,2);
        // Crop origins must mirror about the same whole-frame origin as DrawBody.
        int targetX=source.X+offset.X;
        float x=direction<0?CompanionSprite.Width-targetX-source.Width:targetX;
        var origin=CompanionSpriteLayout.Origin-new Vector2(x,14+offset.Y);
        CompanionDye.Draw(batch,new DrawData(CompanionSprite.Atlas,feet,source,light,0,origin,
            CompanionSpriteLayout.Scale,direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None));
    }
}
