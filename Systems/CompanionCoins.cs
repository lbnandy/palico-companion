using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
namespace PalicoCompanion.Systems;

public static class CompanionCoins {
    static readonly long[] Values={1,100,10000,1000000};
    internal static bool Eligible(Item i)=>!i.IsAir&&!i.favorited&&i.ModItem is null&&i.prefix==0&&i.type>=ItemID.CopperCoin&&i.type<=ItemID.PlatinumCoin;
    internal static long Total(Item[] bag)=>bag.Where(Eligible).Sum(i=>i.stack*PickupCredit.Unit(i));
    internal static long Fresh(Item[] bag)=>bag.Where(Eligible).Sum(PickupCredit.Get);
    internal static Item[] Denominations(long value){var items=new List<Item>();for(int type=ItemID.PlatinumCoin;type>=ItemID.CopperCoin;type--){var model=new Item(type);long unit=PickupCredit.Unit(model),count=value/unit;value%=unit;while(count>0){int n=(int)Math.Min(count,model.maxStack);items.Add(new Item(type,n));count-=n;}}return items.ToArray();}
    internal static bool Repack(Item[] bag,long value,long fresh=0){
        var slots=Enumerable.Range(0,bag.Length).Where(i=>Eligible(bag[i])||bag[i].IsAir).ToArray();
        var plan=Denominations(value);if(plan.Length>slots.Length)return false;
        for(int i=0;i<slots.Length;i++){var item=i<plan.Length?plan[i]:new Item();long n=Math.Min(fresh,item.IsAir?0:item.stack*PickupCredit.Unit(item));PickupCredit.Set(item,n);fresh-=n;bag[slots[i]]=item;}return true;
    }
    internal static bool Add(Item[] bag,long value)=>Repack(bag,Total(bag)+value,Fresh(bag));
    internal static bool AddOwner(Item[] inventory,long value){
        int[] slots=Enumerable.Range(50,4).Concat(Enumerable.Range(0,50)).ToArray();var money=slots.Select(i=>inventory[i]).ToArray();
        if(!Add(money,value))return false;for(int i=0;i<slots.Length;i++)inventory[slots[i]]=money[i];return true;
    }
    internal static void Maintain(CompanionPlayer s){if(s.Gear[0].type!=ItemID.CoinGun)Normalize(s.Bag);}
    // Plan the entire change first. A full bag may lack room for the remainder;
    // in that case leave every original item alone. Favorites are never converted.
    public static bool Normalize(Item[] bag) {
        long total=0,fresh=Fresh(bag);var slots=new List<int>();var counts=new long[4];
        for(int i=0;i<bag.Length;i++)if(Eligible(bag[i])) {
            slots.Add(i);int denomination=bag[i].type-ItemID.CopperCoin;
            counts[denomination]+=bag[i].stack;total+=bag[i].stack*Values[denomination];
        }
        if(counts[0]<100&&counts[1]<100&&counts[2]<100)return false;
        for(int i=0;i<bag.Length;i++)if(bag[i].IsAir)slots.Add(i);
        var planned=new List<Item>();
        for(int d=3;d>=0;d--) {
            long count=total/Values[d];total%=Values[d];
            while(count>0) {
                if(planned.Count>=slots.Count)return false;
                var item=new Item(ItemID.CopperCoin+d);item.stack=(int)System.Math.Min(count,item.maxStack);
                count-=item.stack;planned.Add(item);
            }
        }
        for(int i=0;i<slots.Count;i++){var item=i<planned.Count?planned[i]:new Item();long n=Math.Min(fresh,item.IsAir?0:item.stack*PickupCredit.Unit(item));PickupCredit.Set(item,n);fresh-=n;bag[slots[i]]=item;}
        return true;
    }
}
