using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Item ownership stays in Gear/Loadouts. This object only caches evaluated
// attributes, so UI previews never execute equipment hooks or spawn effects.
internal sealed class CompanionEquipment {
    internal readonly struct ProjectileAnchor:IDisposable {
        readonly Player actor;
        readonly Vector2 position;
        internal ProjectileAnchor(Projectile projectile,CompanionNativeUse use){
            // CrystalLeaf AI hovers above its owner's body, not its weapon
            // hand. Keep native positioning, targeting and fire cadence intact.
            actor=projectile.aiStyle==ProjAIStyleID.CrystalLeaf?use.Actor:null;
            position=actor?.position??default;
            if(actor!=null)actor.Center=use.Cat.Center+new Vector2(0,use.Cat.gfxOffY);
        }
        public void Dispose(){if(actor!=null)actor.position=position;}
    }
    internal const int AccessoryStart=5, AccessoryCount=7;
    static readonly Item Empty=new();
    internal Player Preview;
    Player validation;
    readonly Item[] validationArmor=new Item[20];
    // ItemSlot's player-specific check is internal in tML. Bind it once so
    // companion clicks and quick-equip use the same placement rules as players.
    static readonly Func<Player,Item[],Item,int,bool> AccessoryBlocked=
        typeof(Terraria.UI.ItemSlot).GetMethod("AccCheck_ForPlayer",BindingFlags.Static|BindingFlags.NonPublic)
        .CreateDelegate<Func<Player,Item[],Item,int,bool>>();
    internal float Endurance;
    internal int Defense,HealthBonus,ManaBonus,Regen,Minions=1,Sentries=1;
    int foodRemainder;
    bool wasIndependent;
    readonly CompanionPotionBuffs.Sharing potions=new();
    internal void FoodRegeneration(Player actor,int units,bool fed){
        if(!fed||units<=0||actor.statLife>=actor.statLifeMax2){foodRemainder=0;return;}
        foodRemainder+=units;actor.lifeRegenCount+=foodRemainder/4;foodRemainder%=4;
        if(actor.lifeRegenCount>=120){actor.statLife=Math.Min(actor.statLifeMax2,actor.statLife+actor.lifeRegenCount/120);actor.lifeRegenCount%=120;}
    }
    internal static bool IsSlot(int slot)=>slot is >=1 and <=3 or >=5 and <12;
    internal static int ArmorIndex(int slot)=>slot<4?slot-1:slot-2;
    internal static bool Independent(CompanionPlayer state)=>state.Loadouts.Independent;
    internal static void SetMode(CompanionPlayer state,bool independent){
        if(state.Loadouts.Independent==independent)return;
        state.Loadouts.Independent=independent;
        if(!CompanionNetwork.Client&&state.Cat?.ModProjectile is CompanionCat cat)cat.Weapons.Cancel(preserveSkills:false);
    }

    internal static void AdvanceClocks(CompanionNativeUse use){
        // Player.Update owns these passive proc cooldowns, outside ItemCheck
        // and UpdateArmorSets. Advance them once with the actor's native clock;
        // the native hit/use code still decides when and what to spawn.
        var actor=use.Actor;
        if(actor.immuneTime>0)actor.immuneTime--;
        actor.immune=actor.immuneTime>0;
        for(int i=0;i<actor.hurtCooldowns.Length;i++)if(actor.hurtCooldowns[i]>0)actor.hurtCooldowns[i]--;
        if(actor.starCloakCooldown>0)actor.starCloakCooldown--;
        if(actor.petalTimer>0)actor.petalTimer--;
        if(actor.boneGloveTimer>0)actor.boneGloveTimer--;
        if(actor.crystalLeafCooldown>0)actor.crystalLeafCooldown--;
        if(actor.shadowDodgeTimer>0)actor.shadowDodgeTimer--;
        actor.ghostDmg=Math.Max(0,actor.ghostDmg-6.6666665f);
    }

