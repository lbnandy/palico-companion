using System;
using System.Globalization;
using System.Linq;
using Terraria;

namespace PalicoCompanion.Systems;

// One progression table serves gameplay, the guide and equipment tooltips.
internal sealed record TrainingTier(
    string Key,int Upgrade,float Mining,int Reach,float Wood,float Herbs,int Fishing,
    float Loot,float BossLoot,int Honey,int HoneyOverTime,int Shield,int CleanseTicks,
    int Mana,int ManaSaving,int DrumDamage,int DrumSpeed,int DrumSummon,int Pierce,
    int MarkSeconds,int FlightTicks,int Bomb,int CleanseCooldown,int BombCooldown,int BarrelCooldown);

internal static class TrainingTiers {
    internal const int Last=4, SaveFormat=1;
    static readonly TrainingTier[] tiers=[
        new("Basic",-1,20,1,8,20,5,10,5,15,5,15,120,20,10,20,10,25,4,15,36,120,40,18,30),
        new("Reinforced",21,22,1,10,25,8,11,6,22,8,22,150,30,12,22,12,30,6,16,45,320,39,18,29),
        new("Improved",14,25,2,12,30,10,12,7.5f,30,10,30,180,40,15,25,15,35,8,18,54,480,38,17,28),
        new("Masterwork",15,30,3,16,40,15,15,10,50,15,40,240,60,20,30,20,45,12,20,72,1200,35,16,26),
        new("Celestial",22,35,4,20,50,20,18,12.5f,70,20,50,300,80,25,35,25,50,16,22,90,1800,32,15,24)
    ];
    internal static TrainingTier At(int tier)=>tiers[Math.Clamp(tier,0,Last)];
    internal static int UpgradeTier(int kind)=>Array.FindIndex(tiers,t=>t.Upgrade==kind&&kind>=0);
    internal static bool Upgrade(int kind)=>kind==13||UpgradeTier(kind)>0;
    internal static int LoadTier(int value,int format)=>format<SaveFormat?Math.Clamp(value,0,2) switch {1=>2,2=>3,_=>0}:Math.Clamp(value,0,Last);
    internal static int Honey(int tier,int maximum)=>At(tier).Honey+(tier==Last?Math.Max(0,maximum-500)/10:0);
    internal static int HoneyOverTime(int tier,int maximum)=>At(tier).HoneyOverTime+(tier==Last?Math.Max(0,maximum-500)/50:0);
    internal static int Pierce(int tier){
        if(tier!=Last)return At(tier).Pierce;
        float progress=WorldPower;
        return progress>=6?32:progress>=4?28:progress>=3?24:progress>=2?20:progress>=1.5f?18:At(tier).Pierce;
    }
    internal static int Shield(int tier,int maximum)=>At(tier).Shield+(tier==Last?(int)(Math.Max(0,maximum-500)*.08):0);
    internal static int Mana(int tier,int maximum)=>At(tier).Mana+(tier==Last?Math.Max(0,maximum-200)/5:0);
    internal static float WorldPower {
        get {
            float value=1;
            if(CompanionCrossMod.SupportProgression!=null)
                foreach(Func<float> milestone in CompanionCrossMod.SupportProgression.GetInvocationList())value=Math.Max(value,milestone());
            return value;
        }
    }
    internal static int BombDamage(int tier)=>tier==0?(Main.hardMode?240:120):(int)(At(tier).Bomb*(tier==Last?WorldPower:1));
    internal static int Cooldown(int kind,int tier){var t=At(tier);return kind switch {
        0=>60,1 or 4=>45,2=>t.CleanseCooldown,3=>35,5=>25,6=>30,7=>t.BombCooldown,TrainingCatalog.Barrel=>t.BarrelCooldown,_=>0
    };}
    internal static object[] Values(int kind,int tier){
        var t=At(tier);
        string N(float n)=>n.ToString("0.##",CultureInfo.InvariantCulture);
        string P(float n)=>N(n)+"%";
        return kind switch {
            0=>[t.Honey,t.HoneyOverTime,t.Honey+t.HoneyOverTime],
            1=>[t.Shield],2=>[N(t.CleanseTicks/60f)],3=>[t.Mana,P(t.ManaSaving)],
            4=>[P(t.DrumDamage),P(t.DrumSpeed),P(t.DrumSummon)],5=>[Pierce(tier),t.MarkSeconds],
            6=>[N(t.FlightTicks/60f)],7=>[BombDamage(tier)],TrainingCatalog.Barrel=>[BombDamage(tier)*2],
            8=>[P(t.Loot),P(t.BossLoot)],9=>[P(t.Mining),t.Reach],10=>[P(t.Wood),P(t.Herbs),t.Fishing],_=>[]
        };
    }
    internal static object[] CompareValues(int kind){
        var values=Enumerable.Range(0,Last+1).Select(t=>Values(kind,t)).ToArray();
        return Enumerable.Range(0,values[0].Length).Select(i=>(object)string.Join(" / ",values.Select(v=>v[i]))).ToArray();
    }
}
