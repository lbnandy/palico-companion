using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
namespace PalicoCompanion.Systems;

internal static class CompanionFeeding {
    internal const int Duration=84,MaxFoodTime=216000;
    internal static bool Food(Item item)=>item is {IsAir:false,consumable:true,ModItem:null,stack:>0}&&item.type<ItemID.Count&&
        item.buffTime>0&&item.buffType is BuffID.WellFed or BuffID.WellFed2 or BuffID.WellFed3;
    internal static string Text(string key,params object[] args)=>Language.GetTextValue("Mods.PalicoCompanion.UI.Feed"+key,args);
    internal static string Message(string key)=>CompanionText.MessageFull("Mods.PalicoCompanion.UI.Feed"+key);
    internal static string Status(CompanionPlayer s)=>s.Vitals.FoodTime>0?Text("Effect",CompanionFoodDisplay.Time(s)):"";
    internal static string Unavailable(CompanionPlayer s){
        if(!s.Enabled||s.Stowed||s.Player.dead||s.Vitals.Downed||s.Cat?.ModProjectile is not CompanionCat cat)return "Unavailable";
        if(s.Vitals.FeedCooldown>0||cat.ActionVisual.Eating)return "Eating";
        if(Vector2.DistanceSquared(s.Player.Center,cat.Projectile.Center)>100*100||!Reachability.Clear(s.Player.Center,cat.Projectile.Center))return "Closer";
        if(s.Player.itemAnimation>0||s.Player.itemTime>0||s.Paused||s.PetCooldown>510||s.Directive.Active||s.Construction.Active||s.Digging.Active||s.Mining.Active||s.Trees.Active||s.Garden.Active||s.Order.Active||s.Storage.Active||
            cat.Kite.Active||cat.Barrel.Active||cat.Weapons.Busy||cat.Fishing.Active||cat.Supplies.Active||cat.Catching.Active||cat.Navigation.Riding||!cat.Navigation.Grounded(cat.Projectile)||s.Vitals.Immunity>0||CompanionThreat.WorkDanger(s.Player,cat.Projectile.Center))return "Busy";
        return null;
    }
    internal static bool CanRecover(CompanionPlayer s,CompanionCat cat)=>s.Vitals.FoodTime>0&&s.Vitals.Health<s.Vitals.MaxHealth&&s.Vitals.QuietTicks>=300&&
        s.Enabled&&!s.Stowed&&!s.Player.dead&&!s.Vitals.Downed&&cat!=null&&!cat.Weapons.Busy&&!CompanionThreat.WorkDanger(s.Player,cat.Projectile.Center);
    // The offered item is a real reserved unit in multiplayer, never a second
    // copy consumed from the player's inventory on receipt of a snapshot.
    internal static bool Accept(CompanionPlayer s,Item offered,out string message){
        if(CompanionNetwork.Client){message=Message("Unavailable");return false;}
        string reason=Food(offered)?Unavailable(s):"Invalid";
        if(reason!=null){message=Message(reason);return false;}
        var cat=(CompanionCat)s.Cat.ModProjectile;int type=offered.type;
        int duration=Math.Clamp(offered.buffTime,1,MaxFoodTime);
        if(duration>=s.Vitals.FoodTime)s.Vitals.FoodTime=s.Vitals.FoodDuration=duration;
        s.Vitals.FeedCooldown=Duration;
        if(--offered.stack==0)offered.TurnToAir();
        cat.FaceWork(s.Player.Center);cat.Projectile.direction=CompanionCat.Facing(cat.Projectile.Center,s.Player.Center,cat.Projectile.direction);
        cat.ActionVisual.Begin(cat,CompanionGesture.Eat,type);
        CompanionFeedback.Show(4,cat.Projectile.Center,type);
        string variant=type is ItemID.CookedFish or ItemID.CookedShrimp or ItemID.Sashimi or ItemID.SeafoodDinner?"FoodFish":
            type is ItemID.IceCream or ItemID.ChocolateChipCookie or ItemID.PumpkinPie?"FoodSweet":"Food";
        s.Expression.TrySay(s,cat.Projectile,"Pet",Terraria.GameContent.UI.EmoteID.EmotionLove,variant:variant);
        message=Message("Accepted");return true;
    }
    internal static void Request(CompanionPlayer s){
        if(s.Player.selectedItem is <0 or >=10||!Food(s.Player.HeldItem))return;
        string reason=Unavailable(s);
        if(reason!=null){s.Report(Message(reason));return;}
        if(CompanionNetwork.Client){CompanionNetwork.Feed(s);return;}
        if(Accept(s,s.Player.HeldItem,out string message)){Terraria.Recipe.FindRecipes();}s.Report(message);
    }
    internal static bool Hold(CompanionPlayer s,CompanionCat cat){
        if(!cat.ActionVisual.Eating)return false;
        if(s.Paused||s.PetCooldown>510||s.Vitals.Downed||s.Vitals.Immunity>0||s.Directive.Active||s.Construction.Active||s.Digging.Active||s.Mining.Active||s.Trees.Active||s.Garden.Active||s.Order.Active||s.Storage.Active||
            Vector2.DistanceSquared(s.Player.Center,cat.Projectile.Center)>160*160||CompanionThreat.WorkDanger(s.Player,cat.Projectile.Center)){
            cat.ActionVisual.Cancel(cat);return false;
        }
        cat.Weapons.Tick(s.Player,s,cat.Projectile,null,false);
        cat.Projectile.velocity.X*=.7f;cat.Projectile.velocity.Y=Math.Min(10,cat.Projectile.velocity.Y+.35f);
        cat.FaceWork(s.Player.Center);s.Status=Message("Status");return true;
    }
}
