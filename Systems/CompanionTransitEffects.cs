using Microsoft.Xna.Framework;
using Terraria;
namespace PalicoCompanion.Systems;
// Native teleport style 0 supplies both Dust 159 and its matching Item6 sound.
public static class CompanionTransitEffects {
    public static int Pulses {get;private set;}
    public static void At(Vector2 center){
        if(CompanionNetwork.Server){CompanionNetwork.Transit(center);return;}
        Main.TeleportEffect(new Rectangle((int)center.X-12,(int)center.Y-14,24,28),0,dustCountMult:.35f);
        Pulses++;
    }
    public static void Move(Projectile cat,Vector2 destination){
        At(cat.Center);cat.Center=destination;cat.velocity=Vector2.Zero;if(cat.ModProjectile is CompanionCat c)c.PresentationEpoch++;At(destination);
        if(CompanionNetwork.Server)NetMessage.SendData(Terraria.ID.MessageID.SyncProjectile,-1,-1,null,cat.whoAmI);
    }
}
