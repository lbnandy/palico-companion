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
    static CompanionBallisticProfile? Flight(Projectile sample,Player actor)=>sample.type switch {
        ProjectileID.WoodenArrowFriendly or ProjectileID.FireArrow or ProjectileID.UnholyArrow or ProjectileID.HellfireArrow=>new(.1f,14),
        ProjectileID.ThrowingKnife or ProjectileID.PoisonedKnife or ProjectileID.Shuriken=>new(.4f,19,.97f),
        ProjectileID.Meowmere=>new(.2f,19),
        _=>CompanionCrossMod.Flight(sample,actor)
    };
    internal static bool TryFlight(Item item,CompanionNativeUse use,out Projectile sample,out float speed,out CompanionBallisticProfile profile){
        sample=null;speed=0;profile=default;
        if(item.channel||use?.AttributesReady!=true||use.Cat==null||!use.PreviewShot(item,out int type,out speed)||speed<=0||
            !ContentSamples.ProjectilesByType.TryGetValue(type,out sample)||Flight(sample,use.Actor) is not {} flight)return false;
        profile=flight;return true;
    }
    static Vector2 Launch(CompanionNativeUse use,Vector2 center,NPC target,Projectile sample){
        var hand=CompanionSprite.Hand(use.Cat)+center-use.Cat.Center;
        if((target.Center.X<center.X?-1:1)!=(use.Cat.direction<0?-1:1))hand.X=2*center.X-hand.X;
        if(sample.tileCollide)ClearLaunchFloor(ref hand,sample);
        return hand;
    }
    internal static bool CanReach(Item item,CompanionNativeUse use,Vector2 center,NPC target)=>
        !TryFlight(item,use,out var sample,out var speed,out var flight)||
        CompanionBallistics.TryAim(Launch(use,center,target,sample),target,speed,sample,flight,out _);
    internal static Vector2 Point(Item item,CompanionNativeUse use,NPC target) {
        Vector2 hand=CompanionSprite.Hand(use.Cat);
        Vector2 point=CompanionCombatReach.AimPoint(item,hand,target);
        if(item.channel)return point;
        if(!use.PreviewShot(item,out int type,out float speed))return point;
        bool straight=type is ProjectileID.Bullet or ProjectileID.MeteorShot or ProjectileID.BulletHighVelocity;
        if(speed<=0||!ContentSamples.ProjectilesByType.TryGetValue(type,out var sample))return point;
        CompanionBallisticProfile? flight=Flight(sample,use.Actor);
        if(flight is {} profile){
            hand=Launch(use,use.Cat.Center,target,sample);
            return CompanionBallistics.TryAim(hand,target,speed,sample,profile,out var aim)?aim:point;
        }
        if(!straight)return point;
        // Flight time is in projectile updates; target velocity is per game
        // tick. Extra-updating bullets must not receive excessive lead.
        float time=Vector2.Distance(hand,point)/speed;
        for(int i=0;i<8;i++) {
            point=target.Center+CompanionBallistics.Lead(target,time,sample.extraUpdates);
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
