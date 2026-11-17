using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed partial class CompanionNativeUse {
    internal int HeldGearSlot {get;private set;}
    readonly Item[] equippedSummons=new Item[2];
    readonly int[] summonTypes=new int[2],summonPrefixes=new int[2];
    readonly ulong[] summonRetryAt=new ulong[2];
    bool yieldToSummon,automaticSwitch;
    static int SummonIndex(int slot)=>slot==CompanionWeapons.SentrySlot?1:0;

    void ReconcileSummonEquipment() {
        for(int index=0;index<2;index++){
            int slot=index==0?CompanionWeapons.SummonSlot:CompanionWeapons.SentrySlot;
            var item=CompanionWeapons.InSlot(State.Gear,slot);
            if(ReferenceEquals(item,equippedSummons[index])&&item.type==summonTypes[index]&&item.prefix==summonPrefixes[index])continue;
            // Each dedicated slot owns one summon family, including child shots.
            // Replacing it cannot dismiss the other slot's still-equipped attacks.
            if(summonTypes[index]!=0)foreach(var p in Main.projectile)if(p.active&&CompanionOwnedShot.Of(p)==this&&
                (index==0?(p.minion||ProjectileID.Sets.MinionShot[p.type]||p.GetGlobalProjectile<CompanionOwnedShot>().SummonKind==1):
                          (p.sentry||ProjectileID.Sets.SentryShot[p.type]||p.GetGlobalProjectile<CompanionOwnedShot>().SummonKind==2)))Retire(p);
            if(HeldGearSlot==slot)Cancel(preserveReleased:true,preserveSummons:true);
            equippedSummons[index]=item;summonTypes[index]=item.type;summonPrefixes[index]=item.prefix;
            summonRetryAt[index]=0;
        }
    }
    static bool Summoned(Projectile p)=>p.minion||p.sentry||ProjectileID.Sets.MinionShot[p.type]||ProjectileID.Sets.SentryShot[p.type]||p.GetGlobalProjectile<CompanionOwnedShot>().Summoned;
    // Vanilla WipableTurret also sees inactive entries. Retire their lifetime
    // as Kill does, without invoking death effects or spawning child attacks.
    static void Retire(Projectile p){p.timeLeft=0;p.active=false;if(CompanionNetwork.Server)NetMessage.SendData(MessageID.KillProjectile,-1,-1,null,p.identity,p.owner);}

    bool NeedsSummon(Player owner,NPC target,bool allowed,int slot) {
        var item=CompanionWeapons.InSlot(State.Gear,slot);
        if(!allowed||item.IsAir||!CompanionVitals.Accepts(item,slot)||Main.GameUpdateCount<summonRetryAt[SummonIndex(slot)])return false;
        if(Summoning(item)){
            if(SummonSlots+Math.Max(.01f,ItemID.Sets.StaffMinionSlotsRequired[item.type])>MinionLimit(owner))return false;
            return CanEquipSummon(item,owner,slot);
        }
        Actor.maxTurrets=SentryLimit(owner);
        var point=target?.Center??Cat.Center;
        return CompanionWeaponPolicy.Plan(item,this,target,ref point)&&CanEquipSummon(item,owner,slot);
    }
    bool CanEquipSummon(Item item,Player owner,int slot){
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
            if(!allowed)summonRetryAt[SummonIndex(slot)]=Main.GameUpdateCount+60;
            return allowed;
        }finally{
            Actor.inventory[selected]=held;
            Actor.maxMinions=minions;Actor.maxTurrets=turrets;
        }
    }
    Item SelectWeapon(Player owner,NPC target,bool allowed) {
        automaticSwitch=false;ReconcileSummonEquipment();
        int next=NeedsSummon(owner,target,allowed,CompanionWeapons.SummonSlot)?CompanionWeapons.SummonSlot:
            NeedsSummon(owner,target,allowed,CompanionWeapons.SentrySlot)?CompanionWeapons.SentrySlot:0;
        yieldToSummon=next!=0&&HeldGearSlot!=next;
        // Finish the current use before changing the held item. Releasing a
        // channel lets its native projectile complete its normal withdrawal.
        if(Actor.itemAnimation<=0&&Actor.itemTime<=0){
            if(next!=HeldGearSlot){
                Cancel(preserveReleased:true,preserveSummons:true);HeldGearSlot=next;
                automaticSwitch=true;type=prefix=-1;
                if(Cat.ModProjectile is CompanionCat companion)companion.PendingPresentation=true;
            }
            yieldToSummon=false;
        }
        return CompanionWeapons.InSlot(State.Gear,HeldGearSlot);
    }
    void SummonAttempt(Item item,bool started){
        // Failed CanUse/placement must not monopolize the main weapon. Sentries
        // also need time to settle before considering another deployment.
        // Successful minion casts already obey ItemCheck's adjusted animation,
        // item time and reuse delay. A second base-animation timer ignores use
        // speed bonuses and can switch away between otherwise continuous casts.
        summonRetryAt[SummonIndex(HeldGearSlot)]=started&&!CompanionWeaponPolicy.Sentry(item)?0:Main.GameUpdateCount+(ulong)(started?120:60);
    }
}

// Speed up the existing sequence, not projectile creation. ItemCheck still
// pays each cast and runs the staff's slot/group rules. Sentry deployment and
// channeled summons keep their native cadence and projectile-driven timing.
public sealed class CompanionSummonCasting:ModPlayer {
    public override float UseSpeedMultiplier(Item item)=>CompanionNativeUse.Current is {} use&&use.Actor==Player&&
        use.HeldGearSlot==CompanionWeapons.SummonSlot&&!item.channel&&!CompanionWeaponPolicy.Sentry(item)&&
        CompanionNativeUse.Summoning(item)?2.5f:1f;
}
