using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Keep the native formula and the selected attribute source's ModPlayer hooks.
// Substitute only this cast's pole and bait; never swap inventory.
public sealed class CompanionFishingPower:ModSystem {
    static Player evaluating;
    static Item pole,bait;
    public override void Load(){On_Player.GetFishingConditions+=Conditions;On_Player.Fishing_GetBestFishingPole+=Pole;On_Player.Fishing_GetBait+=Bait;}
    public override void Unload(){On_Player.GetFishingConditions-=Conditions;On_Player.Fishing_GetBestFishingPole-=Pole;On_Player.Fishing_GetBait-=Bait;}
    static void Pole(On_Player.orig_Fishing_GetBestFishingPole orig,Player p,out Item value){if(ReferenceEquals(p,evaluating))value=pole;else orig(p,out value);}
    static void Bait(On_Player.orig_Fishing_GetBait orig,Player p,out Item value){if(ReferenceEquals(p,evaluating))value=bait;else orig(p,out value);}
    static PlayerFishingConditions Conditions(On_Player.orig_GetFishingConditions orig,Player p){
        var c=CompanionFishing.Current;
        return c!=null&&ReferenceEquals(p,c.Actor)?Calculate(c.State,c.Rod,c.Actor.inventory[54]):orig(p);
    }
    internal static PlayerFishingConditions Calculate(CompanionPlayer s,Item rod=null,Item actualBait=null){
        var use=CompanionWorkAttributes.Context(s);var actor=use?.Actor??s.Player;
        using var scope=use==null?null:new CompanionNativeUse.Scope(use,trackHealing:false);
        var previous=evaluating;var previousPole=pole;var previousBait=bait;int previousSkill=actor.fishingSkill;
        try {
            evaluating=actor;pole=rod??CompanionFishing.BestRod(s.Player);
            bait=actualBait??s.Bag.Where(CompanionFishing.SafeBait).OrderByDescending(i=>i.bait).FirstOrDefault();
            actor.fishingSkill+=s.Training.Work(10)?5*(s.Training.Tier+1):0;
            return actor.GetFishingConditions();
        } finally {actor.fishingSkill=previousSkill;evaluating=previous;pole=previousPole;bait=previousBait;}
    }
}
