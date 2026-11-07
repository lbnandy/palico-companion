using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed partial class CompanionNativeUse {
    // Vitals remains authoritative. Player.Hurt evaluates equipment, dodge,
    // shield consumption and mod hooks on the existing isolated actor only.
    internal bool EquipmentHit(int damage,bool boss,NPC npc,Projectile shot,int cooldown){
        var v=State.Vitals;
        if(CompanionNetwork.Client||v.Downed||v.Immunity>0||!EnsureActorSlot())return false;
        using var scope=new Scope(this,false);
        var position=Actor.position;int width=Actor.width,height=Actor.height;
        bool receiving=ReceivingHit;
        try{
            ReceivingHit=true;Actor.position=Cat.position;Actor.width=Cat.width;Actor.height=Cat.height;
            Actor.dead=false;Actor.immune=false;Actor.immuneTime=0;Array.Clear(Actor.hurtCooldowns);
            Actor.statLife=v.Health;Actor.statMana=v.Mana;
            int before=Actor.statLife;
            var source=npc!=null?PlayerDeathReason.ByNPC(npc.whoAmI):PlayerDeathReason.ByProjectile(shot.owner,shot.whoAmI);
            int direction=(npc?.Center.X??shot.Center.X)<Cat.Center.X?1:-1;
            // Thorns belongs to native contact collision, before Hurt, rather
            // than the equipment hook. Preserve its native source/cap here.
            if(npc!=null){
                float thorns=Actor.turtleThorns?2:Actor.thorns;
                if(thorns>0)Actor.ApplyDamageToNPC(npc,(int)(Math.Min(1000,damage*thorns)*CompanionBalanceConfig.Current.DamageScale),10,-direction);
                if(Actor.cactusThorns)Actor.ApplyDamageToNPC(npc,(int)((Main.masterMode?45:Main.expertMode?30:15)*CompanionBalanceConfig.Current.DamageScale),10,-direction);
            }
            double dealt=Actor.Hurt(source,damage,direction,out var info,quiet:true,cooldownCounter:cooldown,
                dodgeable:npc?.IsDamageDodgeable()??true,armorPenetration:shot?.ArmorPenetration??0,knockback:0);
            if(dealt>0&&!Actor.dead){
                if(npc!=null){Actor.StatusFromNPC(npc);NPCLoader.OnHitPlayer(npc,Actor,info);PlayerLoader.OnHitByNPC(Actor,npc,info);}
                else {ProjectileLoader.OnHitPlayer(shot,Actor,info);PlayerLoader.OnHitByProjectile(Actor,shot,info);}
            }
            v.Mana=Math.Clamp(Actor.statMana,0,v.MaxMana);
            return CommitHitHealth(before,boss,60,CompanionNetwork.Server);
        } finally {
            ReceivingHit=receiving;Actor.position=position;Actor.width=width;Actor.height=height;
            Actor.statLife=v.Health;Actor.dead=false;
        }
    }

    internal int NativeHitImmunity=>Math.Max(Actor.immuneTime,Actor.hurtCooldowns.Length==0?0:System.Linq.Enumerable.Max(Actor.hurtCooldowns));
    internal bool CommitHitHealth(int before,bool boss,int immunityTicks,bool showDamage=true){
        var v=State.Vitals;
        int lost=(int)Math.Clamp((long)before-Actor.statLife,0,int.MaxValue);
        if(lost>0)return ((CompanionCat)Cat.ModProjectile).Feedback.HurtFinal(State,Cat,lost,boss,
            immunityTicks:Math.Max(immunityTicks,NativeHitImmunity),showDamage:showDamage);
        // Dodges and lethal-hit vetoes keep native cooldowns/immunity. A veto
        // can also restore more HP than we had before this hit.
        v.Immunity=Math.Max(v.Immunity,NativeHitImmunity);
        if(Actor.statLife>before){
            v.Health=Math.Clamp(Actor.statLife,1,v.MaxHealth);
            v.QuietTicks=0;v.LifeCounter=0;
            if(v.Health>before)CompanionFeedback.Show(1,Cat.Center,v.Health-before,"skill");
        }
        return false;
    }

    internal void EquipmentRegeneration(){
        if(CompanionNetwork.Client||!CompanionEquipment.Independent(State)||!Present)return;
        var v=State.Vitals;
        using var scope=new Scope(this,false);
        bool receiving=ReceivingHit;
        bool campfire=Main.SceneMetrics.HasCampfire,lantern=Main.SceneMetrics.HasHeartLantern;
        try{
            ReceivingHit=true;
            // The human camera's scene scan is not the companion's environment.
            // Evaluate only buffs actually present on this actor on every peer.
            Main.SceneMetrics.HasCampfire=Actor.HasBuff(BuffID.Campfire);
            Main.SceneMetrics.HasHeartLantern=Actor.HasBuff(BuffID.HeartLamp);
            Actor.statLife=v.Health;Actor.statLifeMax2=v.MaxHealth;
            int before=Actor.statLife,count=Actor.lifeRegenCount;
            Actor.sleeping.isSleeping=((CompanionCat)Cat.ModProjectile).Animation.Motion==CatMotion.Rest;
            Actor.UpdateLifeRegen();
            int units=(Actor.statLife-before)*120+Actor.lifeRegenCount-count;
            State.Equipment.FoodRegeneration(Actor,units,v.FoodTime>0&&CompanionFeeding.CanRecover(State,(CompanionCat)Cat.ModProjectile));
            if(Actor.statLife<=0){
                // Damage over time does not respect ordinary hit immunity.
                v.Immunity=0;
                v.HurtFinal(v.Health,CompanionIncomingDamage.BossNear(State.Player),immunityTicks:0);
            }
            else {
                v.Health=Math.Clamp(Actor.statLife,1,v.MaxHealth);
                v.Immunity=Math.Max(v.Immunity,NativeHitImmunity);
            }
        } finally {
            Main.SceneMetrics.HasCampfire=campfire;Main.SceneMetrics.HasHeartLantern=lantern;
            ReceivingHit=receiving;Actor.statLife=v.Health;Actor.dead=false;
        }
    }
}

// Apply the cat's existing support shield at the native final-damage boundary;
// do not subtract defense/endurance twice or trigger effects on the human owner.
public sealed class CompanionEquipmentHurt : ModPlayer {
    public override void ModifyHurt(ref Player.HurtModifiers modifiers){
        var use=CompanionNativeUse.Current;
        if(use is not {ReceivingHit:true}||use.Actor!=Player)return;
        modifiers.DisableSound();modifiers.DisableDust();
        modifiers.ModifyHurtInfo+=(ref Player.HurtInfo info)=>info.Damage=use.State.Training.Shield(use.State,info.Damage,true);
    }
}
