using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed partial class CompanionTraining {
    internal static bool ManualSkill(int kind)=>kind is 0 or 2 or 3 or 4 or 5 or 7;
    internal void ReconcileEffects(CompanionPlayer s) {
        if(Find(s,4)<0)Drum=0;
        if(Find(s,3)<0)ManaDance=0;
        if(Find(s,0)<0)SlowHealing=0;
        if(Find(s,2)<0)CleanseTime=0;
    }
    internal bool Activate(CompanionPlayer s,int kind,bool manual,out string message) {
        message=CompanionText.Message("CompanionSkillActivation.AutomaticOnly");
        if(!ManualSkill(kind))return false;
        message=CompanionText.Message("CompanionSkillActivation.NotEquipped");if(Find(s,kind)<0)return false;
        message=CompanionText.Message("CompanionSkillActivation.Unavailable");if(!Active(s))return false;
        message=CompanionText.Message("CompanionSkillActivation.Cooldown");if(!Ready(s,kind))return false;
        var p=s.Player;var cat=CatFor(s);var use=(cat.ModProjectile as CompanionCat)?.Weapons;
        int debuff=0,manaTarget=0;NPC target=null;
        switch(kind) {
            case 0:
                message=CompanionText.Message("CompanionSkillActivation.NoHealingNeeded");
                if(p.statLife>=p.statLifeMax2||!manual&&(p.statLifeMax2-p.statLife<new[]{20,40,65}[Tier]||p.statLife>p.statLifeMax2*.7f))return false;
                break;
            case 2:
                debuff=MostUrgentDebuff(p,manual);
                message=CompanionText.Message("CompanionSkillActivation.NoDebuff");if(debuff==0)return false;break;
            case 3:
                bool playerNeeds=p.statMana<p.statManaMax2&&(manual||p.statManaMax2-p.statMana>=ManaDeficit(p.statManaMax2)&&p.statMana<p.statManaMax2/2&&p.HeldItem.mana>0&&(p.itemAnimation>0||p.controlUseItem));
                bool catNeeds=s.Vitals.Mana<s.Vitals.MaxMana&&(manual||s.Vitals.MaxMana-s.Vitals.Mana>=ManaDeficit(s.Vitals.MaxMana)&&s.Gear[0].mana>0&&s.Combat&&use!=null&&(use.Busy||use.Actor.controlUseItem||use.WaitingForMana));
                message=CompanionText.Message("CompanionSkillActivation.NoManaNeeded");if(!playerNeeds&&!catNeeds)return false;
                manaTarget=playerNeeds?0:1;break;
            case 4:
                message=CompanionText.Message("CompanionSkillActivation.NotAttacking");
                if(!manual&&(!s.Combat||use==null||!use.Busy&&!Main.projectile.Any(q=>q.active&&q.minion&&CompanionOwnedShot.Of(q)?.State==s&&Main.npc.Any(n=>n.active&&n.CanBeChasedBy()&&Vector2.DistanceSquared(q.Center,n.Center)<240*240))))return false;
                break;
            case 5: case 7:
                message=CompanionText.Message("CompanionSkillActivation.NoTarget");
                if(!manual&&!s.Combat||use==null)return false;
                if(manual) {
                    int targetId=p.MinionAttackTargetNPC;
                    var marked=targetId>=0&&targetId<Main.maxNPCs&&CompanionTargeting.Valid(p,cat.Center,Main.npc[targetId])?Main.npc[targetId]:null;
                    if(marked!=null&&Vector2.DistanceSquared(marked.Center,cat.Center)<MathF.Pow(kind==7?560:900,2))target=marked;
                }
                target??=SupportTarget(s,cat,kind==7);if(target==null)return false;
                // Do not spend a cooldown if there is no projectile slot.
                if(!Main.projectile.Any(q=>!q.active)){message=CompanionText.Message("CompanionSkillActivation.ProjectileLimit");return false;}
                break;
        }
        if(CompanionNetwork.Client){message=CompanionText.Message("CompanionSkillActivation.ServerPending");return false;}
        if(!Use(s,kind,CooldownSeconds(kind),kind is 0 or 2||kind==3&&manaTarget==0?p.Center:null))return false;
        switch(kind) {
            case 0:OwnerEffect(s,0,new[]{15,30,50}[Tier]);SlowHealing=EffectDuration(0);break;
            case 2:CleanseType=debuff;CleanseTime=EffectDuration(2);OwnerEffect(s,2,debuff);break;
            case 3:
                ManaDance=EffectDuration(3);ManaTarget=manaTarget;
                if(manaTarget==0)OwnerEffect(s,1,new[]{20,40,60}[Tier]);
                else {int restored=Math.Min(new[]{20,40,60}[Tier],s.Vitals.MaxMana-s.Vitals.Mana);s.Vitals.Mana+=restored;CompanionFeedback.Show(2,cat.Center,restored,"skill");}
                break;
            case 4:Drum=EffectDuration(4);break;
            case 5:case 7:
                if(use.State==null)use.BindReplica(p,s,cat);
                using(var scope=new CompanionNativeUse.Scope(use)) {
                    Vector2 origin=CompanionSprite.Hand(cat);int id;
                    if(kind==5)id=Projectile.NewProjectile(cat.GetSource_FromThis(),origin,(target.Center-origin).SafeNormalize(Vector2.UnitX)*12,ModContent.ProjectileType<TrainingDart>(),1,0,use.Actor.whoAmI);
                    else {
                        int flight=Math.Clamp((int)(Vector2.Distance(origin,target.Center)/8),8,56);Vector2 lead=target.velocity*flight;
                        if(lead.LengthSquared()>240*240)lead=lead.SafeNormalize(Vector2.Zero)*240;
                        Vector2 velocity=(target.Center+lead-origin)/flight-new Vector2(0,.05f*(flight+1));
                        id=Projectile.NewProjectile(cat.GetSource_FromThis(),origin,velocity,ModContent.ProjectileType<TrainingBomb>(),BombDamage(Tier),4,use.Actor.whoAmI,flight+2);
                    }
                    if(id<Main.maxProjectiles)Main.projectile[id].netUpdate=true;
                }
                break;
        }
        s.Expression.GearUsed(s,kind,kind==3&&manaTarget==1);
        message=CompanionText.Message("CompanionSkillActivation.Used" ,Terraria.Localization.Language.GetText("Mods.PalicoCompanion.TrainingNames."+TrainingCatalog.Entries[kind].Key));return true;
    }
}
