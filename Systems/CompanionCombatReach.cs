using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Tactical reach is a capability, not a damage class. Melee-damage projectiles
// may be ranged attacks, while a shortsword's stab projectile is still contact.
internal static class CompanionCombatReach {
    internal const float SearchRange=700; // stable ExampleSimpleMinion's automatic search
    internal static bool Clear(Vector2 from,Vector2 to)=>Reachability.Clear(from,to,SearchRange);
    internal static Projectile Sample(Item item)=>item.shoot>ProjectileID.None&&ContentSamples.ProjectilesByType.TryGetValue(item.shoot,out var p)?p:null;
    internal static float Range(Item item,Projectile cat=null) {
        if(item.consumable&&CompanionWeapons.SafeThrow(item))return Math.Clamp(item.shootSpeed*18,100,240);
        var shot=Sample(item);
        if(shot==null||shot.aiStyle==ProjAIStyleID.ShortSword)return CompanionWeapons.Range(item)+24;
        // Spears extend from the hand over their animation. Keep a margin for
        // the retracting part; range and desired spacing are separate decisions.
        if(shot.aiStyle==ProjAIStyleID.Spear)return item.channel?120:Math.Clamp(item.shootSpeed*item.useAnimation*.75f,64,180);
        // Vanilla chain weapons return at these distances (AI_013). This is
        // neither a gravity flail nor an unlimited traveling melee projectile.
        if(shot.aiStyle==ProjAIStyleID.Harpoon)return .85f*(shot.type switch {
            ProjectileID.ChainKnife=>Main.remixWorld?300:150,ProjectileID.BoxingGlove=>200,
            ProjectileID.GolemFist=>500,ProjectileID.ChainGuillotine=>525,_=>700});
        if(shot.aiStyle==ProjAIStyleID.Flail)return cat?.ModProjectile is CompanionCat c?c.Weapons.FlailRange:100;
        if(shot.aiStyle==ProjAIStyleID.Yoyo)return Math.Clamp(ProjectileID.Sets.YoyosMaximumRange[shot.type]*0.85f,80,SearchRange);
        if(ProjectileID.Sets.IsAWhip[shot.type])return 220;
        // These are the sword's swing arc, not traveling sword beams.
        if(shot.aiStyle==ProjAIStyleID.LightsBane)return CompanionWeapons.Range(item)+24;
        if(shot.aiStyle==ProjAIStyleID.NightsEdge&&shot.type!=ProjectileID.TerraBlade2)return 100;
        if(shot.aiStyle==ProjAIStyleID.SleepyOctopod||shot.aiStyle==ProjAIStyleID.ForwardStab)return 80; // Monk staff swing/thrust, native collision arc
        if(shot.aiStyle==ProjAIStyleID.HeldProjectile&&item.CountsAsClass(DamageClass.Melee))
            return shot.type is ProjectileID.Arkhalis or ProjectileID.Terragrim or ProjectileID.PiercingStarlight?80:260;
        if(shot.aiStyle==ProjAIStyleID.Boomerang)return item.type==ItemID.PaladinsHammer?150:Math.Clamp(item.shootSpeed*24,120,600);
        if(item.type==ItemID.IceSickle)return 160; // AI_018 slows this disc to a stop
        if(shot.aiStyle==ProjAIStyleID.MoveShort&&!CompanionWeaponPolicy.Rain(item))return 180; // fading/curved spore projectile
        if(shot.aiStyle==ProjAIStyleID.ShadowFlame)return 300; // curling tentacles, not straight bullets
        if(shot.aiStyle==ProjAIStyleID.Vilethorn)return Math.Clamp(item.shootSpeed*7,100,350); // finite chain of thorn segments
        if(shot.aiStyle==ProjAIStyleID.BloodThorn)return 400; // ground-spawned tears
        if(!item.noMelee&&shot.aiStyle==ProjAIStyleID.SuperStarBeam)return CompanionWeapons.Range(item)+24; // auxiliary spores on a sword swing
        return SearchRange;
    }
    internal static bool Distance(Item item) {
        var shot=Sample(item);
        // A short-range yoyo is still a distance weapon. A numeric threshold
        // previously made the wooden yoyo walk into the enemy's body.
        return shot?.aiStyle is ProjAIStyleID.Yoyo or ProjAIStyleID.Spear or ProjAIStyleID.Harpoon or ProjAIStyleID.Flail or ProjAIStyleID.SleepyOctopod or ProjAIStyleID.ForwardStab||
            item.consumable&&CompanionWeapons.SafeThrow(item)||Range(item)>120||CompanionNativeUse.Summoning(item);
    }
    internal static float EffectiveRange(Item item,Projectile cat,NPC target=null) {
        float range=Range(item,cat)*(cat.ModProjectile is CompanionCat c?c.Weapons.ReachFactor:1);
        // FindSharpTearsSpot falls back to the caster's vicinity when there is
        // no terrain near the cursor. Use that short native thorn, not a remote
        // ground-spawn assumption, against an enemy high in open air.
        if(target!=null&&Sample(item)?.aiStyle==ProjAIStyleID.BloodThorn&&!TearSurface(target.Center))range=Math.Min(range,96);
        return range;
    }
    static bool TearSurface(Vector2 at) {
        var tile=at.ToTileCoordinates();
        for(int x=tile.X-6;x<=tile.X+6;x++)for(int y=tile.Y-12;y<=tile.Y+12;y++)
            if(WorldGen.InWorld(x,y,40)&&Vector2.DistanceSquared(new Vector2(x*16+8,y*16+8),at)<180*180&&WorldGen.SolidTile2(x,y))return true;
        return false;
    }
    // Approach a usable surface, never a point inside the enemy. The gap is
    // measured between bodies; weapon reach is measured from the native hand.
    // TerraGuardians' close-range behavior likewise accounts for both bodies
    // and keeps an approach/retreat band rather than chasing Target.Center.
    internal static Vector2 Stand(Item item,Projectile cat,NPC target,bool cautious=false) {
        float edge=Vector2.Distance(cat.Center,target.Hitbox.ClosestPointInRect(cat.Center));
        float range=EffectiveRange(item,cat,target);
        float gap=Distance(item)?Math.Clamp(range*.5f,24,100):Math.Clamp(CompanionWeapons.Range(item)*.3f,8,18);
        // A held lance damages with its extended tip; walking inside that tip
        // cannot be repaired by waiting for another hit cooldown.
        if(item.channel&&Sample(item)?.aiStyle==ProjAIStyleID.Spear)gap=range*.7f;
        if(cautious){gap=Math.Max(gap,80);if(edge>=gap+cat.width/2f)return cat.Center;}
        if(Distance(item)&&edge>=gap+cat.width/2f&&CanUse(item,cat,target))return cat.Center;
        float side=cat.Center.X<target.Center.X?-1:1;
        // Horizontal approach lets even a shortsword reach from its hand anchor;
        // the nearest vertical point avoids chasing the middle of a tall boss.
        float y=Math.Clamp(cat.Center.Y,target.Top.Y+Math.Min(8,target.height/2f),target.Bottom.Y-Math.Min(8,target.height/2f));
        if(Distance(item)&&edge>range*.8f)gap=range*.65f;
        return new Vector2((side<0?target.Left.X:target.Right.X)+side*(cat.width/2f+gap),y);
    }
    internal static Vector2 AimPoint(Item item,Vector2 hand,NPC target) {
        if(Distance(item))return target.Center;
        var surface=target.Hitbox.ClosestPointInRect(hand);
        return surface+(target.Center-surface).SafeNormalize(Vector2.UnitX)*4;
    }
    internal static bool CanUse(Item item,Projectile cat,NPC target)=>target!=null&&
        Microsoft.Xna.Framework.Vector2.DistanceSquared(CompanionSprite.Hand(cat),target.Hitbox.ClosestPointInRect(CompanionSprite.Hand(cat)))<=MathF.Pow(EffectiveRange(item,cat,target),2);
}
