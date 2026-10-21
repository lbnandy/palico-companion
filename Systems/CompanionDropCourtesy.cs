using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// A short hand-off window for intentional player throws, not mined/monster loot.
// Native item mod-data carries the remaining time to the server and observers.
// This does not reserve the item or prevent a human player from picking it up.
public sealed class CompanionDropCourtesy:GlobalItem {
    public override bool InstancePerEntity=>true;
    internal const int Duration=600;
    internal ulong until;
    internal int Remaining=>(int)Math.Min((ulong)Duration,until>Main.GameUpdateCount?until-Main.GameUpdateCount:0);
    internal static bool Protected(Item item)=>item.TryGetGlobalItem<CompanionDropCourtesy>(out var tag)&&tag.Remaining>0;
    public override void OnSpawn(Item item,IEntitySource source){until=source.Context=="ThrowItem"?Main.GameUpdateCount+Duration:0;}
    // Courtesy is only a ground-item policy, not inventory identity. Do not
    // carry a ticking field into subsequent cursor/equipment transactions.
    public override bool OnPickup(Item item,Player player){until=0;return true;}
    public override void NetSend(Item item,BinaryWriter writer)=>writer.Write((ushort)Remaining);
    public override void NetReceive(Item item,BinaryReader reader){int remaining=Math.Min(Duration,(int)reader.ReadUInt16());until=remaining>0?Main.GameUpdateCount+(uint)remaining:0;}
    public override void OnStack(Item destination,Item source,int amount){
        if(source.TryGetGlobalItem<CompanionDropCourtesy>(out var other))until=Math.Max(until,other.until);
    }
}
public sealed class CompanionDropCourtesyHooks:ModSystem {
    public override void Load()=>On_Item.NewItem_Inner+=Spawn;
    public override void Unload()=>On_Item.NewItem_Inner-=Spawn;
    static int Spawn(On_Item.orig_NewItem_Inner orig,IEntitySource source,int x,int y,int width,int height,Item item,int type,int stack,bool noBroadcast,int prefix,bool noGrabDelay,bool reverseLookup){
        // Observe the actual throw, after vanilla has selected cursor/inventory
        // and handled favorites. DropSelectedItem replaces the spawned clone
        // with this original object before sending it, so both must be marked.
        if(source.Context=="ThrowItem"&&item is {IsAir:false})
            item.GetGlobalItem<CompanionDropCourtesy>().until=Main.GameUpdateCount+CompanionDropCourtesy.Duration;
        return orig(source,x,y,width,height,item,type,stack,noBroadcast,prefix,noGrabDelay,reverseLookup);
    }
}
