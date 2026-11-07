using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;
// Keep vanilla teleport style 0's particles and sound independently selectable.
public static class CompanionTransitEffects {
    public static void At(Vector2 center){
        if(CompanionNetwork.Server){CompanionNetwork.Transit(center);return;}
        var area=new Rectangle((int)center.X-12,(int)center.Y-14,24,28);
        if(ModContent.GetInstance<CompanionConfig>().TeleportSounds)SoundEngine.PlaySound(SoundID.Item6,area.Center.ToVector2());
        // Main.TeleportEffect has no silent option. Use its style 0 particle
        // recipe here rather than intercepting sound or changing global volume.
        int count=(int)(area.Width*area.Height/5*.35f);
        for(int i=0;i<count;i++){
            var dust=Dust.NewDustDirect(area.TopLeft(),area.Width,area.Height,159);
            dust.scale=Main.rand.Next(20,70)*.01f;
            if(i<10)dust.scale+=.25f;
            if(i<5)dust.scale+=.25f;
        }
    }
    public static void Move(Projectile cat,Vector2 destination){
        At(cat.Center);cat.Center=destination;cat.velocity=Vector2.Zero;if(cat.ModProjectile is CompanionCat c)c.PresentationEpoch++;At(destination);
        if(CompanionNetwork.Server)NetMessage.SendData(Terraria.ID.MessageID.SyncProjectile,-1,-1,null,cat.whoAmI);
    }
}
