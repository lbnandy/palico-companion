using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Tactical reach is a capability, not a damage class. Melee-damage projectiles
// may be ranged attacks, while a shortsword's stab projectile is still contact.
internal static class CompanionCombatReach {
    // World-space limits are shared by authority and replicas, not a viewport.
    // Acquisition and retention are separate, as in vanilla minion targeting.
    // Read the synchronized config live; changing range must not require a
    // respawn or alter native projectile lifetime / finite weapon reach.
    internal static float SearchRange=>1200*CompanionBalanceConfig.Current.TargetingRangeScale;
    internal static float TrackingRange=>1600*CompanionBalanceConfig.Current.TargetingRangeScale;
    internal static float RangedRange=>TrackingRange;
    // Native attack visibility treats hammered solid tiles as obstructions too.
    // Keep the bounded hand ray: CanHitLine also tests neighboring floor cells.
    internal static bool Clear(Vector2 from,Vector2 to)=>Reachability.Clear(from,to,TrackingRange,includeSlopes:true);
    internal static Projectile Sample(Item item)=>item.shoot>ProjectileID.None&&ContentSamples.ProjectilesByType.TryGetValue(item.shoot,out var p)?p:null;
    internal static float Range(Item item,Projectile cat=null) {
        if(CompanionCrossMod.Reach(item,(cat?.ModProjectile as CompanionCat)?.Weapons.Actor) is float reach)return reach;
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
        if(shot.aiStyle==ProjAIStyleID.Yoyo)return Math.Clamp(ProjectileID.Sets.YoyosMaximumRange[shot.type]*0.85f,80,700);
        // FillWhipControlPoints uses base animation, launch speed and the
        // projectile's native range multiplier. Reserve the last part of the
        // extension for moving targets rather than standing at the tip.
        if(ProjectileID.Sets.IsAWhip[shot.type])return item.shootSpeed*ContentSamples.ItemsByType[item.type].useAnimation*
            shot.WhipSettings.RangeMultiplier*(cat?.ModProjectile is CompanionCat whipCat?whipCat.Weapons.Actor.whipRangeMultiplier:1);
        // These are the sword's swing arc, not traveling sword beams.
        if(shot.aiStyle==ProjAIStyleID.LightsBane)return CompanionWeapons.Range(item)+24;
        if(shot.aiStyle==ProjAIStyleID.NightsEdge&&shot.type!=ProjectileID.TerraBlade2)return 100;
        if(shot.aiStyle==ProjAIStyleID.SleepyOctopod||shot.aiStyle==ProjAIStyleID.ForwardStab)return 80; // Monk staff swing/thrust, native collision arc
        if(shot.aiStyle==ProjAIStyleID.HeldProjectile&&item.CountsAsClass(DamageClass.Melee)) {
            // Starlight's native collision sweeps five projectile widths from
            // the hand. Leave one width of margin; do not use its shorter light
            // effect as weapon reach. ItemCheck supplies item scale to the shot.
            if(shot.type==ProjectileID.PiercingStarlight)return shot.width*4*item.scale;
            // These short slashes use a box centered on the native hand, not
            // Starlight's forward sweep. Width is a diameter, not reach.
            return shot.type is ProjectileID.Arkhalis or ProjectileID.Terragrim?shot.width*.5f:260;
        }
        if(shot.aiStyle==ProjAIStyleID.Boomerang)return item.type==ItemID.PaladinsHammer?150:Math.Clamp(item.shootSpeed*24,120,600);
        if(item.type==ItemID.IceSickle)return 160; // AI_018 slows this disc to a stop
        if(shot.aiStyle==ProjAIStyleID.MoveShort&&!CompanionWeaponPolicy.Rain(item))return 180; // fading/curved spore projectile
        if(shot.aiStyle==ProjAIStyleID.ShadowFlame)return 300; // curling tentacles, not straight bullets
        if(shot.aiStyle==ProjAIStyleID.Vilethorn)return Math.Clamp(item.shootSpeed*7,100,350); // finite chain of thorn segments
        if(shot.aiStyle==ProjAIStyleID.BloodThorn)return 400; // ground-spawned tears
        if(!item.noMelee&&shot.aiStyle==ProjAIStyleID.SuperStarBeam)return CompanionWeapons.Range(item)+24; // auxiliary spores on a sword swing
        return RangedRange;
    }
    internal static bool Distance(Item item) {
        var shot=Sample(item);
        // Attached swings and thrusts still have reach even below 120 pixels.
        // Classify their native attack shape before the numeric fallback, also
        // covering mod weapons which reuse these projectile behaviors.
        return shot?.aiStyle is ProjAIStyleID.Yoyo or ProjAIStyleID.Spear or ProjAIStyleID.Harpoon or ProjAIStyleID.Flail or ProjAIStyleID.SleepyOctopod or ProjAIStyleID.ForwardStab||
            shot!=null&&item.CountsAsClass(DamageClass.Melee)&&shot.aiStyle is (ProjAIStyleID.HeldProjectile or ProjAIStyleID.LightsBane or ProjAIStyleID.NightsEdge)||
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
        var owner=Main.player[CompanionCat.OwnerOf(cat)];
        var anchor=CompanionStation.Origin(owner);
        var state=owner.GetModPlayer<CompanionPlayer>();
        var companion=cat.ModProjectile as CompanionCat;
        if(companion!=null)companion.tactics.Approaching=companion.tactics.FollowingOwner=false;
        Vector2 follow=CompanionNavigation.FollowGoal(owner,cat.Center);
        bool distance=Distance(item),close=CloseCombat(item);
        bool standOff=Sample(item)?.aiStyle is ProjAIStyleID.Yoyo or ProjAIStyleID.Harpoon or ProjAIStyleID.Flail or ProjAIStyleID.Boomerang;
        bool followsOwner=companion!=null&&!state.Stationed&&!CompanionWeapons.SummonWeapon(item)&&
            !close&&!standOff&&distance&&Range(item,cat)>=RangedRange&&
            CompanionWeaponAim.CanReach(item,companion.Weapons,companion.Navigation.Riding?CompanionNavigation.FlightFollowGoal(owner):follow,target);
        bool summoning=CompanionWeapons.SummonWeapon(item)||state.Gear[0].IsAir&&CompanionWeapons.SummonWeapon(CompanionWeapons.Summon(state.Gear));
        // Seeing a distant enemy does not require leaving the owner to reach it.
        // Reuse return/approach selection; targeting, evasion and station limits stay separate.
        bool limitApproach=!state.Stationed&&!summoning&&NeedsApproach(item,cat);
        bool InApproachRange(Vector2 point)=>PursuitLocal(item,cat,point,anchor)&&
            (!limitApproach||Vector2.DistanceSquared(point,anchor)<=800*800); // 50 tiles
        bool flightApproach=companion?.Navigation.Riding==true&&!followsOwner;
        // Ordinary ranged combat already follows safely through its normal
        // branch below. A second return state bypasses that branch's routing
        // and recovery. Clear it here, including after a weapon-mode change.
        if(followsOwner)companion.tactics.EndReturn();
        bool outside=!InApproachRange(cat.Center);
        // Close and finite-reach attacks use the existing pursuit boundary.
        // Owner starts and stops do not override their combat position.
        bool leaving=!NeedsApproach(item,cat)&&!state.Stationed&&Vector2.Dot(owner.velocity,anchor-cat.Center)>1;
        bool returning=!followsOwner&&(companion?.tactics.ReturnToOwner(Vector2.Distance(cat.Center,anchor),outside,leaving)??outside);
        if(returning)return SafeGoal(cat,owner,follow);
        if(!flightApproach&&!CompanionNavigation.Fits(cat.Center)||CompanionIncomingDamage.HazardAt(cat,cat.Center))return SafeGoal(cat,owner,follow);
        // Summons do their own pursuit. An empty primary slot retains the
        // existing defensive basic sword without sending its summoner after foes.
        if(summoning)
            return Vector2.DistanceSquared(cat.Center,follow)<=96*96?cat.Center:SafeGoal(cat,owner,follow);
        float edge=Vector2.Distance(cat.Center,target.Hitbox.ClosestPointInRect(cat.Center));
        float range=EffectiveRange(item,cat,target);
        // Beam swords can shoot at range without giving up their contact swing.
        float spacing=SpacingRange(item,cat,target);
        float gap=distance?Math.Clamp(spacing*.5f,24,240):Math.Clamp(CompanionWeapons.Range(item)*.3f,8,18);
        // A usable approach distance is not a minimum safe distance. Sword
        // beams and wide/custom slashes may already hit from close by; do not
        // backpedal merely because their attack can also reach farther away.
        // Keep native stand-off weapons (yoyos, chains and returning throws)
        // on their existing spacing, independently of their melee damage class.
        float minimumGap=close?Math.Clamp(CompanionWeapons.Range(item)*.15f,4,8):standOff?gap:Math.Min(gap,32);
        // Imminent contact and moving shots already belong to CompanionEvasion;
        // longer projectile reach alone is not a reason to retreat.
        // A held lance damages with its extended tip; walking inside that tip
        // cannot be repaired by waiting for another hit cooldown.
        if(item.channel&&Sample(item)?.aiStyle==ProjAIStyleID.Spear)minimumGap=gap=range*.7f;
        // Caution changes spacing inside usable reach, never beyond it.
        if(cautious){
            float caution=Math.Min(80,Math.Max(8,spacing*.5f-cat.width/2f));
            gap=Math.Max(gap,caution);minimumGap=Math.Max(minimumGap,caution);
        }
        float upper=close||standOff?Math.Max(gap+24,Math.Min(spacing*.72f,360)):float.MaxValue;
        if(Safe(cat,cat.Center,flightApproach)&&CanUse(item,cat,target)&&
            CompanionAttackPath.Clear(item,companion?.Weapons,cat.Center,target)){
            // Ordinary long-range attacks follow from their normal position.
            // SafeGoal rejects occupied destinations; Evasion can wait safely
            // instead of crossing a threat. Finite/melee spacing stays separate.
            if(followsOwner){
                var movingGoal=SafeGoal(cat,owner,companion.Navigation.Riding?CompanionNavigation.FlightFollowGoal(owner):follow);
                if(movingGoal==cat.Center||CompanionAttackPath.Clear(item,companion.Weapons,movingGoal,target)){
                    companion.tactics.FollowingOwner=true;
                    return movingGoal;
                }
            }
            // Contact weapons approach their stand point even when cautious;
            // caution changes that spacing, not whether a short attack can reach.
            if(distance&&edge>=minimumGap+cat.width/2f&&edge<=upper)return cat.Center;
        }
        // If contact really is too close, separate only enough to clear it.
        // The wider approach point still applies when closing on a distant foe.
        if(edge<minimumGap+cat.width/2f)gap=close||standOff?minimumGap:Math.Min(gap,minimumGap+16);
        float side=cat.Center.X<target.Center.X?-1:1;
        // Horizontal approach lets even a shortsword reach from its hand anchor;
        // the nearest vertical point avoids chasing the middle of a tall boss.
        float y=Math.Clamp(cat.Center.Y,target.Top.Y+Math.Min(8,target.height/2f),target.Bottom.Y-Math.Min(8,target.height/2f));
        var point=new Vector2((side<0?target.Left.X:target.Right.X)+side*(cat.width/2f+gap),y);
        // A low enemy's body can put our taller body's feet inside the floor.
        // Resolve that nearby surface before rejecting the approach. Keep this
        // local to grounded combat; return flight and work goals stay unchanged.
        if(companion!=null&&!companion.Navigation.Riding&&companion.Navigation.Grounded(cat)&&!CompanionNavigation.Fits(point)){
            for(int up=2;up<=16;up+=2){
                if(!CompanionNavigation.TryStand(point-new Vector2(0,up),out var stand))continue;
                point=stand;break;
            }
        }
        // Use the same leash for selecting and maintaining this approach.
        if(!InApproachRange(point))return SafeGoal(cat,owner,follow);
        // Combat flight uses the original pet-style approach through terrain.
        // Ground movement, owner following and damaging hazards keep their checks.
        var goal=flightApproach&&Safe(cat,point,true)?point:SafeGoal(cat,owner,point);
        if(companion!=null)companion.tactics.Approaching=goal==point&&edge>=minimumGap+cat.width/2f;
        return goal;
    }
    static bool Safe(Projectile cat,Vector2 point,bool flightApproach=false)=>(flightApproach||CompanionNavigation.Fits(point))&&
        !CompanionIncomingDamage.HazardAt(cat,point)&&!CompanionIncomingDamage.ContactAt(cat,point);
    // Reuse body clearance and native damage geometry. Prefer a short escape
    // when both the combat point and the owner's feet are unsafe.
    internal static Vector2 SafeGoal(Projectile cat,Player owner,Vector2 preferred){
        var state=owner.GetModPlayer<CompanionPlayer>();
        preferred=CompanionStation.Limit(state,preferred);
        if(Safe(cat,preferred))return preferred;
        if(Safe(cat,cat.Center))return cat.Center;
        bool flying=(cat.ModProjectile as CompanionCat)?.Navigation.Riding==true;
        Vector2 best=cat.Center;float score=float.MaxValue;
        foreach(int radius in new[]{32,64,128})
        foreach(var direction in new[]{new Vector2(-1,0),new Vector2(1,0),new Vector2(0,-1),new Vector2(-1,-1),new Vector2(1,-1),new Vector2(0,1)}){
            var point=cat.Center+direction*radius;
            if(!flying&&!CompanionNavigation.TryStand(point,out point))continue;
            if(!CompanionStation.Contains(state,point)||!Safe(cat,point)||!CompanionNavigation.ClearFlight(cat.Center,point))continue;
            float cost=Vector2.DistanceSquared(point,cat.Center)+Vector2.DistanceSquared(point,preferred)*.1f;
            if(cost<score){score=cost;best=point;}
        }
        return best;
    }
    static float SpacingRange(Item item,Projectile cat,NPC target=null){
        float range=EffectiveRange(item,cat,target);
        // The ranged fallback permits an unknown attack to fire, but does not
        // establish where a custom melee swing can actually connect. Approach
        // within a modest melee band; native reach and miss recovery still
        // constrain short attacks. Traveling throws retain ranged positioning.
        if(!item.noMelee&&item.CountsAsClass(DamageClass.Melee))range=Math.Min(range,CompanionWeapons.Range(item)+24);
        else if(CloseCombat(item)&&Range(item,cat)==RangedRange)range=Math.Min(range,160);
        if(Sample(item)?.aiStyle is ProjAIStyleID.NightsEdge or ProjAIStyleID.TrueNightsEdge)range=Math.Min(range,100);
        return range;
    }
    static bool CloseCombat(Item item){
        if(!item.CountsAsClass(DamageClass.Melee))return false;
        int? style=Sample(item)?.aiStyle;
        return style is not (ProjAIStyleID.Yoyo or ProjAIStyleID.Harpoon or ProjAIStyleID.Flail or ProjAIStyleID.Boomerang)&&
            (!item.noMelee||style is not (ProjAIStyleID.ThrownProjectile or ProjAIStyleID.StickProjectile));
    }
    // Finite-reach weapons must approach targets inside the existing targeting
    // leash. The tighter ranged hover area must not cancel that approach.
    static bool NeedsApproach(Item item,Projectile cat)=>CloseCombat(item)||Range(item,cat)<RangedRange||
        CompanionWeaponAim.TryFlight(item,(cat.ModProjectile as CompanionCat)?.Weapons,out _,out _,out _);
    internal static bool PursuitLocal(Item item,Projectile cat,Vector2 point,Vector2 anchor)=>
        NeedsApproach(item,cat)
            ?Vector2.DistanceSquared(point,anchor)<TrackingRange*TrackingRange:Local(point,anchor);
    internal static bool Local(Vector2 point,Vector2 anchor)=>Math.Abs(point.Y-anchor.Y)<=240&&Vector2.DistanceSquared(point,anchor)<=560*560;
    internal static Vector2 AimPoint(Item item,Vector2 hand,NPC target) {
        if(Distance(item))return target.Center;
        var surface=target.Hitbox.ClosestPointInRect(hand);
        return surface+(target.Center-surface).SafeNormalize(Vector2.UnitX)*4;
    }
    internal static bool CanUse(Item item,Projectile cat,NPC target)=>target!=null&&
        Microsoft.Xna.Framework.Vector2.DistanceSquared(CompanionSprite.Hand(cat),target.Hitbox.ClosestPointInRect(CompanionSprite.Hand(cat)))<=MathF.Pow(EffectiveRange(item,cat,target),2)&&
        CompanionWeaponAim.CanReach(item,(cat.ModProjectile as CompanionCat)?.Weapons,cat.Center,target);
}
