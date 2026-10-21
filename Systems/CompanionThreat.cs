using Microsoft.Xna.Framework;
using Terraria;
namespace PalicoCompanion.Systems;

// Work interruption and work selection must use the same visibility/range rule.
internal static class CompanionThreat {
    internal static bool InterruptsWork(NPC npc, Player owner, Vector2 cat) => (npc.CanBeChasedBy()||CompanionTargeting.Destructible(npc)) &&
        (Vector2.DistanceSquared(npc.Center,owner.Center)<240*240 && Reachability.Clear(owner.Center,npc.Center) ||
         Vector2.DistanceSquared(npc.Center,cat)<160*160 && Reachability.Clear(cat,npc.Center));
    internal static bool WorkDanger(Player owner,Vector2 cat) {
        if(owner.GetModPlayer<CompanionPlayer>().Cat?.ModProjectile is CompanionCat c&&c.HoldingThreat)return true;
        if(CompanionTargeting.Marked(owner,cat)!=null)return true;
        foreach(var npc in Main.npc)if(InterruptsWork(npc,owner,cat))return true;
        return false;
    }
}
