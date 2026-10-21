using System;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.UI;
using Terraria.Audio;
using Terraria.ID;
namespace PalicoCompanion.Systems;
public static class CompanionSorting {
    // Stable's native core accepts arbitrary inventory sizes; public SortChest
    // assumes forty slots and the player's open chest, so never redirect it.
    static readonly Action<Item[],int[]> nativeSort=typeof(ItemSorting)
        .GetMethod("Sort",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(Item[]),typeof(int[])},null)
        ?.CreateDelegate<Action<Item[],int[]>>();
    internal static void Request(CompanionPlayer s){if(CompanionNetwork.Client){CompanionNetwork.SortBag(s);return;}if(Sort(s))new InventoryOutcome(InventoryCue.Tick).Present(s);}
    public static bool Sort(CompanionPlayer s){
        if(nativeSort==null){s.Report(CompanionText.MessageFull("Mods.PalicoCompanion.UI.SortUnavailable"));return false;}
        var copy=s.Bag.Select(i=>i.Clone()).ToArray();int previousOwner=Main.myPlayer;
        // Native sorting prioritizes weapon classes from Main.LocalPlayer's
        // damage modifiers. Dedicated servers otherwise read the dummy player.
        // Bound synchronous scope only; no UI input or player-array replacement.
        try{Main.myPlayer=s.Player.whoAmI;nativeSort(copy,Array.Empty<int>());}
        catch(Exception e){Terraria.ModLoader.ModContent.GetInstance<PalicoCompanion>().Logger.Warn("Native bag sort failed; original inventory retained",e);s.Report(CompanionText.MessageFull("Mods.PalicoCompanion.UI.SortUnavailable"));return false;}
        finally{Main.myPlayer=previousOwner;}
        s.Bag=copy;s.Report(CompanionText.MessageFull("Mods.PalicoCompanion.UI.SortDone"));return true;
    }
}
