using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
namespace PalicoCompanion.Systems;

// New pickups are shared opportunistically. This never creates a movement job.
internal static class CompanionDelivery {
    internal static bool Near(CompanionPlayer s)=>s.Enabled&&!s.Paused&&!s.Stowed&&!s.Vitals.Downed&&!s.Player.dead&&s.Cat is {} cat&&Vector2.DistanceSquared(cat.Center,s.Player.Center)<=160*160;
    internal static int Preference=>ModContent.GetInstance<CompanionConfig>().CoinDestination switch {CoinDestinationMode.Bank=>1,CoinDestinationMode.Bag=>2,_=>0};
    internal static bool CanOffer(CompanionPlayer s){
        bool share=ModContent.GetInstance<CompanionConfig>().ShareNewPickups;
        return s.Bag.Any(i=>!i.IsAir&&!i.favorited&&PickupCredit.Get(i)>0&&(i.IsACoin?Preference!=2&&s.Gear[0].type!=ItemID.CoinGun:share&&CompanionRestock.Surplus(s,i)>0&&(UpgradeNeed(s.Player,i,s.Player.inventory)>0||i.maxStack>1&&s.Player.inventory.Take(58).Any(t=>!t.IsAir&&t.type==i.type&&t.stack<t.maxStack&&ItemLoader.CanStack(t,i)))));
    }
    internal static int MoveToStacks(Item item,Item[] target,int limit=int.MaxValue){
        int before=item.stack;foreach(var stack in target){if(item.IsAir||before-item.stack>=limit)break;if(stack.IsAir||stack.type!=item.type||stack.stack>=stack.maxStack||!ItemLoader.CanStack(stack,item))continue;ItemLoader.StackItems(stack,item,out _,false,Math.Min(item.stack,Math.Min(limit-(before-item.stack),stack.maxStack-stack.stack)));}
        int moved=before-Math.Max(0,item.stack);if(item.stack<=0)item.TurnToAir();return moved;
    }
    internal static int UpgradeNeed(Player p,Item i,Item[] inventory){int need=i.type switch {ItemID.LifeCrystal=>Math.Max(0,(400-p.statLifeMax)/20),ItemID.LifeFruit=>p.statLifeMax>=400?Math.Max(0,(500-p.statLifeMax)/5):0,ItemID.ManaCrystal=>Math.Max(0,(200-p.statManaMax)/20),_=>0};return Math.Max(0,need-inventory.Where(x=>x.type==i.type&&!x.IsAir).Sum(x=>x.stack));}
    static int MoveUpgrade(Item item,Item[] inventory,int limit){var normal=inventory.Take(50).ToArray();int n=InventoryTransfer.Move(item,normal,limit);Array.Copy(normal,inventory,50);return n;}
    internal static Item[] Plan(CompanionPlayer s,Item[] inventory,Item[] bank,bool share,int coins){
        var result=new List<Item>();
        if(share)foreach(var item in s.Bag){
            int fresh=(int)Math.Min(int.MaxValue,PickupCredit.Get(item));if(item.IsAir||item.favorited||item.IsACoin||fresh<=0)continue;
            int limit=Math.Min(fresh,CompanionRestock.Surplus(s,item));if(limit<=0)continue;
            var copy=item.Clone();copy.stack=Math.Min(copy.stack,limit);PickupCredit.Clear(copy);
            int need=UpgradeNeed(s.Player,copy,inventory);
            int moved=need>0?MoveUpgrade(copy,inventory,Math.Min(limit,need)):copy.maxStack>1?MoveToStacks(copy,inventory,limit):0;
            if(moved<=0)continue;
            var given=ItemLoader.TransferWithLimit(item,moved);PickupCredit.Clear(given);result.Add(given);if(item.stack<=0)item.TurnToAir();
        }
        if(coins!=2&&s.Gear[0].type!=ItemID.CoinGun){
            long fresh=CompanionCoins.Fresh(s.Bag);if(fresh>0){
                // A bank with only a gold slot can take whole gold while the
                // silver/copper change stays with the cat. Never force a new slot.
                var destination=coins==1?bank:inventory;
                foreach(long amount in new[]{fresh,fresh/100*100,fresh/10000*10000,fresh/1000000*1000000}.Distinct().Where(n=>n>0)){
                    var source=s.Bag.Select(i=>i.Clone()).ToArray();var target=destination.Select(i=>i.Clone()).ToArray();
                    if(CompanionCoins.Repack(source,CompanionCoins.Total(source)-amount,fresh-amount)&&(coins==1?CompanionCoins.Add(target,amount):CompanionCoins.AddOwner(target,amount))){
                        Array.Copy(source,s.Bag,source.Length);Array.Copy(target,destination,target.Length);result.AddRange(CompanionCoins.Denominations(amount));break;
                    }
                }
            }
        }
        if(result.Count>0&&s.Cat?.ModProjectile is CompanionCat cat)cat.ActionVisual.Begin(cat,CompanionGesture.Deliver,result[0].type);
        return result.ToArray();
    }
    internal static void Receive(CompanionPlayer s,Item[] items,bool bank){
        int moved=0;bool money=false,upgrade=false;var inventory=s.Player.inventory.Take(58).ToArray();
        foreach(var item in items){if(item.IsAir)continue;var display=item.Clone();PickupCredit.Clear(item);int before=item.stack;
            if(item.IsACoin){long value=item.stack*PickupCredit.Unit(item);if(bank?CompanionCoins.Add(s.Player.bank.item,value):CompanionCoins.AddOwner(inventory,value))item.TurnToAir();}
            else if(UpgradeNeed(s.Player,item,inventory)>0)MoveUpgrade(item,inventory,UpgradeNeed(s.Player,item,inventory));
            else MoveToStacks(item,inventory);
            int n=before-(item.IsAir?0:item.stack);moved+=n;money|=n>0&&display.IsACoin;upgrade|=n>0&&display.type is ItemID.LifeCrystal or ItemID.LifeFruit or ItemID.ManaCrystal;
            if(n>0&&!Main.dedServ){display.Center=s.Player.Center;PopupText.NewText(PopupTextContext.RegularItemPickup,display,n);}
            if(!item.IsAir){PickupCredit.Set(item,item.stack*PickupCredit.Unit(item));s.DeliveryRemainder.Add(item);}
        }
        Array.Copy(inventory,s.Player.inventory,58);
        if(moved>0){SoundEngine.PlaySound(money?SoundID.CoinPickup:SoundID.Grab,s.Player.Center);Recipe.FindRecipes();s.Report(bank&&money?CompanionText.Message("CompanionDelivery.CoinsBanked"):CompanionText.Message("CompanionDelivery.ItemsDelivered"));
            if(upgrade&&s.Cat is {} cat)s.Expression.TrySay(s,cat,"Delivered",Terraria.GameContent.UI.EmoteID.ItemLifePotion,variant:"Upgrade");
        }
        if(CompanionNetwork.Client)for(int i=0;i<58;i++)NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,s.Player.whoAmI,i);
    }
    internal static void Tick(CompanionPlayer s){
        if(s.Player.whoAmI!=Main.myPlayer||Main.dedServ)return;
        // Manually deposited player/cursor stacks can never inherit pickup credit.
        foreach(var item in s.Player.inventory)PickupCredit.Clear(item);PickupCredit.Clear(Main.mouseItem);
        if(++s.DeliveryTicks%90!=0)return;
        if(CompanionNetwork.Client&&CompanionNetwork.Pending){CompanionNetwork.RetryAutomatic();return;}
        if(!Near(s))return;
        if(CompanionNetwork.Client){CompanionNetwork.Deliver(s);return;}
        foreach(var item in s.DeliveryRemainder)InventoryTransfer.Move(item,s.Bag);s.DeliveryRemainder.RemoveAll(i=>i.IsAir);
        if(s.DeliveryRemainder.Count>0)return;
        var inventory=s.Player.inventory.Take(58).Select(i=>i.Clone()).ToArray();var bank=s.Player.bank.item.Select(i=>i.Clone()).ToArray();
        Receive(s,Plan(s,inventory,bank,ModContent.GetInstance<CompanionConfig>().ShareNewPickups,Preference),Preference==1);
        foreach(var item in s.DeliveryRemainder)InventoryTransfer.Move(item,s.Bag);s.DeliveryRemainder.RemoveAll(i=>i.IsAir);
    }
}
