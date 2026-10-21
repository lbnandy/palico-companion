using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.Default;

namespace PalicoCompanion.Systems;

// Only read removed save identities. No retired item is registered or spawnable.
internal static class LegacyTrainingMigration {
    internal static readonly Dictionary<string,int> Retired=new(){["FishingKit"]=11,["BuildingKit"]=12,["SlowHealModule"]=16,["SelfGuardModule"]=17,["SummonModule"]=18};
    static readonly Dictionary<int,int[]> Costs=new(){
        [11]=[ItemID.IronBar,6,ItemID.Cobweb,30,ItemID.Worm,3],
        [12]=[ItemID.Wood,50,ItemID.IronBar,8,ItemID.Rope,50],
        [16]=[ItemID.BeeWax,6,ItemID.BottledHoney,5],
        [17]=[ItemID.CobaltBar,6,ItemID.SoulofNight,3],
        [18]=[ItemID.SpiderFang,12,ItemID.SoulofNight,5]
    };
    internal static List<Item> Convert(Item item){
        var refunds=new List<Item>();
        int kind=-1;TrainingItem old=item?.ModItem as TrainingItem;
        bool retired=item?.ModItem is UnloadedItem missing&&missing.ModName=="PalicoCompanion"&&Retired.TryGetValue(missing.ItemName,out kind);
        if(!retired){if(old==null||!old.Installed||old.Kind is not (0 or 1 or 4))return refunds;kind=old.Kind;}
        int costKind=retired?kind:kind==0?16:kind==1?17:18;
        int count=Math.Max(1,item.stack);
        var costs=Costs[costKind];
        for(int i=0;i<costs.Length;i+=2){
            int type=costs[i]==-1?ItemID.IronBar:costs[i];
            int remaining=costs[i+1]*count;
            while(remaining>0){var refund=new Item(type);refund.stack=Math.Min(remaining,refund.maxStack);remaining-=refund.stack;refunds.Add(refund);}
        }
        if(retired)item.TurnToAir();else old.Installed=old.Alternate=false;
        return refunds;
    }
    internal static void Tick(CompanionPlayer s){
        if(Main.gameMenu||Main.GameUpdateCount%60!=0||ReferenceEquals(s.Player,CompanionNativeUse.Current?.Actor))return;
        var refunds=new List<Item>();
        if(s.Player.whoAmI==Main.myPlayer&&Main.netMode!=NetmodeID.Server&&!CompanionNetwork.Pending){
            foreach(var array in new[]{s.Player.inventory,s.Player.bank.item,s.Player.bank2.item,s.Player.bank3.item,s.Player.bank4.item})
                foreach(var item in array)refunds.AddRange(Convert(item));
            foreach(var refund in refunds)s.Player.QuickSpawnItem(s.Player.GetSource_Misc("LegacyPalicoMaterials"),refund,refund.stack);
            refunds.Clear();
        }
        if(CompanionNetwork.Client||CompanionNetwork.Server&&(!s.NetReady||s.NetLeaving))return;
        foreach(var item in s.Bag)refunds.AddRange(Convert(item));
        foreach(var item in s.Training.Slots)refunds.AddRange(Convert(item));
        if(refunds.Count==0)return;
        foreach(var refund in refunds){InventoryTransfer.Move(refund,s.Bag);if(!refund.IsAir)s.Player.QuickSpawnItem(s.Player.GetSource_Misc("LegacyPalicoMaterials"),refund,refund.stack);}
        if(CompanionNetwork.Server)CompanionNetwork.Send(s);
    }
}
