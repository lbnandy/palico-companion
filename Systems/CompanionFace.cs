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
        int bob=CompanionDivingVisual.HeadBob(pose);
        var source=new Rectangle(11,CompanionSpriteLayout.Blink*CompanionSprite.Height+14,10,2);
        // Crop origins must mirror about the same whole-frame origin as DrawBody.
        float x=direction<0?CompanionSprite.Width-source.Right:source.X;
        var origin=CompanionSpriteLayout.Origin-new Vector2(x,14+bob);
        CompanionDye.Draw(batch,new DrawData(CompanionSprite.Atlas,feet,source,light,0,origin,
            CompanionSpriteLayout.Scale,direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None));
    }
}
