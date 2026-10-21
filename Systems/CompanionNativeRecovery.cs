using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;
public sealed partial class CompanionNativeUse {
    internal bool NativeDrink {get;private set;}
    internal int Heal(Player owner,CompanionPlayer state,Projectile cat,out int potion){
        Cat=cat;State=state;return Drink(false,0,out potion);
    }
    internal int Drink(bool mana,int required,out int potion){
        NativeDrink=false;potion=0;var v=State.Vitals;
        if(CompanionNetwork.Client||v.Downed||v.Health<=0||
            (mana?(required<=0||required>v.MaxMana||v.Mana>=required):(v.PotionDelay>0||v.Health>v.MaxHealth/2)))return 0;
        Func<Item,bool> valid=mana?CompanionRecovery.ManaPotion:CompanionRecovery.Supported;
        if(!EnsureActorSlot())return 0;
        if(!initialized){Actor.inventory=InventoryTransfer.Empty(59);Actor.ResetEffects();initialized=true;}
        Actor.dead=false;Actor.statMana=v.Mana;Actor.statManaMax=Actor.statManaMax2=v.MaxMana;
        Actor.statLife=v.Health;Actor.statLifeMax=Actor.statLifeMax2=v.MaxHealth;Actor.potionDelay=v.PotionDelay;
        int before=mana?v.Mana:v.Health;
        var inventory=Actor.inventory;int selected=Actor.selectedItem;
        try{
            using(new Scope(this,false)){
                int missing=mana?v.MaxMana-v.Mana:v.MaxHealth-v.Health,score=int.MaxValue;Item chosen=null;
                foreach(var item in State.Bag){
                    if(!valid(item)||!CombinedHooks.CanUseItem(Actor,item))continue;
                    int dose=mana?Actor.GetHealMana(item,true):Actor.GetHealLife(item,true);
                    if(dose<=0||mana&&dose<required-v.Mana)continue;
                    int rank=dose>=missing?dose-missing:1000000-dose;
                    if(rank<score){score=rank;chosen=item;}
                }
                if(chosen==null)return 0;
                potion=chosen.type;
                Actor.inventory=InventoryTransfer.Empty(59);Actor.inventory[0]=chosen;Actor.selectedItem=0;
                // Actual tML/native use/consume hooks, cooldowns and ModPlayer
                // hooks run on the companion, never the human or their banks.
                if(mana){
                    int sickness=Actor.FindBuffIndex(BuffID.ManaSickness);
                    if(sickness>=0)Actor.buffTime[sickness]=v.ManaSickness;
                    else if(v.ManaSickness>0)Actor.AddBuff(BuffID.ManaSickness,v.ManaSickness);
                    Actor.QuickMana();
                }else Actor.QuickHeal();
            }
            int restored=Math.Max(0,(mana?Actor.statMana:Actor.statLife)-before);
            if(restored==0)return 0;
            NativeDrink=true;
            if(mana){v.Mana=Math.Min(v.MaxMana,Actor.statMana);int index=Actor.FindBuffIndex(BuffID.ManaSickness);v.ManaSickness=index>=0?Actor.buffTime[index]:0;}
            else{v.Health=Math.Min(v.MaxHealth,Actor.statLife);v.PotionDelay=Actor.potionDelay;}
            return restored;
        }finally{
            Actor.inventory=inventory;Actor.selectedItem=selected;
            // QuickHeal/QuickMana refresh native recipes while the temporary
            // actor is current. Restore the human's visible recipe cache too.
            if(Main.netMode==NetmodeID.SinglePlayer&&Current!=this)Recipe.FindRecipes();
        }
    }
}
