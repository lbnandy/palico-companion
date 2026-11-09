using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent.Achievements;
namespace PalicoCompanion.Systems;

// Native ItemCheck contains player-only achievement triggers (e.g. Terrarian).
// A companion is not a signed-in player; dedicated servers have no achievement
// manager. Keep the original path untouched for every actual human player.
public sealed class CompanionAchievementScope:ModSystem {
    internal static bool IsCompanion(Player player)=>player!=null&&(ReferenceEquals(player,CompanionNativeUse.Current?.Actor)||ReferenceEquals(player,CompanionFishing.Current?.Actor));
    public override void Load()=>On_AchievementsHelper.HandleSpecialEvent+=SpecialEvent;
    public override void Unload()=>On_AchievementsHelper.HandleSpecialEvent-=SpecialEvent;
    static void SpecialEvent(On_AchievementsHelper.orig_HandleSpecialEvent orig,Player player,int eventId){
        if(IsCompanion(player))return;
        orig(player,eventId);
    }
}
