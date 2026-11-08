using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class CompanionBuffVisuals:ModSystem {
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
