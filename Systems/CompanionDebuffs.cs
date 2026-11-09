using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Buffs execute once on the companion's native actor. Preserve their own
// penalties when inheritance later replaces the shared combat stat groups.
public sealed class CompanionDebuffs:ModSystem {
    public override void Load()=>IL_Player.UpdateBuffs+=ObserveBuffs;
    public override void Unload()=>IL_Player.UpdateBuffs-=ObserveBuffs;
    internal static void Update(CompanionNativeUse use){
        use.Debuffs.Clear();
        var actor=use.Actor;
        PlayerLoader.PreUpdateBuffs(actor);
        Array.Clear(actor.buffImmune);
        actor.UpdateBuffs(actor.whoAmI);
        PlayerLoader.PostUpdateBuffs(actor);
    }
    internal static void RemoveExpiredOrImmune(Player actor){
        // Player.Update performs this cleanup after equipment has established
        // immunities. Equipping immunity also cures an already present debuff.
        for(int i=actor.buffType.Length-1;i>=0;i--)
            if(actor.buffType[i]>0&&(actor.buffTime[i]<=0||actor.buffImmune[actor.buffType[i]]))actor.DelBuff(i);
    }
    internal static void ApplyDefense(Player actor){
        // These flags are resolved in Player.Update after equipment, not in
        // UpdateBuffs/UpdateEquips. Match that order without replaying movement.
        if(actor.brokenArmor)actor.statDefense/=2f;
        if(actor.witheredArmor)actor.statDefense/=2f;
    }
    static void ObserveBuffs(ILContext il){
        var c=new ILCursor(il);int index=-1;
        // First native buff branch, after validation and timer decrement.
        if(!c.TryGotoNext(MoveType.Before,i=>i.MatchLdarg(0),
            i=>i.MatchLdfld<Player>(nameof(Player.buffType)),i=>i.MatchLdloc(out index),
            i=>i.OpCode==OpCodes.Ldelem_I4,i=>i.MatchLdcI4(1)))
            throw new InvalidOperationException("Native buff evaluation boundary was not found");
        c.Emit(OpCodes.Ldarg_0);c.Emit(OpCodes.Ldloc,il.Body.Variables[index]);
        c.EmitDelegate<Action<Player,int>>((actor,slot)=>{
            var use=CompanionNativeUse.Current;
            if(use?.Actor==actor&&!CompanionEquipment.Independent(use.State))use.Debuffs.Begin(actor,actor.buffType[slot]);
        });
        if(!c.TryGotoNext(MoveType.After,i=>i.MatchCall(typeof(BuffLoader),nameof(BuffLoader.Update))))
            throw new InvalidOperationException("Native buff completion boundary was not found");
        // Incoming branches include native buffs which remove themselves.
        c.MoveAfterLabels();
        c.Emit(OpCodes.Ldarg_0);c.EmitDelegate<Action<Player>>(actor=>{
            var use=CompanionNativeUse.Current;
            if(use?.Actor==actor&&!CompanionEquipment.Independent(use.State))use.Debuffs.End(actor);
        });
    }
    internal sealed class Modifiers {
        struct Values {
            internal StatModifier Damage,Knockback;
            internal float Crit,Speed,Penetration;
            internal static Values Read(Player p,DamageClass type)=>new(){Damage=p.GetDamage(type),Knockback=p.GetKnockback(type),Crit=p.GetCritChance(type),Speed=p.GetAttackSpeed(type),Penetration=p.GetArmorPenetration(type)};
        }
        readonly Values[] before=new Values[DamageClassLoader.DamageClassCount],delta=new Values[DamageClassLoader.DamageClassCount];
        bool recording;
        float manaBefore,mana;
        internal Modifiers()=>Clear();
        internal void Clear(){
            recording=false;mana=0;
            for(int i=0;i<delta.Length;i++)delta[i]=new(){Damage=StatModifier.Default,Knockback=StatModifier.Default};
        }
        internal void Begin(Player actor,int buff){
            // Mana sickness is already applied by Vitals.MagicMultiplier.
            recording=buff>0&&Main.debuff[buff]&&buff!=BuffID.ManaSickness;
            if(!recording)return;
            for(int i=0;i<before.Length;i++)before[i]=Values.Read(actor,DamageClassLoader.GetDamageClass(i));
            manaBefore=actor.manaCost;
        }
        static StatModifier Difference(StatModifier a,StatModifier b)=>new(1+b.Additive-a.Additive,
            a.Multiplicative==0?1:b.Multiplicative/a.Multiplicative,b.Flat-a.Flat,b.Base-a.Base);
        internal void End(Player actor){
            if(!recording)return;recording=false;
            for(int i=0;i<delta.Length;i++){
                var a=before[i];var b=Values.Read(actor,DamageClassLoader.GetDamageClass(i));ref var d=ref delta[i];
                d.Damage=d.Damage.CombineWith(Difference(a.Damage,b.Damage));
                d.Knockback=d.Knockback.CombineWith(Difference(a.Knockback,b.Knockback));
                d.Crit+=b.Crit-a.Crit;d.Speed+=b.Speed-a.Speed;d.Penetration+=b.Penetration-a.Penetration;
            }
            mana+=actor.manaCost-manaBefore;
        }
        internal void ApplyCombat(Player actor){
            for(int i=0;i<delta.Length;i++){
                var t=DamageClassLoader.GetDamageClass(i);var d=delta[i];
                actor.GetDamage(t)=actor.GetDamage(t).CombineWith(d.Damage);
                actor.GetKnockback(t)=actor.GetKnockback(t).CombineWith(d.Knockback);
                actor.GetCritChance(t)+=d.Crit;actor.GetAttackSpeed(t)+=d.Speed;actor.GetArmorPenetration(t)+=d.Penetration;
            }
            actor.manaCost+=mana;
        }
    }
}
