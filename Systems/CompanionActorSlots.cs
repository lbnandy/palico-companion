using System;
using Terraria;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Process-local native ItemCheck actors, NOT network player identities. A lease
// must never evict a real player, and two companions must not share hit immunity
// or native owner-indexed projectile state. Fishing still reserves slot 253.
internal sealed class CompanionActorSlots : ModSystem {
    static readonly WeakReference<CompanionNativeUse>[] leases=new WeakReference<CompanionNativeUse>[255];
    internal static bool Owns(CompanionNativeUse use,int slot)=>slot>=0&&slot<Main.maxPlayers&&
        leases[slot]!=null&&leases[slot].TryGetTarget(out var holder)&&ReferenceEquals(holder,use)&&
        (!Main.player[slot].active||ReferenceEquals(Main.player[slot],use.Actor));
    internal static int Acquire(CompanionNativeUse use) {
        for(int slot=Main.maxPlayers-1;slot>=0;slot--) {
            if(slot==253||slot==Main.myPlayer||Main.player[slot].active)continue;
            if(leases[slot]!=null&&leases[slot].TryGetTarget(out var holder)&&
                holder.Cat is {active:true}&&holder.Cat.ModProjectile is CompanionCat cat&&ReferenceEquals(cat.Weapons,holder))continue;
            leases[slot]=new(use);return slot;
        }
        return -1;
    }
    internal static void Release(CompanionNativeUse use,int slot) {
        if(slot>=0&&slot<leases.Length&&leases[slot]!=null&&leases[slot].TryGetTarget(out var holder)&&ReferenceEquals(holder,use))leases[slot]=null;
    }
    public override void OnWorldUnload()=>Array.Clear(leases);
    public override void Unload()=>Array.Clear(leases);
}
