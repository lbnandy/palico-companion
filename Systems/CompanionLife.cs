using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;
namespace PalicoCompanion.Systems;

// Character-owned identity and inventory. Never manufacture a second inventory.
public static class CompanionLife {
    public static string L(string key)=>Language.GetTextValue("Mods.PalicoCompanion.UI."+key);
    public static string World=>Main.ActiveWorldFileData?.UniqueId.ToString()??"";
    public static void Stop(CompanionPlayer s){
        s.Directive.Clear(s);s.Expression.ClearPendingWork();
        if(s.Cat?.ModProjectile is CompanionCat c)c.StopWork(s);
        s.Mining.Cancel();s.Digging.Clear(s);s.Garden.Cancel();s.Storage.Cancel();s.Trees.Cancel();s.Order.Cancel();
    }
    public static void Follow(CompanionPlayer s){if(s.Stowed&&s.Cat is {} cat&&!CompanionRestVisual.Hidden(s.Vitals))CompanionTransitEffects.At(cat.Center);Stop(s);s.Stowed=false;s.Paused=false;s.Enabled=CompanionNetwork.Server?s.NetEnabled:ModContent.GetInstance<CompanionConfig>().Enabled;s.Bonded=true;s.Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.Following");}
    public static void Toggle(CompanionPlayer s){if(CompanionNetwork.Client){CompanionNetwork.Action(0);return;}if(s.Stowed)Follow(s);else Stow(s);}
    public static void Stow(CompanionPlayer s){if(!s.Stowed&&s.Cat is {} cat&&!CompanionRestVisual.Hidden(s.Vitals))CompanionTransitEffects.At(cat.Center);Stop(s);s.Stowed=true;s.Enabled=false;s.Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.Stowed");}
}
