using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Keep the native formula and the real owner's ModPlayer fishing hooks. Only
// substitute the pole and bait actually used by this cast; never swap inventory.
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
        var previous=evaluating;var previousPole=pole;var previousBait=bait;int previousSkill=s.Player.fishingSkill;
        try {
            evaluating=s.Player;pole=rod??CompanionFishing.BestRod(s.Player);
            bait=actualBait??s.Bag.Where(CompanionFishing.SafeBait).OrderByDescending(i=>i.bait).FirstOrDefault();
            s.Player.fishingSkill+=s.Training.Work(10)?5*(s.Training.Tier+1):0;
            return s.Player.GetFishingConditions();
        } finally {s.Player.fishingSkill=previousSkill;evaluating=previous;pole=previousPole;bait=previousBait;}
    }
}
