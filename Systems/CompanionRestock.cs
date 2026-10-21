using System;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

public static class CompanionRestock {
    // Player.PurgeDD2EnergyCrystals removes these temporary event resources,
    // including favorites. A separate companion bag must not preserve them.
    internal static void ExpireEventResources(CompanionPlayer s){
        if(CompanionNetwork.Client||Terraria.GameContent.Events.DD2Event.Ongoing)return;
        foreach(var item in s.Bag)if(item.type==ItemID.DD2EnergyCrystal)item.TurnToAir();
    }
    // Personal-use resources must not bounce back into a matching chest on the
    // next visit. Favorites are also protected by the transfer layer.
    public static bool Keep(Item i,CompanionPlayer state=null)=>state!=null&&(Thrown(state,i)||(CompanionCrossMod.UsesPerMaterial?.Invoke(state.Gear[0],i)??0)>0)||CompanionLighting.Torch(i)||Logistics.IsSupply(i.type)||GardenAssist.IsSeed(i.type)||TreeSpecies.IsSeed(i.type)||
        CompanionRecovery.Supported(i)||CompanionRecovery.ManaPotion(i)||CompanionFishing.SafeBait(i)||i.ammo>0||CompanionLighting.Glowstick(i)||i.type==ItemID.DD2EnergyCrystal;
    static bool Ammo(CompanionPlayer s,Item i)=>!i.IsAir&&s.Gear[0].useAmmo>0&&i.ammo==s.Gear[0].useAmmo&&ItemLoader.CanChooseAmmo(s.Gear[0],i,s.Player);
    internal static int Infinite(Item i)=>CompanionCrossMod.InfiniteStack?.Invoke(i)??0;
    internal static bool Thrown(CompanionPlayer s,Item i)=>!i.IsAir&&s.Gear[0].consumable&&s.Gear[0].shoot>ProjectileID.None&&i.type==s.Gear[0].type;
    internal static Item[] Destination(CompanionPlayer s,Item i)=>Thrown(s,i)?new[]{s.Gear[0]}:s.Bag;
    static int Goal(int configured,Item i)=>configured==0?0:Math.Max(configured,Infinite(i));
    // Merge only compatible, unstarred ammunition; native StackItems preserves
    // mod data and pickup credit. A split total is not an infinite ammo stack.
    internal static void Maintain(CompanionPlayer s){
        if(CompanionNetwork.Client)return;
        var first=s.Bag.FirstOrDefault(i=>!i.IsAir&&!i.favorited&&Ammo(s,i));
        if(first!=null&&Infinite(first)>0&&first.stack<Infinite(first))
            foreach(var other in s.Bag){
                if(ReferenceEquals(first,other)||other.IsAir||other.favorited||other.type!=first.type||!ItemLoader.CanStack(first,other))continue;
                ItemLoader.StackItems(first,other,out _,false,Math.Min(other.stack,Math.Max(0,Infinite(first)-first.stack)));
                if(other.stack<=0)other.TurnToAir();if(first.stack>=Infinite(first))break;
            }
        if(s.Gear[0].favorited)return;
        foreach(var item in s.Bag)if(!item.IsAir&&!item.favorited&&Thrown(s,item)){
            int goal=Goal(CompanionStock.For(s).Ammo,item);
            InventoryTransfer.Move(item,new[]{s.Gear[0]},Math.Max(0,goal-s.Gear[0].stack));
        }
    }
    static (int low,int cap,Func<Item,bool> match) Policy(CompanionPlayer s,Item i){
        var stock=CompanionStock.For(s);
        (int,int,Func<Item,bool>) Target(int cap,Func<Item,bool> match)=>(Math.Max(1,cap/3),cap,match);
        // Variants share a category budget; prefer what the cat already carries.
        if(CompanionLighting.Torch(i))return Target(stock.Torches,CompanionLighting.Torch);
        if(Thrown(s,i))return (Infinite(i)>0?Infinite(i):Math.Max(1,stock.Ammo/3),Goal(stock.Ammo,i),x=>x.type==i.type);
        if(CompanionLighting.Glowstick(i))return Target(stock.Glowsticks,CompanionLighting.Glowstick);
        if(i.type==ItemID.WoodPlatform)return Target(stock.Platforms,x=>x.type==ItemID.WoodPlatform);
        if(i.type==ItemID.Rope)return Target(stock.Ropes,x=>x.type==ItemID.Rope);
        if(CompanionRecovery.Supported(i))return Target(stock.Healing,CompanionRecovery.Supported);
        if(s.Gear[0].mana>0&&CompanionRecovery.ManaPotion(i))return Target(stock.Mana,CompanionRecovery.ManaPotion);
        if(Ammo(s,i)){
            if(stock.Ammo==0||s.Bag.Any(x=>!x.IsAir&&!x.favorited&&Ammo(s,x)&&!x.consumable))return (0,0,_=>false);
            var preferred=s.Bag.FirstOrDefault(x=>!x.IsAir&&!x.favorited&&Ammo(s,x))??i;
            if(Infinite(preferred)>0){
                if(preferred.type!=i.type||!ItemLoader.CanStack(preferred,i))return (0,0,_=>false);
                int cap=Goal(stock.Ammo,i);
                // Refill even one below the threshold, not only below one third.
                return (Infinite(i),cap,x=>x.type==i.type&&ItemLoader.CanStack(preferred,x));
            }
            return Target(stock.Ammo,x=>Ammo(s,x));
        }
        int uses=CompanionCrossMod.UsesPerMaterial?.Invoke(s.Gear[0],i)??0;
        if(uses>0)return Target((stock.Ammo+uses-1)/uses,x=>(CompanionCrossMod.UsesPerMaterial?.Invoke(s.Gear[0],x)??0)>0);
        if((CompanionNetwork.Server?s.NetForestry:ModContent.GetInstance<CompanionConfig>().Forestry)&&i.type==ItemID.Acorn)return Target(stock.Acorns,x=>x.type==ItemID.Acorn);
        if((CompanionNetwork.Server?s.NetFishing:ModContent.GetInstance<CompanionConfig>().Fishing)&&CompanionFishing.SafeBait(i))return Target(stock.Bait,CompanionFishing.SafeBait);
        return (0,0,_=>false);
    }
    public static int Wanted(CompanionPlayer s,Item i,bool onlyLow=false){
        if(i.IsAir||i.favorited||Thrown(s,i)&&s.Gear[0].favorited)return 0;
        var policy=Policy(s,i);if(policy.cap==0)return 0;
        long held=0;
        foreach(var owned in Thrown(s,i)?s.Bag.Concat(new[]{s.Gear[0]}):s.Bag){
            if(owned.IsAir||owned.favorited||!policy.match(owned))continue;
            if(owned.ammo>0&&!owned.consumable)return 0; // Endless ammunition already covers the need.
            held+=owned.stack;
        }
        if(onlyLow&&held>=policy.low)return 0;
        return (int)Math.Max(0,Math.Min(policy.cap-held,i.ammo>0&&!i.consumable?1:int.MaxValue));
    }
    internal static int Surplus(CompanionPlayer s,Item item){
        // Keep every already-infinite stack intact even after changing weapons
        // or lowering stock settings; only newly picked excess can be offered.
        int threshold=Infinite(item);
        if(threshold>0&&item.stack>=threshold)return Math.Min(Math.Max(0,item.stack-threshold),SurplusCore(s,item));
        return SurplusCore(s,item);
    }
    static int SurplusCore(CompanionPlayer s,Item item){
        if(item.type==ItemID.DD2EnergyCrystal)return 0;
        if(GardenAssist.IsSeed(item.type)||TreeSpecies.IsSeed(item.type)&&item.type!=ItemID.Acorn)return 0;
        var policy=Policy(s,item);if(policy.cap<=0)return item.stack;
        long held=(Thrown(s,item)?s.Bag.Concat(new[]{s.Gear[0]}):s.Bag).Where(i=>!i.IsAir&&!i.favorited&&policy.match(i)).Sum(i=>(long)i.stack);
        return (int)Math.Clamp(held-policy.cap,0,item.stack);
    }
    public static System.Collections.Generic.IEnumerable<Item> Candidates(CompanionPlayer s,Chest c)=>c.item.OrderByDescending(i=>!i.IsAir&&s.Bag.Any(held=>!held.IsAir&&!held.favorited&&held.type==i.type));
    public static bool Needs(CompanionPlayer s,Chest c)=>c.item.Any(i=>Wanted(s,i,true)>0&&InventoryTransfer.HasRoom(i,Destination(s,i)));
}