    internal static bool Accepts(CompanionPlayer state,Item item,int slot){
        if(item.IsAir)return true;
        if(!Independent(state)||!IsSlot(slot))return false;
        if(slot<4)return slot switch {1=>item.headSlot>=0,2=>item.bodySlot>=0,_=>item.legSlot>=0};
        if(!CompanionEquipmentSlots.Unlocked(state,slot)||!item.accessory||item.expertOnly&&!Main.expertMode)return false;
        var actor=(state.Cat?.ModProjectile as CompanionCat)?.Weapons.Actor??(state.Equipment.validation??=new Player());
        var armor=state.Equipment.validationArmor;
        Array.Fill(armor,Empty);
        for(int i=1;i<state.Gear.Length;i++)
            if(IsSlot(i)&&CompanionEquipmentSlots.Unlocked(state,i))armor[ArmorIndex(i)]=state.Gear[i];
        var previous=actor.armor;
        try{
            // Hooks must see this loadout, even before the next equipment tick
            // and on remote clients, without consulting Main.LocalPlayer.
            actor.armor=armor;
            return ItemLoader.CanEquipAccessory(actor,item,ArmorIndex(slot),false)&&
                !AccessoryBlocked(actor,armor,item,ArmorIndex(slot));
        }finally{actor.armor=previous;}
    }

    internal static bool TryEquip(CompanionPlayer state,Item item){
        if(!Independent(state)||item.IsAir)return false;
        for(int i=1;i<state.Gear.Length;i++)if(IsSlot(i)&&state.Gear[i].IsAir&&Accepts(state,item,i)){
            state.Gear[i]=ItemLoader.TransferWithLimit(item,1);
            if(item.stack<=0)item.TurnToAir();
            return true;
        }
        return false;
    }

