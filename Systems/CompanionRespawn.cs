using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
namespace PalicoCompanion.Systems;

internal static class CompanionRespawn {
    internal static bool Safe(Player owner){
        if(!owner.active||owner.dead)return false;
        var state=owner.GetModPlayer<CompanionPlayer>();
        Vector2 position=state.Cat?.Center??owner.Center;
        if(CompanionThreat.WorkDanger(owner,position))return false;
        foreach(var npc in Main.ActiveNPCs){
            if(npc.boss||npc.type is NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail)return false;
            // Recovery must not depend on attack mode, weapon range or line of sight.
            if(npc.CanBeChasedBy()&&(Vector2.DistanceSquared(npc.Center,owner.Center)<800*800||Vector2.DistanceSquared(npc.Center,position)<800*800))return false;
        }
        foreach(var shot in Main.ActiveProjectiles)
            if(shot.hostile&&shot.damage>0&&(Vector2.DistanceSquared(shot.Center,owner.Center)<800*800||Vector2.DistanceSquared(shot.Center,position)<800*800))return false;
        return true;
    }
}
