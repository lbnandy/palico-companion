using Microsoft.Xna.Framework;
using System.IO;
using System;
using System.Collections.Generic;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using CalamityMod.Items.Weapons.Summon;
using CalamityMod.Projectiles.Summon.SmallAresArms;

namespace PalicoCompanion.Systems;

// The native mount is sized for a player torso. Keep its IK and cannon offsets,
// but attach the shoulder joints to the companion's authored torso poses.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityCompanionMount:ModSystem {
    delegate Vector2 ConnectOrig(ExoskeletonCannon cannon);
    delegate Vector2 ConnectHook(ConnectOrig orig,ExoskeletonCannon cannon);
    Hook connection;
    public override void Load()=>connection=new Hook(typeof(ExoskeletonCannon).GetProperty(nameof(ExoskeletonCannon.ConnectOffset)).GetMethod,(ConnectHook)Connect);
    public override void Unload(){connection?.Dispose();connection=null;}
    static Vector2 Connect(ConnectOrig orig,ExoskeletonCannon cannon){
        var shot=cannon.Projectile;
        if(CompanionOwnedShot.Of(shot) is not {Available:true} use)return orig(cannon);
        var cat=use.Cat;int direction=cat.direction<0?-1:1;
        int frame=cat.ModProjectile is CompanionCat companion?companion.Animation.SpriteFrame:CompanionSpriteLayout.Idle;
        var pose=CompanionSpriteLayout.CarryOffset(frame,false);pose.X*=direction;
        int side=Math.Sign(cannon.OwnerRestingOffset.X);
        var anchor=cat.Bottom+new Vector2(-6*direction+side*3,cat.gfxOffY-(shot.ai[0]<2?22:28))+pose;
        return anchor-use.Actor.Center-new Vector2(side*shot.scale*20,0);
    }
}

// Supply the selection normally made in Ares' configuration UI. Calamity
// still creates, damages, animates and synchronizes the actual cannon arms.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamitySummoningItem:GlobalItem {
    public override bool CanUseItem(Item item,Player player){
        if(item.ModItem is not AresExoskeleton||CompanionNativeUse.Current is not {} use||use.Actor!=player)return true;
        foreach(var shot in Main.ActiveProjectiles)
            if(shot.ModProjectile is ExoskeletonPanel&&CompanionOwnedShot.Of(shot)==use)return false;
        return CalamitySummoningPanel.FreeArm(use)>=0;
    }
}

[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamitySummoningPanel:GlobalProjectile {
    internal static int FreeArm(CompanionNativeUse use){
        if(use.SummonSlots+AresExoskeleton.MinionSlotsPerCannon>use.Actor.maxMinions)return -1;
        for(int index=0;index<ExoskeletonCannon.HoverOffsetTable.Length;index++){
            bool occupied=false;
            foreach(var shot in Main.ActiveProjectiles)
                if(shot.ModProjectile is ExoskeletonCannon&&shot.ai[0]==index&&CompanionOwnedShot.Of(shot)==use){occupied=true;break;}
            if(!occupied)return index;
        }
        return -1;
    }
    public override bool PreAI(Projectile shot){
        if(shot.ModProjectile is not ExoskeletonPanel panel||CompanionOwnedShot.Of(shot) is not {Available:true} use)return true;
        if(CompanionNetwork.Client)return true;
        int index=FreeArm(use);
        if(index<0){shot.Kill();return false;}
        // Keep the native crate arrival before installing its cannon arms.
        if(panel.Time<AresExoskeleton.BoxParticleLifetime)return true;
        panel.ArmIndex=index;
        panel.ArmIDToSpawn=ModContent.ProjectileType<ExoskeletonTeslaCannon>();
        return true;
    }
    public override bool PreDraw(Projectile shot,ref Color lightColor)=>
        shot.ModProjectile is not ExoskeletonPanel||CompanionOwnedShot.Of(shot)==null;
}

// Tesla's extra-AI override omits the base cannon's joint rotations. Replicas
// must receive the native pose; running the owner's IK/firing branch locally
// would create a second simulation. Rebuild the joints around the presented
// torso, not an absolute server position ahead of the cat's visual buffer.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityCompanionCannonPose:GlobalProjectile {
    public override bool AppliesToEntity(Projectile entity,bool lateInstantiation)=>entity.ModProjectile is ExoskeletonCannon;
    public override void SendExtraAI(Projectile shot,BitWriter bits,BinaryWriter writer){
        bool owned=CompanionOwnedShot.Of(shot)!=null;bits.WriteBit(owned);
        if(!owned)return;
        var limbs=((ExoskeletonCannon)shot.ModProjectile).Limbs;
        writer.Write(shot.rotation);
        writer.Write((byte)limbs.Limbs.Length);
        foreach(var limb in limbs.Limbs)writer.Write(limb.Rotation);
    }
    public override void ReceiveExtraAI(Projectile shot,BitReader bits,BinaryReader reader){
        if(!bits.ReadBit())return;
        var limbs=((ExoskeletonCannon)shot.ModProjectile).Limbs;
        shot.rotation=reader.ReadSingle();
        int count=reader.ReadByte();
        for(int i=0;i<count;i++){double rotation=reader.ReadDouble();if(i<limbs.Limbs.Length)limbs[i].Rotation=rotation;}
    }
    static void Reanchor(Projectile shot){
        if(CompanionOwnedShot.Of(shot) is not {Available:true} use)return;
        var cannon=(ExoskeletonCannon)shot.ModProjectile;
        var anchor=use.Actor.Center+cannon.ConnectOffset+new Vector2(Math.Sign(cannon.OwnerRestingOffset.X)*shot.scale*20,0);
        cannon.Limbs.UpdateConnectPoints(anchor);shot.Center=cannon.Limbs.EndPoint;
    }
    public override bool PreAI(Projectile shot){if(CompanionNetwork.Client)Reanchor(shot);return true;}
    public override bool PreDraw(Projectile shot,ref Color lightColor){Reanchor(shot);return true;}
    public override void DrawBehind(Projectile shot,int index,List<int> behindNPCsAndTiles,List<int> behindNPCs,List<int> behindProjectiles,List<int> overPlayers,List<int> overWiresUI){
        if(CompanionOwnedShot.Of(shot)==null)return;
        shot.hide=true;behindProjectiles.Add(index);
    }
}
