using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.UI;
namespace PalicoCompanion.Systems;

// A real death event, not disappearance/despawn. Multi-part vanilla bosses
// celebrate only when the encounter's last living part has been defeated.
public sealed class CompanionBossVictory:GlobalNPC {
    internal static bool FinalBoss(NPC npc){
        if(CompanionSpeechContent.Find(npc) is {} boss)return boss.Final(npc);
        bool worm=npc.type is NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail;
        if(!npc.boss&&!worm)return false;
        if(worm)return !Main.npc.Any(n=>n!=npc&&n.active&&n.life>0&&n.type is NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail);
        if(npc.type is NPCID.Retinazer or NPCID.Spazmatism)return !Main.npc.Any(n=>n!=npc&&n.active&&n.life>0&&n.type is NPCID.Retinazer or NPCID.Spazmatism);
        return true;
    }
    public override void OnKill(NPC npc){
        if(CompanionNetwork.Client||!FinalBoss(npc))return;
        foreach(var owner in Main.player){
            if(!owner.active||owner.dead)continue;
            var s=owner.GetModPlayer<CompanionPlayer>();
            if(s.NetLeaving||s.Cat is not {} cat||s.Vitals.Downed||s.Stowed||!npc.playerInteraction[owner.whoAmI]&&Vector2.DistanceSquared(owner.Center,npc.Center)>2000*2000)continue;
            // Record actual shared victory even with chatter/emotes muted.
            // World downed flags and a boss despawning are not shared memories.
            string family=CompanionSpeechContext.BossFamily(npc);
            bool learned=s.Dialogue.Memories.Victory(family);
            s.Expression.TrySay(s,cat,"BossVictory",EmoteID.EmoteHappiness,context:CompanionText.MessageFull(Lang.GetNPCName(npc.type).Key),variant:family);
            if(learned)CompanionNetwork.Send(s,s.Player.whoAmI);
        }
    }
}
