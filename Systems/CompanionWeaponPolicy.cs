using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
namespace PalicoCompanion.Systems;

// Usage decisions only: Player.ItemCheck still owns every projectile, payment,
// animation and native projectile limit. Inspired by TG WeaponProfile.
internal static class CompanionWeaponPolicy {
    internal static bool Rain(Item item)=>item.type is ItemID.CrimsonRod or ItemID.NimbusRod;
    internal static bool Sentry(Item item)=>item.sentry||CompanionCombatReach.Sample(item)?.sentry==true;
    internal static bool Plan(Item item,CompanionNativeUse use,NPC target,ref Vector2 aim) {
        if(target==null)return true;
        if(item.type==ItemID.ChargedBlasterCannon){
            // Keep native small/large energy shots, then release before the
            // beam starts at 180. AI_075 handles its own normal cancellation.
            foreach(var p in Main.projectile)if(p.active&&p.type==ProjectileID.ChargedBlasterCannon&&CompanionOwnedShot.Of(p)==use&&p.ai[0]>=170)return false;
        }
        if(item.type==ItemID.MagnetSphere){
            // Each recast shortens the previous sphere's remaining life to 30
            // ticks. Let a useful sphere reach the target and fire normally.
            foreach(var p in Main.projectile)if(p.active&&p.type==item.shoot&&p.timeLeft>30&&CompanionOwnedShot.Of(p)==use){
                var delta=target.Center-p.Center;
                bool inRange=Math.Abs(delta.X)+Math.Abs(delta.Y)<300;
                bool approaching=delta.LengthSquared()<CompanionCombatReach.SearchRange*CompanionCombatReach.SearchRange&&Vector2.Dot(delta,p.velocity)>0;
                if((inRange||approaching)&&CompanionCombatReach.Clear(p.Center,target.Center))return false;
            }
        }
        if(item.channel&&CompanionCombatReach.Sample(item)?.aiStyle==ProjAIStyleID.Spear) {
            foreach(var p in Main.projectile)if(p.active&&p.aiStyle==ProjAIStyleID.Spear&&CompanionOwnedShot.Of(p)==use&&p.ai[0]>=use.Actor.itemAnimationMax) {
                // Native held lances keep the direction of their initial stab.
                // Let the normal release/retraction finish before aiming again
                // when the enemy has moved away from the tip.
                var tip=p.Hitbox;tip.Inflate(8,8);
                if(!tip.Intersects(target.Hitbox))return false;
            }
        }
        if(Sentry(item)){
            // Some mod weapons mark only their projectile as a sentry. Treat
            // these as persistent placements too, not an ordinary repeated
            // shot which keeps killing the old turret and resetting its AI.
            // Native ItemCheck still places/pays, and UpdateMaxTurrets replaces
            // the oldest turret when the fight moves beyond our combat area.
            int useful=0;
            foreach(var p in Main.projectile)if(p.active&&p.sentry&&CompanionOwnedShot.Of(p)==use&&
                Vector2.DistanceSquared(p.Center,target.Hitbox.ClosestPointInRect(p.Center))<=
                CompanionCombatReach.SearchRange*CompanionCombatReach.SearchRange)useful++;
            if(useful>=use.Actor.maxTurrets)return false;
            // Spread placements. DD2's native CanUse rejects overlapping turrets.
            // ItemCheck/Shoot still decides whether the item rests or floats.
            float side=use.Cat.Center.X<target.Center.X?-1:1;
            // Native grounded sentries scan down to terrain. Avoid paying for
            // one hundreds of tiles below an airborne fight.
            bool nativeGround=item.type is ItemID.QueenSpiderStaff or ItemID.StaffoftheFrostHydra or ItemID.HoundiusShootius||
                item.shoot>ProjectileID.None&&ProjectileID.Sets.IsADD2Turret[item.shoot];
            for(int candidate=0;candidate<6;candidate++){
                float offset=(64+(useful+candidate/2)*96)*side*(candidate%2==0?1:-1);
                var point=target.Center+new Vector2(offset,-16);
                if(!WorldGen.InWorld((int)point.X/16,(int)point.Y/16,10)||
                    Collision.SolidCollision(point-new Vector2(8),16,16)||!CompanionCombatReach.Clear(use.Cat.Center,point))continue;
                if(nativeGround){
                    Vector2 previous=use.Aim;
                    use.PlacementAim(point);
                    int x,y;
                    try{using(new CompanionNativeUse.Scope(use))use.Actor.FindSentryRestingSpot(item.shoot,out x,out y,out _);}
                    finally{use.PlacementAim(previous);}
                    if(Vector2.DistanceSquared(new Vector2(x,y),target.Hitbox.ClosestPointInRect(new Vector2(x,y)))>
                        CompanionCombatReach.SearchRange*CompanionCombatReach.SearchRange)continue;
                    if(ProjectileID.Sets.IsADD2Turret[item.shoot]&&Terraria.GameContent.Events.DD2Event.Ongoing&&
                        Player.WouldSpotOverlapWithSentry(x,y,item.shoot is ProjectileID.DD2LightningAuraT1 or ProjectileID.DD2LightningAuraT2 or ProjectileID.DD2LightningAuraT3))continue;
                }
                aim=point;return true;
            }
            return false;
        }
        if(Rain(item)) {
            bool blood=item.type==ItemID.CrimsonRod;
            int moving=blood?ProjectileID.BloodCloudMoving:ProjectileID.RainCloudMoving;
            int cloud=blood?ProjectileID.BloodCloudRaining:ProjectileID.RainCloudRaining;
            int useful=0;bool inFlight=false;
            foreach(var p in Main.projectile)if(p.active&&CompanionOwnedShot.Of(p)==use) {
                if(p.type==moving)inFlight=true;
                if(p.type==cloud&&p.ai[1]<18000&&p.Bottom.Y<target.Bottom.Y&&target.Top.Y-p.Bottom.Y<480&&
                    p.Center.X>=target.Left.X-24&&p.Center.X<=target.Right.X+24&&CompanionCombatReach.Clear(p.Bottom,target.Center))useful++;
            }
            // Don't repeatedly replace a cloud that is still doing its job, or
            // queue more casts while the native moving cloud is on its way.
            if(inFlight||useful>=(blood?1:2))return false;
            float x=target.Center.X+Math.Clamp(target.velocity.X*12,-64,64);
            if(!blood)x+=(useful==0?-1:1)*Math.Min(16,target.width*.25f);
            for(int rise=96;rise>=32;rise-=16) {
                var candidate=new Vector2(x,target.Top.Y-rise);
                if(!WorldGen.InWorld((int)candidate.X/16,(int)candidate.Y/16,10)||
                    Collision.SolidCollision(candidate-new Vector2(20,12),40,24)||
                    !CompanionCombatReach.Clear(use.Actor.Center,candidate)||!CompanionCombatReach.Clear(candidate,target.Center))continue;
                aim=candidate;return true;
            }
            return false;
        }
        if(CompanionCombatReach.Sample(item)?.aiStyle==ProjAIStyleID.Flail) {
            foreach(var p in Main.projectile)if(p.active&&CompanionOwnedShot.Of(p)==use&&p.aiStyle==ProjAIStyleID.Flail)
                return p.ai[0]==0&&p.localAI[1]<18;
        }
        return true;
    }
}
