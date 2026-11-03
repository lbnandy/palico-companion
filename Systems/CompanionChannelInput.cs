using Terraria;
using Terraria.ID;

namespace PalicoCompanion.Systems;

// Input policy only. Native ItemCheck/projectile AI owns charging, firing,
// costs and recovery. A channel flag alone does not mean release-to-fire:
// beams, accelerating guns and yoyos must remain held while combat permits.
internal sealed class CompanionChannelInput {
    Projectile released;
    int identity;
    internal void Reset(){released=null;}
    internal bool Hold(CompanionNativeUse use,Item weapon,NPC target,bool requested){
        // Keep the button up through the native volley/retraction, including
        // target loss and reacquisition. Do not restart a half-finished volley.
        if(released!=null){
            if(released.active&&released.identity==identity&&CompanionOwnedShot.Of(released)==use)return false;
            Reset();
        }
        foreach(var shot in Main.projectile){
            if(!shot.active||CompanionOwnedShot.Of(shot)!=use)continue;
            bool? complete=null;
            if(weapon.channel&&shot.type==weapon.shoot&&shot.aiStyle==ProjAIStyleID.Flail)
                complete=shot.ai[0]!=0||shot.localAI[1]>=18;
            else if(weapon.channel&&shot.type==weapon.shoot&&shot.aiStyle==ProjAIStyleID.Spear){
                var tip=shot.Hitbox;tip.Inflate(8,8);
                complete=shot.ai[0]>=use.Actor.itemAnimationMax&&target!=null&&!tip.Intersects(target.Hitbox);
            }
            else complete=CompanionCrossMod.Charged(weapon,shot,use);
            if(complete.HasValue&&(!requested||complete.Value||!use.Actor.channel&&weapon.channel||weapon.useAmmo>0&&!use.Actor.HasAmmo(weapon))){
                released=shot;identity=shot.identity;return false;
            }
        }
        return requested;
    }
}
