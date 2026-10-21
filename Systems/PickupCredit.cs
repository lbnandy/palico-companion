using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
namespace PalicoCompanion.Systems;

// Provenance belongs to the stack, not its slot or item type. Native stack and
// split hooks preserve it through sorting, ItemIO saves and transaction receipts.
// Coins use copper units so denomination changes never manufacture eligibility.
public sealed class PickupCredit:GlobalItem {
    public override bool InstancePerEntity=>true;
    long credit;int observed;
    internal static long Unit(Item i)=>i.type switch {ItemID.SilverCoin=>100,ItemID.GoldCoin=>10000,ItemID.PlatinumCoin=>1000000,_=>1};
    internal static long Get(Item i){if(i==null||i.IsAir)return 0;var g=i.GetGlobalItem<PickupCredit>();g.credit=Math.Clamp(g.credit-Math.Max(0,g.observed-i.stack)*Unit(i),0,i.stack*Unit(i));g.observed=i.stack;return g.credit;}
    internal static void Set(Item i,long value){if(i==null||i.IsAir)return;var g=i.GetGlobalItem<PickupCredit>();g.credit=Math.Clamp(value,0,i.stack*Unit(i));g.observed=i.stack;}
    internal static void Clear(Item i)=>Set(i,0);
    public override void OnStack(Item destination,Item source,int numToTransfer){
        long own=Get(destination),from=Get(source),moved=Math.Min(from,numToTransfer*Unit(source));
        credit=own+moved;observed=destination.stack+numToTransfer;
        var other=source.GetGlobalItem<PickupCredit>();other.credit=from-moved;other.observed=source.stack-numToTransfer;
    }
    public override void SplitStack(Item destination,Item source,int numToTransfer){
        long from=Get(source),moved=Math.Min(from,numToTransfer*Unit(source));credit=moved;observed=numToTransfer;
        var other=source.GetGlobalItem<PickupCredit>();other.credit=from-moved;other.observed=source.stack-numToTransfer;
    }
    public override void SaveData(Item item,TagCompound tag){long n=Get(item);if(n>0)tag["freshPickup"]=n;}
    public override void LoadData(Item item,TagCompound tag)=>Set(item,tag.GetLong("freshPickup"));
    public override void NetSend(Item item,BinaryWriter writer)=>writer.Write(Get(item));
    public override void NetReceive(Item item,BinaryReader reader)=>Set(item,reader.ReadInt64());
}
