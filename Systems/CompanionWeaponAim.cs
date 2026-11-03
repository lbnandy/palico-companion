using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Aim the native shot; never change its velocity after firing. Only known
// ballistic profiles receive gravity correction. Cursor-controlled, homing,
// growing and held projectiles must keep their actual target point.
internal static class CompanionWeaponAim {
    // A small companion's hand may be lower than half a native projectile.
    // Reuse the healing/drum clearance rule for other supported low launches;
    // only move an obstructed origin, leaving native velocity and AI intact.
    internal static void ClearLaunchFloor(ref Vector2 position,Projectile shot){
        ClearLaunchFloor(ref position,shot.width,shot.height);
    }
    internal static void ClearLaunchFloor(ref Vector2 position,int width,int height){
        var spawn=position;var half=new Vector2(width,height)*.5f;
        for(int rise=0;rise<=height;rise++,spawn.Y--){
            if(Collision.SolidCollision(spawn-half,width,height))continue;
            position=spawn;return;
        }
    }
    internal static Vector2 SummonPoint(Projectile cat,NPC target) {
        // A summoning cursor places a creature; it is not a shot direction.
        // Spawning exactly at an enemy's center can also feed a zero vector
        // into native minion intercept/normalization code (e.g. the twins).
        foreach(var offset in new[]{new Vector2(-cat.direction*40,-48),new Vector2(cat.direction*40,-48),new Vector2(0,-72)}) {
            var point=cat.Center+offset;
            if(CompanionNavigation.Fits(point)&&CompanionCombatReach.Clear(cat.Center,point)&&
                (target==null||Vector2.DistanceSquared(point,target.Hitbox.ClosestPointInRect(point))>32*32))return point;
        }
        return cat.Center;
    }
    internal static Vector2 Point(Item item,CompanionNativeUse use,NPC target) {
        Vector2 hand=CompanionSprite.Hand(use.Cat);
        Vector2 point=CompanionCombatReach.AimPoint(item,hand,target);
        if(item.channel||item.useAmmo<=0)return point;
        int type;float speed;
        using(new CompanionNativeUse.Scope(use)) {
            if(!use.Actor.PickAmmo(item,out type,out speed,out _,out _,out _,dontConsume:true))return point;
        }
        bool arrow=type is ProjectileID.WoodenArrowFriendly or ProjectileID.FireArrow or ProjectileID.UnholyArrow or ProjectileID.HellfireArrow;
        bool straight=type is ProjectileID.Bullet or ProjectileID.MeteorShot or ProjectileID.BulletHighVelocity;
        if((!arrow&&!straight)||speed<=0||!ContentSamples.ProjectilesByType.TryGetValue(type,out var sample))return point;
        // Flight time is in projectile updates; target velocity is per game
        // tick. Extra-updating bullets must not receive excessive lead.
        float time=Vector2.Distance(hand,point)/speed;
        for(int i=0;i<8;i++) {
            float ticks=time/(sample.extraUpdates+1);
            Vector2 lead=target.velocity*Math.Min(ticks,30);
            // Long predictions become unreliable when enemies turn or jump.
            if(lead.LengthSquared()>96*96)lead=lead.SafeNormalize(Vector2.Zero)*96;
            float falling=Math.Max(0,time-14);
            point=target.Center+lead-new Vector2(0,arrow?.05f*falling*(falling+1):0);
            time=Math.Min(120,Vector2.Distance(hand,point)/speed);
        }
        return point;
    }
}

public sealed class CompanionLaunch:GlobalItem {
    public override void ModifyShootStats(Item item,Player player,ref Vector2 position,ref Vector2 velocity,ref int type,ref int damage,ref float knockback){
        if(CompanionNativeUse.Current?.Actor!=player||
            !ContentSamples.ProjectilesByType.TryGetValue(type,out var shot)||!shot.tileCollide||shot.minion||shot.sentry)return;
        // Correct only a primary shot launched near the low hand. Remote
        // placements, child projectiles and human players keep native origins.
        if(Vector2.DistanceSquared(position,player.Center)>32*32)return;
        CompanionWeaponAim.ClearLaunchFloor(ref position,shot);
    }
}
