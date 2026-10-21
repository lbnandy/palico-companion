using System;
using System.Linq;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
namespace PalicoCompanion.Systems;

public sealed class TrainingLootSnapshot:GlobalNPC {
    public override bool InstancePerEntity=>true;
    internal List<(int Owner,int Tier)> Eligible;
    internal bool BagHandled;
    internal List<(int Owner,int Tier)> Snapshot(NPC npc){
        if(Eligible!=null)return Eligible;Eligible=new();
        // Native companion damage credits its real owner before loot is rolled.
        // Participation, not distance or the final killing blow, grants the bonus.
        for(int i=0;i<Main.maxPlayers;i++){var p=CompanionNativeUse.Current?.State.Player.whoAmI==i?CompanionNativeUse.Current.State.Player:Main.player[i];if(!p.active||p.dead||!npc.playerInteraction[i])continue;var s=p.GetModPlayer<CompanionPlayer>();if(s.Training.Active(s)&&s.Training.Work(8))Eligible.Add((i,s.Training.Tier));}
        return Eligible;
    }
}
public sealed class TrainingLoot:GlobalItem {
    // Boss rewards are crafting materials only; bags, equipment, trophies and
    // rare world keys are never repeated. Actual registered bag rules supply
    // the material quantities and their conditions, not a parallel loot table.
    internal static readonly HashSet<int> BossMaterials=[ItemID.Gel,ItemID.DemoniteOre,ItemID.CrimtaneOre,ItemID.ShadowScale,ItemID.TissueSample,ItemID.BeeWax,ItemID.HallowedBar,ItemID.SoulofMight,ItemID.SoulofSight,ItemID.SoulofFright,ItemID.LunarOre];
    internal static int Bonus(int stack,float rate){float expected=stack*rate;return (int)expected+(Main.rand.NextFloat()<expected-(int)expected?1:0);}
    public override void OnSpawn(Item item,IEntitySource source){
        if(CompanionNetwork.Client||source is not EntitySource_Loot loot||loot.Entity is not NPC npc||npc.friendly||npc.SpawnedFromStatue)return;
        var snapshot=npc.GetGlobalNPC<TrainingLootSnapshot>();var eligible=snapshot.Snapshot(npc);if(eligible.Count==0)return;
        if(ItemID.Sets.BossBag[item.type]){
            if(snapshot.BagHandled)return;snapshot.BagHandled=true;
            var rates=new List<DropRateInfo>();foreach(var rule in Main.ItemDropsDB.GetRulesForItemID(item.type))rule.ReportDroprates(rates,new DropRateInfoChainFeed(1));
            foreach(var e in eligible){var p=CompanionNativeUse.Current?.State.Player.whoAmI==e.Owner?CompanionNativeUse.Current.State.Player:Main.player[e.Owner];var info=new DropAttemptInfo{player=p,item=item.type,npc=npc,IsExpertMode=Main.expertMode,IsMasterMode=Main.masterMode,rng=Main.rand};
                foreach(var rate in rates.Where(r=>BossMaterials.Contains(r.itemId))){if(rate.conditions!=null&&rate.conditions.Any(c=>!c.CanDrop(info)))continue;if(Main.rand.NextFloat()>=rate.dropRate)continue;int amount=Bonus(Main.rand.Next(rate.stackMin,rate.stackMax+1),new[]{.05f,.075f,.10f}[e.Tier]);if(amount>0)Spawn(npc,rate.itemId,amount,e.Owner);}
            }return;
        }
        if(item.type is ItemID.JungleKey or ItemID.CorruptionKey or ItemID.CrimsonKey or ItemID.HallowedKey or ItemID.FrozenKey or ItemID.DungeonDesertKey)return;
        if(item.type is ItemID.CopperCoin or ItemID.SilverCoin or ItemID.GoldCoin or ItemID.PlatinumCoin or ItemID.Heart or ItemID.Star||item.maxStack<=1||!npc.boss&&item.rare>=ItemRarityID.Cyan)return;
        int tier=eligible.Max(e=>e.Tier);int bonus;
        bool boss=npc.boss||npc.type is NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail||npc.realLife>=0&&Main.npc[npc.realLife].boss;
        if(boss){if(!BossMaterials.Contains(item.type))return;bonus=Bonus(item.stack,new[]{.05f,.075f,.10f}[tier]);}
        else bonus=Main.rand.NextFloat()<new[]{.10f,.12f,.15f}[tier]?1:0;
        if(bonus>0&&Spawn(npc,item.type,bonus,-1))Notice(eligible.First(e=>e.Tier==tier).Owner,item.type,bonus);
    }
    static void Notice(int owner,int type,int count){var s=Main.player[owner].GetModPlayer<CompanionPlayer>();TrainingWorkFeedback.Trigger(s,8);if(s.Cat is {} cat)s.Expression.TrySay(s,cat,"Loot",Terraria.GameContent.UI.EmoteID.EmoteHappiness,context:CompanionText.MessageFull(Lang.GetItemName(type).Key)+" ×"+count,variant:"Plunder");}
    static bool Spawn(NPC npc,int type,int count,int owner){int n=Item.NewItem(new EntitySource_Misc("PalicoPlunder"),npc.Hitbox,type,count,noBroadcast:owner>=0);if(n>=Main.maxItems)return false;if(owner>=0){Notice(owner,type,count);Main.timeItemSlotCannotBeReusedFor[n]=54000;Main.item[n].playerIndexTheItemIsReservedFor=owner;if(CompanionNetwork.Server){NetMessage.SendData(MessageID.InstancedItem,owner,-1,null,n);Main.item[n].active=false;}}return true;}
}
