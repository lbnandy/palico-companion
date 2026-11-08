using System;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Process-local native ItemCheck actors, NOT network player identities. A lease
// must never evict a real player, and two companions must not share hit immunity
// or native owner-indexed projectile state. Fishing still reserves slot 253.
internal sealed class CompanionActorSlots : ModSystem {
    static readonly WeakReference<CompanionNativeUse>[] leases=new WeakReference<CompanionNativeUse>[255];
    static readonly bool[] initializing=new bool[255];
    static readonly ConditionalWeakTable<Player,object> initialized=new();
    static bool Free(int slot)=>slot!=253&&slot!=Main.myPlayer&&!Main.player[slot].active&&!initializing[slot]&&
        !(leases[slot]!=null&&leases[slot].TryGetTarget(out var holder)&&
          holder.Cat is {active:true,ModProjectile:CompanionCat cat}&&ReferenceEquals(cat.Weapons,holder));
    internal static bool Initialized(Player actor)=>initialized.TryGetValue(actor,out _);
    // Utility actors don't run Player.Update, but native item hooks still read
    // ModPlayer state initialized by ResetEffects. Run that lifecycle once in
    // an unused, non-local slot, before use; never as part of drawing.
    internal static bool Initialize(Player actor){
        if(Initialized(actor))return true;
        if(Main.gameMenu)return false;
        for(int slot=Main.maxPlayers-1;slot>=0;slot--){
            if(!Free(slot))continue;
            var previous=Main.player[slot];int identity=actor.whoAmI;
            initializing[slot]=true;
            try{
                actor.whoAmI=slot;Main.player[slot]=actor;
                actor.ResetEffects();initialized.Add(actor,new object());return true;
            }finally{
                Main.player[slot]=previous;actor.whoAmI=identity;initializing[slot]=false;
            }
        }
        return false;
    }
    internal static bool Owns(CompanionNativeUse use,int slot)=>slot>=0&&slot<Main.maxPlayers&&
        leases[slot]!=null&&leases[slot].TryGetTarget(out var holder)&&ReferenceEquals(holder,use)&&
        (!Main.player[slot].active||ReferenceEquals(Main.player[slot],use.Actor));
    internal static int Acquire(CompanionNativeUse use) {
        for(int slot=Main.maxPlayers-1;slot>=0;slot--) {
            if(!Free(slot))continue;
            leases[slot]=new(use);return slot;
        }
        return -1;
    }
    internal static void Release(CompanionNativeUse use,int slot) {
        if(slot>=0&&slot<leases.Length&&leases[slot]!=null&&leases[slot].TryGetTarget(out var holder)&&ReferenceEquals(holder,use))leases[slot]=null;
    }
    static void Clear(){Array.Clear(leases);Array.Clear(initializing);initialized.Clear();}
    public override void OnWorldUnload()=>Clear();
    public override void Unload()=>Clear();
}
