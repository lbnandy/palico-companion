using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace PalicoCompanion.Systems;

// Adapt visual primitives from stable Player held-item rendering / rapier AI.
// Labor has its own timed tile action. Combat uses CompanionNativeUse's isolated
// Player executor; neither path calls ItemCheck on the human player's object.
public static class CompanionItemVisual {
    public static void BeginUse(Item item,Vector2 at) {
        if(CompanionNetwork.Server){CompanionNetwork.FeedbackAt(4,at,item.type);return;}
        if(item.UseSound.HasValue&&!ItemID.Sets.SkipsInitialUseSound[item.type]) {
            SoundEngine.PlaySound(item.UseSound,at);
        }
    }
    // Vanilla swing spans 3.5 radians. Anchor contact at our real mining hit
    // (60% of the swing), and mirror in world space for left-facing labor.
    public static float SwingAngle(float aim,float progress,int direction)=>
        aim+3.5f*(Math.Clamp(progress,0,1)-0.6f)*(direction<0?-1:1);
    // Player.ItemCheck_ApplyUseStyle_Inner, Swing use style: tool rotation
    // depends on animation time and facing, not the mouse/target elevation.
    public static float? ToolRotation(Item item,float progress,int direction)=>
        item.useStyle==ItemUseStyleID.Swing&&(item.pick>0||item.axe>0||item.hammer>0)
            ?((Math.Clamp(progress,0,1)-0.5f)*3.5f-0.3f)*(direction<0?-1:1):null;
    public static float ThrustTravel(float progress,float range)=>
        (Math.Clamp(progress,0,1)-0.5f)*range*0.6f;
    public static float ThrustOpacity(float progress) =>
        Math.Clamp(progress*16/7,0,1)*Math.Clamp((16-progress*16)/4,0,1);
    public static float ItemScale(Item item)=>Math.Clamp(item.scale,0.1f,4f);
    public static Vector2 SwingOrigin(int width,int height,int direction)=>new(direction<0?width:0,height);
    public static float SpriteRotation(float bladeAngle,int width,int height,int direction) {
        float diagonal=(float)Math.Atan2(height,width);
        return bladeAngle+(direction<0?MathHelper.Pi-diagonal:diagonal);
    }
    public static void DrawSwing(SpriteBatch batch,Item item,Vector2 hand,float angle,int direction,Color light,int tick,float? spriteRotation=null) {
        Main.instance.LoadItem(item.type);var texture=TextureAssets.Item[item.type].Value;
        Rectangle frame=Main.itemAnimations[item.type]?.GetFrame(texture)??texture.Bounds;
        Vector2 origin=SwingOrigin(frame.Width,frame.Height,direction);
        float rotation=spriteRotation??SpriteRotation(angle,frame.Width,frame.Height,direction),scale=ItemScale(item);
        var flip=direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None;
        // Same opaque-handle anchor as native weapons, with the work swing's
        // own timing. The actual tool pixels, rather than a texture corner,
        // remain covered by the forepaw through both facing directions.
        hand+=hand-CompanionGrip.DrawnGrip(new Terraria.DataStructures.DrawData(texture,hand,frame,light,rotation,origin,scale,flip,0));
        batch.Draw(texture,hand,frame,item.GetAlpha(light),rotation,origin,scale,flip,0);
        if(item.color!=default(Color))batch.Draw(texture,hand,frame,item.GetColor(light),rotation,origin,scale,flip,0);
        if(item.glowMask>=0&&item.glowMask<TextureAssets.GlowMask.Length) {
            var mask=TextureAssets.GlowMask[item.glowMask].Value;
            if(mask.Width>=frame.Right&&mask.Height>=frame.Bottom)
                batch.Draw(mask,hand,frame,new Color(250,250,250,item.alpha),rotation,origin,scale,flip,0);
        }
        if(item.flame) {
            Main.instance.LoadItemFlames(item.type);
            if(TextureAssets.ItemFlame[item.type].IsLoaded) {
                var flame=TextureAssets.ItemFlame[item.type].Value;
                if(flame.Width>=frame.Right&&flame.Height>=frame.Bottom)
                    for(int i=0;i<7;i++) {
                        var jitter=new Vector2((float)Math.Sin(tick*.18f+i*2)*1.2f,-(float)Math.Abs(Math.Sin(tick*.12f+i))*1.5f);
                        batch.Draw(flame,hand+jitter,frame,new Color(100,100,100,0),rotation,origin,scale,flip,0);
                    }
            }
        }
    }
    public static void DrawThrust(SpriteBatch batch,Item item,Vector2 hand,float aim,float progress,Color light) {
        // Vanilla shortswords hide the item icon and draw a dedicated rapier
        // projectile. Borrow that texture, NOT its player-anchored AI/heldProj.
        Main.instance.LoadProjectile(item.shoot);var texture=TextureAssets.Projectile[item.shoot].Value;
        var frame=new Rectangle(0,0,texture.Width,texture.Height/Math.Max(1,Main.projFrames[item.shoot]));
        float scale=ItemScale(item),half=frame.Height*scale/2;
        Vector2 center=hand+aim.ToRotationVector2()*(half+ThrustTravel(progress,CompanionWeapons.Range(item)));
        batch.Draw(texture,center,frame,light*ThrustOpacity(progress),aim+MathHelper.PiOver2,frame.Size()/2,scale,SpriteEffects.None,0);
    }
    public static void DrawGrip(SpriteBatch batch,Vector2 feet,int pose,int direction,Color light,bool offhand=false) {
        var atlas=CompanionSprite.Atlas;if(atlas is null)return;
        var offset=offhand?CompanionSpriteLayout.OffHand[pose]:CompanionSpriteLayout.MainHand[pose];
        var point=CompanionSpriteLayout.Origin+offset/CompanionSpriteLayout.Scale;
        int x=(int)point.X-1,y=(int)point.Y;
        var source=new Rectangle(x,pose*CompanionSpriteLayout.Height+y,3,2);
        float left=direction<0?CompanionSpriteLayout.Origin.X-x-source.Width:x-CompanionSpriteLayout.Origin.X;
        var at=feet+new Vector2(left,y-CompanionSpriteLayout.Origin.Y)*CompanionSpriteLayout.Scale;
        CompanionDye.Draw(batch,new Terraria.DataStructures.DrawData(atlas,at,source,light,0,Vector2.Zero,CompanionSpriteLayout.Scale,direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None));
    }
}
