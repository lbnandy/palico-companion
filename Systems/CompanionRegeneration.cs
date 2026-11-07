using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Observe the real owner's normal update once for inheritance mode. Never
// replay the owner's hooks or import their recovery timer into the companion.
public sealed class CompanionRegeneration:ModSystem {
    sealed class Sample {internal int Bonus;internal long Pending;internal ulong Tick;internal bool Capturing;}
    static ConditionalWeakTable<Player,Sample> samples=new();
    Hook positiveRegen;
    delegate void RegenOriginal(Player player);
    public override void Load(){
        On_Player.UpdateLifeRegen+=Observe;
        positiveRegen=new Hook(typeof(PlayerLoader).GetMethod(nameof(PlayerLoader.UpdateLifeRegen),BindingFlags.Public|BindingFlags.Static)!,ObserveModBonus);
    }
    public override void Unload(){On_Player.UpdateLifeRegen-=Observe;positiveRegen?.Dispose();positiveRegen=null;samples=new();}
    static bool Owner(Player p)=>p!=CompanionNativeUse.Current?.Actor&&p!=CompanionFishing.Current?.Actor&&
        p.whoAmI>=0&&p.whoAmI<Main.maxPlayers&&ReferenceEquals(Main.player[p.whoAmI],p);
    static void Observe(On_Player.orig_UpdateLifeRegen orig,Player p){
        if(!Owner(p)){orig(p);return;}
        var sample=samples.GetOrCreateValue(p);
        // At entry, equipment/buffs have contributed but natural regeneration
        // and damage-over-time processing have not. Mod positive-regeneration
        // hooks run later; capture their net addition without copying debuffs.
        sample.Pending=Math.Max(0,p.lifeRegen);sample.Bonus=0;sample.Capturing=true;
        try {orig(p);sample.Bonus=(int)Math.Clamp(sample.Pending,0,100000);sample.Tick=Main.GameUpdateCount;}
        finally {sample.Capturing=false;}
    }
    static void ObserveModBonus(RegenOriginal orig,Player p){
        int before=p.lifeRegen;orig(p);
        if(samples.TryGetValue(p,out var sample)&&sample.Capturing)
            sample.Pending+=Math.Max(0,(long)p.lifeRegen-before);
    }
    internal static int Bonus(Player p)=>p.active&&!p.dead&&Owner(p)&&samples.TryGetValue(p,out var sample)&&
        Main.GameUpdateCount-sample.Tick<=1?sample.Bonus:0;
}
