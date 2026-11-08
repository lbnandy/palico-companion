using System;
using System.IO;
using Terraria;

namespace PalicoCompanion.Systems;

// Read evaluated modifiers only. Opening this panel must never replay item,
// equipment or hit hooks. Multiplayer uses the server's existing snapshots.
internal sealed class CompanionStats {
    internal bool Ready;
    internal int Weapon,Class,Minions,Sentries,Inspiration,InspirationMax;
    internal float Additive=1,Multiplicative=1,Base,Flat,Crit,Speed=1,Penetration,Endurance,Regen;
    internal ulong Tick;
    internal void Capture(CompanionNativeUse use){
        if(CompanionNetwork.Client)return;
        var actor=use.Actor;var item=actor.HeldItem;var type=item.DamageType;
        var damage=actor.GetTotalDamage(type);
        Weapon=item.type;Class=type.Type;Additive=damage.Additive;Multiplicative=damage.Multiplicative;Base=damage.Base;Flat=damage.Flat;
        Crit=actor.GetTotalCritChance(type)+item.crit;
        Speed=actor.GetWeaponAttackSpeed(item);
        Penetration=actor.GetTotalArmorPenetration(type)+item.ArmorPenetration;
        Endurance=Math.Clamp(actor.endurance,0,1);
        Minions=CompanionNativeUse.MinionLimit(use.State.Player);Sentries=CompanionNativeUse.SentryLimit(use.State.Player);
        Inspiration=InspirationMax=0;CompanionCrossMod.CaptureResources?.Invoke(use,this);
        Regen=use.State.Vitals.RecoveryPerSecond;Ready=true;Tick=Main.GameUpdateCount;
    }
    internal bool Available(CompanionPlayer s)=>Ready&&s.Enabled&&!s.Stowed&&!s.Paused&&!s.Vitals.Downed&&!s.Player.dead&&
        (CompanionNetwork.Client||Main.GameUpdateCount-Tick<60);
    internal void Write(BinaryWriter w,CompanionPlayer s){
        w.Write(Available(s));w.Write(Weapon);w.Write(Class);
        w.Write(Additive);w.Write(Multiplicative);w.Write(Base);w.Write(Flat);w.Write(Crit);w.Write(Speed);w.Write(Penetration);w.Write(Endurance);w.Write(Regen);
        w.Write(Minions);w.Write(Sentries);w.Write(Inspiration);w.Write(InspirationMax);
    }
    internal static CompanionStats Read(BinaryReader r)=>new(){Ready=r.ReadBoolean(),Weapon=r.ReadInt32(),Class=r.ReadInt32(),
        Additive=r.ReadSingle(),Multiplicative=r.ReadSingle(),Base=r.ReadSingle(),Flat=r.ReadSingle(),Crit=r.ReadSingle(),Speed=r.ReadSingle(),Penetration=r.ReadSingle(),Endurance=r.ReadSingle(),Regen=r.ReadSingle(),
        Minions=r.ReadInt32(),Sentries=r.ReadInt32(),Inspiration=r.ReadInt32(),InspirationMax=r.ReadInt32()};
}
