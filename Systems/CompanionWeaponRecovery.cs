using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace PalicoCompanion.Systems;

// The borrowed Player owns native healing projectiles; Vitals owns saved HP.
// Transfer only the change made inside an authoritative native-use scope.
internal static class CompanionWeaponRecovery {
    internal static int Begin(CompanionNativeUse use){
        var v=use.State.Vitals;var actor=use.Actor;
        actor.statLife=v.Health;actor.statLifeMax=actor.statLifeMax2=v.MaxHealth;
        actor.lifeSteal=v.LifeSteal;
        // Moon Leech targets human players. Respect the owner's restriction
        // rather than letting an unaddressable virtual player bypass the boss.
        actor.moonLeech=use.State.Player.moonLeech||actor.HasBuff(BuffID.MoonLeech);
        return v.Health;
    }
    internal static void End(CompanionNativeUse use,int before){
        if(CompanionNetwork.Client||!use.Available)return;
        var v=use.State.Vitals;
        v.LifeSteal=Math.Clamp(use.Actor.lifeSteal,-100000,Main.expertMode?70:80);
        int healed=Math.Clamp(use.Actor.statLife-before,0,v.MaxHealth-v.Health);
        if(healed<=0)return;
        v.Health+=healed;CompanionFeedback.Show(1,use.Cat.Center,healed,"skill");
    }
    internal static void LifeDrain(CompanionNativeUse use){
        if(CompanionNetwork.Client||!use.Available)return;
        var actor=use.Actor;int count=0;
        if(actor.HeldItem.type==ItemID.SoulDrain&&actor.itemAnimation>0)
            foreach(var n in Main.ActiveNPCs)if(n.soulDrain&&Vector2.DistanceSquared(n.Center,actor.position)<1100*1100)count++;
        actor.soulDrain=count;
        if(count==0)return;
        // NPC.UpdateLifeRegen locates human wielders in Main.player; our actor
        // is absent there between scopes. Match its count and Player's existing
        // Soul Drain regen term without running a full player update twice.
        int rate=(5+count)/2;var v=use.State.Vitals;
        v.QuietTicks=Math.Min(3600,v.QuietTicks+2+rate);
        if(v.Health>=v.MaxHealth)return;
        v.LifeCounter+=rate;v.Health=Math.Min(v.MaxHealth,v.Health+v.LifeCounter/120);v.LifeCounter%=120;
    }
}
