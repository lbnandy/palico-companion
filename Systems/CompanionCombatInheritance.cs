using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Transfer modifiers, not equipment or a fully evaluated weapon's damage.
// Player's normal damage-class inheritance then handles hybrids, whips and
// modded classes, and PickAmmo still evaluates the cat's own ammunition.
internal static class CompanionCombatInheritance {
    static readonly Action<Player> ResetDamageClasses=typeof(Player).GetMethod("ResetDamageClassData",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate<Action<Player>>();

    internal static void ResetPreview(Player actor){
        // Reset only value data. Running ModPlayer.ResetEffects on a tooltip
        // player can execute unrelated UI/world code before entering a world.
        ResetDamageClasses(actor);
        Resources(actor,null);
    }

    internal static void Apply(Player actor,Player owner){
        float ratio=CompanionBalanceConfig.Current.CombatInheritanceScale;
        CompanionCrossMod.InheritStats?.Invoke(actor,owner,ratio);
        if(ratio<=0)return;
        for(int i=0;i<DamageClassLoader.DamageClassCount;i++){
            var type=DamageClassLoader.GetDamageClass(i);
            var damage=owner.GetDamage(type);
            // Remove the owner's potion penalty before scaling bonuses. The
            // companion applies its own mana sickness in the native-use path.
            if(type==DamageClass.Magic&&owner.manaSick)damage/=Math.Max(.01f,1-owner.manaSickReduction);
            actor.GetDamage(type)=Blend(actor.GetDamage(type),damage,ratio);
            actor.GetCritChance(type)=Blend(actor.GetCritChance(type),owner.GetCritChance(type),ratio);
            actor.GetAttackSpeed(type)=Blend(actor.GetAttackSpeed(type),owner.GetAttackSpeed(type),ratio);
            actor.GetArmorPenetration(type)=Blend(actor.GetArmorPenetration(type),owner.GetArmorPenetration(type),ratio);
            actor.GetKnockback(type)=Blend(actor.GetKnockback(type),owner.GetKnockback(type),ratio);
        }
        Resources(actor,owner,ratio);
    }

    internal static float Blend(float baseline,float value,float ratio)=>baseline+(value-baseline)*ratio;
    internal static int Blend(int baseline,int value,float ratio)=>(int)MathF.Round(Blend((float)baseline,value,ratio));
    static StatModifier Blend(StatModifier baseline,StatModifier value,float ratio)=>new(
        Blend(baseline.Additive,value.Additive,ratio),Blend(baseline.Multiplicative,value.Multiplicative,ratio),
        Blend(baseline.Flat,value.Flat,ratio),Blend(baseline.Base,value.Base,ratio));

    static void Resources(Player actor,Player owner,float ratio=0){
        actor.arrowDamage=Blend(StatModifier.Default,owner?.arrowDamage??StatModifier.Default,ratio);
        actor.bulletDamage=Blend(StatModifier.Default,owner?.bulletDamage??StatModifier.Default,ratio);
        actor.specialistDamage=Blend(StatModifier.Default,owner?.specialistDamage??StatModifier.Default,ratio);
        actor.whipRangeMultiplier=Blend(1f,owner?.whipRangeMultiplier??1,ratio);
        actor.meleeScaleGlove=owner?.meleeScaleGlove??false;
        actor.manaCost=Blend(1f,owner?.manaCost??1,ratio);
        actor.manaRegenBonus=Blend(0,owner?.manaRegenBonus??0,ratio);
        actor.manaRegenDelayBonus=Blend(0f,owner?.manaRegenDelayBonus??0,ratio);
        actor.manaRegenBuff=owner?.manaRegenBuff??false;
        actor.usedArcaneCrystal=owner?.usedArcaneCrystal??false;
        // Native on/off resource effects remain on/off when inheritance is
        // enabled. Do not rewrite their mechanics or copy accessory triggers.
        actor.archery=owner?.archery??false;
        actor.magicQuiver=owner?.magicQuiver??false;
        actor.ammoCost75=owner?.ammoCost75??false;
        actor.ammoCost80=owner?.ammoCost80??false;
        actor.ammoBox=owner?.ammoBox??false;
        actor.ammoPotion=owner?.ammoPotion??false;
        actor.chloroAmmoCost80=owner?.chloroAmmoCost80??false;
        actor.huntressAmmoCost90=owner?.huntressAmmoCost90??false;
    }
}
