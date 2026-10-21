using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Held projectiles update their owner's arm after the companion's own AI.
// Publish the completed frame, never a mixture of pre/post-projectile poses.
public sealed class CompanionPresentationSync:ModSystem {
    public override void PostUpdateProjectiles(){
        if(!CompanionNetwork.Server)return;
        foreach(var p in Main.projectile){
            if(!p.active||p.ModProjectile is not CompanionCat cat)continue;
            if(cat.PendingPresentation){
                cat.PendingPresentation=false;
                NetMessage.SendData(MessageID.SyncProjectile,-1,-1,null,p.whoAmI);
            }
        }
    }
}
