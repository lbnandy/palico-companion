using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class CompanionBuffVisuals:ModSystem {
    // Only native, self-contained status layers. Do not run the complete
    // player renderer, vanity hooks or equipment updates while drawing a cat.
    internal static void Equipment(CompanionCat cat,Color light,List<DrawData> cache){
        cache.Clear();
        if(!cat.Weapons.Present||cat.Weapons.State.Stowed||cat.Weapons.State.Vitals.Downed)return;
        var actor=cat.Weapons.Actor;
        var center=cat.Projectile.Center+new Vector2(0,cat.Projectile.gfxOffY);
        var info=new PlayerDrawSet{drawPlayer=actor,Position=center-actor.Size/2,
            DrawDataCache=cache,colorArmorBody=light,
            playerEffect=cat.Projectile.direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None};
        PlayerDrawLayers.DrawPlayer_26_SolarShield(ref info);
        PlayerDrawLayers.DrawPlayer_37_BeetleBuff(ref info);
        // IceBarrier is anchored to the humanoid body frame rather than its
        // center. Translate that anchor only; retain native texture/animation.
        info.Position.Y-=actor.height/2f-actor.bodyFrame.Height/2f+4+actor.bodyPosition.Y;
        info.Position.X-=actor.bodyPosition.X;
        PlayerDrawLayers.DrawPlayer_35_IceBarrier(ref info);
        foreach(var data in cache)data.Draw(Main.spriteBatch);
    }
    public override void Load(){if(!Main.dedServ)On_Main.DrawInfernoRings+=DrawInfernoRings;}
    public override void Unload()=>On_Main.DrawInfernoRings-=DrawInfernoRings;

    static void DrawInfernoRings(On_Main.orig_DrawInfernoRings orig,Main self){
        orig(self);
        if(Main.gameMenu)return;
        foreach(var projectile in Main.ActiveProjectiles){
            if(projectile.ModProjectile is not CompanionCat cat||!InfernoVisible(cat))continue;
            DrawInferno(self,cat.Weapons.Actor);
        }
    }
    internal static bool InfernoVisible(CompanionCat cat)=>cat.Weapons.Present&&
        !cat.Weapons.State.Stowed&&cat.Weapons.State.Cat==cat.Projectile&&cat.Weapons.Actor.inferno;

    static void DrawInferno(Main main,Player actor){
        // Main.DrawInfernoRings has no single-entity entry point. Reuse its
        // texture, three layers and animation math in the same world pass,
        // without registering a fake player or rerunning buff/damage hooks.
        main.LoadFlameRing();
        if(!Main.gamePaused&&main.IsActive){actor.flameRingScale+=.004f;actor.flameRingRot+=.05f;}
        if(actor.flameRingScale>=1f)actor.flameRingScale=.8f;
        actor.flameRingRot=MathHelper.WrapAngle(actor.flameRingRot);
        for(int layer=0;layer<3;layer++){
            float scale=actor.flameRingScale+.1f*layer;
            if(scale>1f)scale-=.2f;
            float opacity=MathHelper.Lerp(.8f,0,Math.Abs(scale-.9f)*10);
            Main.spriteBatch.Draw(TextureAssets.FlameRing.Value,actor.Center-Main.screenPosition,
                new Rectangle(0,400*layer,400,400),new Color(opacity,opacity,opacity,opacity/2),
                actor.flameRingRot+MathHelper.Pi/3*layer,new Vector2(200),scale,SpriteEffects.None,0);
        }
    }
}
