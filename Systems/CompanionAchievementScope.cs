using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent.Achievements;
namespace PalicoCompanion.Systems;

// Native ItemCheck contains player-only achievement triggers (e.g. Terrarian).
// A companion is not a signed-in player; dedicated servers have no achievement
// manager. Keep the original path untouched for every actual human player.
public sealed class CompanionAchievementScope:ModSystem {
    public override void Load()=>On_AchievementsHelper.HandleSpecialEvent+=SpecialEvent;
    public override void Unload()=>On_AchievementsHelper.HandleSpecialEvent-=SpecialEvent;
    static void SpecialEvent(On_AchievementsHelper.orig_HandleSpecialEvent orig,Player player,int eventId){
        if(ReferenceEquals(player,CompanionNativeUse.Current?.Actor)||ReferenceEquals(player,CompanionFishing.Current?.Actor))return;
        orig(player,eventId);
    }
}