    internal void Apply(CompanionNativeUse use){
        var actor=use.Actor;var state=use.State;
        bool independent=Independent(state);
        if(wasIndependent!=independent){
            // Positive equipment buffs cannot carry into inheritance mode.
            // Keep debuffs and dodge cooldowns: switching modes is not a cleanse
            // or a way to refresh an exhausted dodge.
            if(!independent)for(int i=actor.buffType.Length-1;i>=0;i--)
                if(actor.buffType[i]>0&&!Main.debuff[actor.buffType[i]])actor.DelBuff(i);
            actor.lifeRegenTime=0;actor.lifeRegenCount=0;foodRemainder=0;
            wasIndependent=independent;
        }
        potions.Update(actor,state.Player,CompanionBalanceConfig.Current.SharePotionBuffs);
        for(int i=0;i<actor.armor.Length;i++)actor.armor[i]=Empty;
        Array.Clear(actor.hideVisibleAccessory);
        if(independent)for(int i=AccessoryStart;i<AccessoryStart+AccessoryCount;i++)actor.hideVisibleAccessory[ArmorIndex(i)]=state.Loadouts.Hidden(i);
        if(!independent){
            Preview=null;actor.head=actor.body=actor.legs=-1;actor.beetleDefense=actor.beetleOffense=actor.setSolar=false;
            actor.statLifeMax=actor.statLifeMax2=state.Vitals.MaxHealth;
            actor.statManaMax=actor.statManaMax2=state.Vitals.MaxMana;
            actor.statDefense=Player.DefenseStat.Default;actor.statDefense+=state.Vitals.EffectiveDefense;
            actor.endurance=state.Vitals.EffectiveEndurance;
            actor.maxMinions=Math.Max(0,state.Player.maxMinions);actor.maxTurrets=Math.Max(0,state.Player.maxTurrets);
            using(new CompanionNativeUse.Scope(use)){
                CompanionDebuffs.Update(use);
                CompanionCombatInheritance.Apply(actor,state.Player);
                use.Debuffs.ApplyCombat(actor);
                // Native post-buff/equipment status hooks must see the final
                // inherited base, rather than have their penalties overwritten.
                PlayerLoader.PostUpdateEquips(actor);
                CompanionCrossMod.UpdateInheritedStatus?.Invoke(use);
            }
            Minions=Math.Max(0,actor.maxMinions);Sentries=Math.Max(0,actor.maxTurrets);
            CompanionDebuffs.RemoveExpiredOrImmune(actor);
            CompanionDebuffs.ApplyDefense(actor);
            use.EquipmentRegeneration();
            return;
        }
        for(int i=1;i<state.Gear.Length;i++)if(IsSlot(i)&&CompanionEquipmentSlots.Unlocked(state,i))actor.armor[ArmorIndex(i)]=state.Gear[i];
        int HealthAtEquipmentScale()=>(int)((long)state.Vitals.Health*actor.statLifeMax2/Math.Max(1,state.Vitals.MaxHealth));
        int healthChange,manaBefore=actor.statMana=state.Vitals.Mana,manaChange;
        // Preserve the cat's injury percentage for low-life conditions.
        int healthBeforeBuffs=actor.statLife=HealthAtEquipmentScale();
        actor.maxMinions=actor.maxTurrets=1;
        // Buffs precede equipment, but retain native remote-owner checks.
        // Unlike equipment flags, aura damage must not run on visual replicas.
        using(new CompanionNativeUse.Scope(use,trackHealing:false))CompanionDebuffs.Update(use);
        // Composite items need local-owner checks to reconstruct their flags
        // on replicas. Limit that override to the item/set calculation.
        using(new CompanionNativeUse.Scope(use,trackHealing:false,equipmentOwner:true)){
            // TurnToAir can leave equip-slot metadata behind. Empty slots must
            // not keep granting the set bonus of their previous armor.
            actor.head=actor.armor[0].IsAir?-1:actor.armor[0].headSlot;
            actor.body=actor.armor[1].IsAir?-1:actor.armor[1].bodySlot;
            actor.legs=actor.armor[2].IsAir?-1:actor.armor[2].legSlot;
            // These set flags are cleared by Player.Update, not ResetEffects.
            actor.beetleDefense=actor.beetleOffense=actor.setSolar=false;
            actor.UpdateEquips(actor.whoAmI);
            actor.UpdateArmorSets(actor.whoAmI);
            // Player.Update clears Holy Protection when its granting set is
            // removed. Keep the cooldown, but do not carry a free dodge into
            // another loadout (or an empty armor slot).
            if(actor.shadowDodge&&!actor.onHitDodge){
                actor.ClearBuff(BuffID.ShadowDodge);
                actor.shadowDodge=false;
            }
        }
        // Passive effects may deal damage or apply NPC buffs. Keep their native
        // remote-owner checks, just as for buffs and projectile AI.
        using(new CompanionNativeUse.Scope(use,trackHealing:false)){
            PlayerLoader.PostUpdateEquips(actor);
            // Max-life equipment must not make a healthy cat appear injured
            // to conditional passives. Preserve actual healing separately.
            healthChange=actor.statLife-healthBeforeBuffs;
            int before=HealthAtEquipmentScale();actor.statLife=before+healthChange;
            // Player.Update applies this limit before PostUpdateMiscEffects.
            // Preserve that order so later mod hooks can still alter it.
            actor.pickSpeed=Math.Max(.3f,actor.pickSpeed);
            CompanionCrossMod.UpdateEquipment?.Invoke(use);
            healthChange=actor.statLife-before;
            manaChange=actor.statMana-manaBefore;
            Defense=Math.Max(0,(int)actor.statDefense);
            Endurance=Math.Clamp(actor.endurance,0,1);
            HealthBonus=actor.statLifeMax2-actor.statLifeMax;
            ManaBonus=actor.statManaMax2-actor.statManaMax;
            Regen=Math.Max(0,actor.lifeRegen);
            Minions=Math.Max(0,actor.maxMinions);Sentries=Math.Max(0,actor.maxTurrets);
            Preview??=new Player();
            CompanionCombatInheritance.ResetModifiers(Preview);
            CompanionCombatInheritance.CopyEquipment(Preview,actor);
        }
        CompanionDebuffs.ApplyDefense(actor);
        Defense=Math.Max(0,(int)actor.statDefense);
        state.Vitals.Sync(state.Player,state.Gear);
        if(!CompanionNetwork.Client&&manaChange!=0)
            state.Vitals.Mana=Math.Clamp(state.Vitals.Mana+manaChange,0,state.Vitals.MaxMana);
        if(!CompanionNetwork.Client&&healthChange!=0){
            int before=state.Vitals.Health;
            state.Vitals.Health=Math.Clamp(before+healthChange,1,state.Vitals.MaxHealth);
            if(state.Vitals.Health>before)CompanionFeedback.Show(1,use.Cat.Center,state.Vitals.Health-before,"skill");
        }
        actor.statLife=state.Vitals.Health;actor.statLifeMax=actor.statLifeMax2=state.Vitals.MaxHealth;
        actor.statMana=state.Vitals.Mana;actor.statManaMax=actor.statManaMax2=state.Vitals.MaxMana;
        CompanionDebuffs.RemoveExpiredOrImmune(actor);
        use.EquipmentRegeneration();
    }
}
