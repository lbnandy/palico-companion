using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using CalamityMod.Projectiles.BaseProjectiles;

namespace PalicoCompanion.Systems;

// Native custom-use holdouts initialize their swing from the owner's animation
// duration. Their packet can precede the cat's buffered pose, so carry that
// duration with the holdout instead of initializing its native state from zero.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityCompanionUsePose:GlobalProjectile {
    public override bool InstancePerEntity=>true;
    int animation,duration;
    public override bool AppliesToEntity(Projectile entity,bool lateInstantiation)=>entity.ModProjectile is BaseCustomUseStyleProjectile;
    public override void SendExtraAI(Projectile shot,BitWriter bits,BinaryWriter writer){
        var use=CompanionOwnedShot.Of(shot);bits.WriteBit(use!=null);
        if(use!=null){writer.Write(use.Actor.itemAnimation);writer.Write(use.Actor.itemAnimationMax);}
    }
    public override void ReceiveExtraAI(Projectile shot,BitReader bits,BinaryReader reader){
        if(bits.ReadBit()){animation=reader.ReadInt32();duration=reader.ReadInt32();}
    }
    bool Prepare(Projectile shot){
        if(!CompanionNetwork.Client||CompanionOwnedShot.Of(shot) is not {Available:true} use)return true;
        var held=(BaseCustomUseStyleProjectile)shot.ModProjectile;
        if(use.Actor.HeldItem.type!=held.AssignedItemID)return true;
        if((held.whenSpawned||use.Actor.itemAnimationMax<=0)&&duration>0)use.Actor.itemAnimationMax=duration;
        if(held.whenSpawned&&animation>0)use.Actor.itemAnimation=animation;
        // Continue an established native hold through the visual buffer's idle
        // boundary. Server kill packets still own the actual weapon lifetime.
        if(use.Actor.channel&&use.Actor.controlUseItem){
            use.Actor.itemAnimation=System.Math.Max(1,use.Actor.itemAnimation);
        }
        return use.Actor.itemAnimationMax>0;
    }
    public override bool PreAI(Projectile shot){
        bool ready=Prepare(shot);
        if(CompanionNetwork.Client&&CompanionOwnedShot.Of(shot) is {Available:true} use&&
            use.Actor.HeldItem.type==((BaseCustomUseStyleProjectile)shot.ModProjectile).AssignedItemID&&
            use.Actor.controlUseItem&&use.Actor.itemAnimation>0)
            shot.timeLeft=System.Math.Max(2,shot.timeLeft);
        return ready;
    }
    public override bool PreDraw(Projectile shot,ref Color lightColor)=>Prepare(shot);
}
