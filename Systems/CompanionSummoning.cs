using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed partial class CompanionNativeUse {
    internal int HeldGearSlot {get;private set;}
    Item equippedSummon;
    int summonType,summonPrefix;
    ulong summonRetryAt;
    bool yieldToSummon,automaticSwitch;

    void ReconcileSummonEquipment() {
        var item=CompanionWeapons.Summon(State.Gear);
        if(ReferenceEquals(item,equippedSummon)&&item.type==summonType&&item.prefix==summonPrefix)return;
        // Equipment owns the summons, not the temporary item in the actor's hand.
        // Removing/replacing it cannot bank a second set of persistent attacks.
        foreach(var p in Main.projectile)if(p.active&&CompanionOwnedShot.Of(p)==this&&Summoned(p))Retire(p);
        Array.Clear(Actor.buffType);Array.Clear(Actor.buffTime);
        if(HeldGearSlot==CompanionWeapons.SummonSlot)Cancel(preserveReleased:true,preserveSummons:true);
        equippedSummon=item;summonType=item.type;summonPrefix=item.prefix;summonRetryAt=0;
    }
    static bool Summoned(Projectile p)=>p.minion||p.sentry||ProjectileID.Sets.MinionShot[p.type]||ProjectileID.Sets.SentryShot[p.type]||p.GetGlobalProjectile<CompanionOwnedShot>().Summoned;
    static void Retire(Projectile p){p.active=false;if(CompanionNetwork.Server)NetMessage.SendData(MessageID.KillProjectile,-1,-1,null,p.identity,p.owner);}

    bool NeedsSummon(Player owner,NPC target,bool allowed) {
        var item=CompanionWeapons.Summon(State.Gear);
        if(!allowed||!CompanionWeapons.SummonWeapon(item)||Main.GameUpdateCount<summonRetryAt)return false;
        if(Summoning(item)){
            if(SummonSlots+Math.Max(.01f,ItemID.Sets.StaffMinionSlotsRequired[item.type])>MinionLimit(owner))return false;
            return CanEquipSummon(item,owner);
        }
        if(target==null)return false;
        Actor.maxTurrets=SentryLimit(owner);
        var point=target.Center;
        return CompanionWeaponPolicy.Plan(item,this,target,ref point)&&CanEquipSummon(item,owner);
    }
    bool CanEquipSummon(Item item,Player owner){
        // Free slots do not imply another summon is allowed (some staffs own
        // one group). Ask the native item before interrupting the main weapon.
        // ItemCheck still performs the actual use, payment and projectile spawn.
        CountOwnedProjectiles();
        int selected=Actor.selectedItem;
        Item held=Actor.inventory[selected];
        int minions=Actor.maxMinions,turrets=Actor.maxTurrets;
        try{
            Actor.inventory[selected]=item;
            Actor.maxMinions=MinionLimit(owner);Actor.maxTurrets=SentryLimit(owner);
            bool allowed;
            using(new Scope(this))allowed=CombinedHooks.CanUseItem(Actor,item);
            if(!allowed)summonRetryAt=Main.GameUpdateCount+60;
            return allowed;
        }finally{
            Actor.inventory[selected]=held;
            Actor.maxMinions=minions;Actor.maxTurrets=turrets;
        }
    }
    Item SelectWeapon(Player owner,NPC target,bool allowed) {
        automaticSwitch=false;ReconcileSummonEquipment();
        bool needed=NeedsSummon(owner,target,allowed);
        yieldToSummon=needed&&HeldGearSlot==0;
        // Finish the current use before changing the held item. Releasing a
        // channel lets its native projectile complete its normal withdrawal.
        if(Actor.itemAnimation<=0&&Actor.itemTime<=0){
            int next=needed?CompanionWeapons.SummonSlot:0;
            if(next!=HeldGearSlot){
                Cancel(preserveReleased:true,preserveSummons:true);HeldGearSlot=next;
                automaticSwitch=true;type=prefix=-1;
                if(Cat.ModProjectile is CompanionCat companion)companion.PendingPresentation=true;
            }
            yieldToSummon=false;
        }
        return HeldGearSlot==CompanionWeapons.SummonSlot?CompanionWeapons.Summon(State.Gear):CompanionWeapons.Active(State.Gear);
    }
    void SummonAttempt(Item item,bool started){
        // Failed CanUse/placement must not monopolize the main weapon. Sentries
        // also need time to settle before considering another deployment.
        summonRetryAt=Main.GameUpdateCount+(ulong)(started?CompanionWeaponPolicy.Sentry(item)?120:Math.Max(6,item.useAnimation):60);
    }
}
