using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

internal static partial class CompanionNetwork {
    const byte Pulse=25;
    static readonly byte[][] equipmentState=new byte[256][];
    internal static int FullSnapshots,Pulses;
    // Use ItemIO, including GlobalItem/ModItem data; never fingerprint just type/stack.
    internal static byte[] EquipmentState(CompanionPlayer s){
        using var memory=new MemoryStream();using var w=new BinaryWriter(memory);
        w.Write(s.CatName);w.Write(s.HoodColor);w.Write(s.Bonded);w.Write(s.Introduced);w.Write(s.Pickup);w.Write(s.Combat);w.Write(s.Torches);w.Write(s.Bridges);w.Write(s.Ropes);w.Write(s.AutoLogistics);
        s.Chests.Write(w);Items(w,s.Bag);Items(w,s.Gear);Items(w,s.Dyes);Items(w,s.Training.Slots);w.Write(s.Training.Tier);w.Write(s.Training.Expanded);s.Loadouts.Write(w);
        return memory.ToArray();
    }
    static void Periodic(CompanionPlayer s){
        var state=EquipmentState(s);int owner=s.Player.whoAmI;
        if(equipmentState[owner]==null||!state.AsSpan().SequenceEqual(equipmentState[owner])){Send(s);return;}
        Cache(s);using var p=Packet(Pulse);p.Write((byte)owner);p.Write(s.Stowed);p.Write(s.Enabled);p.Write(s.Paused);p.Write(s.Status??"");p.Write(s.Vitals.EffectiveDefense);Vitals(p,s.Vitals);s.Training.WriteTimers(p);
        p.Write((ulong)Main.GameUpdateCount);p.Write(s.Enabled&&!s.Player.dead&&!s.NetLeaving&&s.Cat?.ModProjectile is CompanionCat cat?cat.NetworkInstance:0UL);p.Send();Pulses++;
    }
    static void ReadPulse(BinaryReader r){
        int owner=r.ReadByte();var s=Main.player[owner].GetModPlayer<CompanionPlayer>();
        bool stowed=r.ReadBoolean(),enabled=r.ReadBoolean(),paused=r.ReadBoolean();string status=r.ReadString();int defense=r.ReadInt32();var vitals=new CompanionVitals();Vitals(r,vitals);
        s.Training.ReadTimers(r);ulong time=r.ReadUInt64(),token=r.ReadUInt64();CompanionRoster.Confirm(owner,token,time);
        s.Stowed=stowed;s.Enabled=enabled;s.Paused=paused;s.Status=status;s.Vitals=vitals;s.Vitals.EffectiveDefense=defense;
    }
    internal static void ClearPulse(){Array.Clear(equipmentState);FullSnapshots=Pulses=0;}
}
public sealed class CompanionPulseLifecycle:ModSystem {
    public override void OnWorldLoad()=>CompanionNetwork.ClearPulse();
    public override void OnWorldUnload()=>CompanionNetwork.ClearPulse();
}
