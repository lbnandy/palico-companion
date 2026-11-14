using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Acquisition and firing must agree on obstruction. This grants permission to
// try the native attack, never changes its collision, placement or hit rules.
internal static class CompanionAttackPath {
    internal static Query For(Player owner){
        var state=owner.GetModPlayer<CompanionPlayer>();
        return new(CompanionWeapons.Active(state.Gear),(state.Cat?.ModProjectile as CompanionCat)?.Weapons);
    }
    internal static bool Clear(Item item,CompanionNativeUse use,Vector2 from,NPC target)=>
        new Query(item,use).Clear(from,target);
    // Resolve the actual ammunition once per target scan, not once per NPC.
    internal readonly struct Query {
        readonly Item item;
        readonly CompanionNativeUse use;
        readonly Projectile shot;
        readonly bool throughWalls;
        readonly Fall fall;
        internal Query(Item item,CompanionNativeUse use){
            this.item=item;this.use=use;
            int type=item.shoot;
            if(item.useAmmo>0&&(use==null||!use.PreviewShot(item,out type,out _)))type=ProjectileID.None;
            shot=ContentSamples.ProjectilesByType.TryGetValue(type,out var sample)?sample:null;
            throughWalls=ThroughWalls(item,shot);
            fall=(item.type,shot?.type) switch {
                (ItemID.StarWrath,ProjectileID.StarWrath)=>Fall.Wrath,
                (ItemID.Starfury,ProjectileID.Starfury)=>Fall.Starfury,
                (ItemID.BlizzardStaff,ProjectileID.Blizzard)=>Fall.Blizzard,
                (ItemID.MeteorStaff,ProjectileID.Meteor1)=>Fall.Meteor,
                (ItemID.LunarFlareBook,ProjectileID.LunarFlare)=>Fall.Lunar,
                _=>Fall.None
            };
        }
        internal bool Clear(Vector2 from,NPC target){
            Vector2 to=target.Center;
            // These ItemCheck branches spawn above the cursor/caster. AI_005
            // enables collision later; neither the initial flag nor a ray
            // straight from the cat describes the damaging part of the flight.
            if(fall!=Fall.None){
                Vector2 caster=use?.Cat!=null?CompanionSprite.Hand(use.Cat)+from-use.Cat.Center:from;
                // Turning toward a newly selected target happens after planning.
                // Evaluate the hand on that side, not the previous idle facing.
                if(use?.Cat!=null&&(to.X<from.X?-1:1)!=(use.Cat.direction<0?-1:1))caster.X=2*from.X-caster.X;
                // As with CanUse, measure contact from the hand to the enemy's
                // body, not its center; large or nearby enemies still meet the blade.
                bool contact=!item.noMelee&&Vector2.DistanceSquared(caster,target.Hitbox.ClosestPointInRect(caster))<=MathF.Pow(CompanionWeapons.Range(item),2)&&CompanionCombatReach.Clear(from,to);
                if(contact)return true;
                return SkyPath(fall,shot,caster,to,use?.Actor.height??42);
            }
            return throughWalls||CompanionCombatReach.Clear(from,to);
        }
    }
    static bool ThroughWalls(Item item,Projectile shot){
        if(item.IsAir||item.sentry||CompanionWeapons.Support(item)||shot==null||
            !shot.friendly||shot.hostile||shot.ownerHitCheck||shot.minion||shot.sentry||shot.hide||
            ProjectileID.Sets.IsAWhip[shot.type])return false;
        // A held weapon/controller being intangible says nothing about its
        // damaging shots. Spears and short slashes still require an open path.
        if(shot.aiStyle is ProjAIStyleID.HeldProjectile or ProjAIStyleID.Spear or ProjAIStyleID.ShortSword or
            ProjAIStyleID.Flail or ProjAIStyleID.Yoyo or ProjAIStyleID.Harpoon or ProjAIStyleID.NightsEdge or
            ProjAIStyleID.TrueNightsEdge or ProjAIStyleID.LightsBane or ProjAIStyleID.SleepyOctopod or ProjAIStyleID.ForwardStab or
            ProjAIStyleID.ThickLaser or ProjAIStyleID.FallingStar)return false;
        // Native thorn chains grow segments without integrating position, so
        // tileCollide is not their travel rule (Projectile.UpdatePosition).
        return !shot.tileCollide||shot.aiStyle==ProjAIStyleID.Vilethorn;
    }
    enum Fall {None,Wrath,Starfury,Blizzard,Meteor,Lunar}
    static bool SkyPath(Fall kind,Projectile shot,Vector2 caster,Vector2 target,int casterHeight){
        if(!float.IsFinite(target.X)||!float.IsFinite(target.Y)||!float.IsFinite(caster.X)||!float.IsFinite(caster.Y)||
            Vector2.DistanceSquared(caster,target)>CompanionCombatReach.TrackingRange*CompanionCombatReach.TrackingRange)return false;
        // Lunar Flare only enables collision after its top passes the cursor.
        // The target can therefore be hit before intervening tiles stop it.
        if(kind==Fall.Lunar)return true;
        bool wrath=kind==Fall.Wrath,weather=kind is Fall.Blizzard or Fall.Meteor;
        // Starfury starts colliding when its bottom reaches the cursor height.
        // Star Wrath starts when its center passes min(cursor Y, caster Y-200).
        // Sample the native spread without consuming RNG or invoking Shoot.
        float collisionY=wrath?Math.Min(target.Y,caster.Y-200):weather?
            caster.Y-casterHeight*.5f-300+shot.height*.5f:target.Y-shot.height*.5f;
        int side=target.X<caster.X?-1:1;
        for(int volley=0;volley<(wrath?3:kind==Fall.Blizzard?2:1);volley++)for(int spread=0;spread<3;spread++){
            Vector2 spawn=wrath?caster+new Vector2(-spread*200*side,-600-100*volley):
                new Vector2(weather?(target.X+caster.X)*.5f-50*side+(spread-1)*250:
                    target.X+(spread-1)*100,caster.Y-600-100*volley);
            if(spawn.Y>=target.Y)continue;
            // Blizzard/Meteor also collide immediately above the surface.
            float start=weather&&spawn.Y-shot.height*.5f<Main.worldSurface*16?0:
                Math.Clamp((collisionY-spawn.Y)/(target.Y-spawn.Y),0,1);
            if(ClearDescent(Vector2.Lerp(spawn,target,start),target,shot.width,shot.height))return true;
        }
        return false;
    }
    static bool ClearDescent(Vector2 from,Vector2 to,int width,int height){
        int steps=Math.Max(1,(int)Math.Ceiling(Vector2.Distance(from,to)/4));
        var half=new Vector2(width,height)*.5f;
        for(int i=0;i<=steps;i++){
            Vector2 point=Vector2.Lerp(from,to,i/(float)steps);
            if(!WorldGen.InWorld((int)(point.X/16),(int)(point.Y/16),2)||Collision.SolidCollision(point-half,width,height))return false;
        }
        return true;
    }
}
