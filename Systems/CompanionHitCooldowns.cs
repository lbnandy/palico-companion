using System;
using System.Buffers;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// A fresh GlobalNPC instance distinguishes respawns that reuse the same slot.
public sealed class CompanionNpcEpoch : GlobalNPC {
    public override bool InstancePerEntity=>true;
}

// Network actors share virtual player index 255, not a shared attack cooldown.
// Borrow only NPC.immune[actor], restore it even on exceptions, and age the
// companion's own counters by world ticks. Local/static projectile immunity is
// intentionally untouched. Nested scopes for the same actor are no-ops.
internal sealed class CompanionHitCooldowns {
    readonly Entry[] entries=new Entry[Main.maxNPCs];
    struct Entry {internal CompanionNpcEpoch Epoch;internal int Ticks;internal ulong At;}
    internal IDisposable Enter(int actor)=>new Borrow(this,actor);
    sealed class Borrow : IDisposable {
        readonly CompanionHitCooldowns bank;readonly int actor;
        readonly (NPC Npc,CompanionNpcEpoch Epoch,int Before)[] saved;
        int count;
        internal Borrow(CompanionHitCooldowns bank,int actor){
            this.bank=bank;this.actor=actor;
            saved=ArrayPool<(NPC,CompanionNpcEpoch,int)>.Shared.Rent(Main.maxNPCs);
            foreach(var n in Main.ActiveNPCs){
                var epoch=n.GetGlobalNPC<CompanionNpcEpoch>();ref var entry=ref bank.entries[n.whoAmI];
                saved[count++]=(n,epoch,n.immune[actor]);
                n.immune[actor]=ReferenceEquals(epoch,entry.Epoch)?Math.Max(0,entry.Ticks-(int)Math.Min((ulong)int.MaxValue,Main.GameUpdateCount-entry.At)):0;
            }
        }
        public void Dispose(){
            try {for(int i=0;i<count;i++){
                var (n,epoch,before)=saved[i];
                if(!ReferenceEquals(epoch,n.GetGlobalNPC<CompanionNpcEpoch>()))continue;
                bank.entries[n.whoAmI]=new(){Epoch=epoch,Ticks=n.immune[actor],At=Main.GameUpdateCount};
                n.immune[actor]=before;
            }}finally{ArrayPool<(NPC,CompanionNpcEpoch,int)>.Shared.Return(saved,clearArray:true);}
        }
    }
}
