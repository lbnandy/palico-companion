using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Discover potion buffs from item metadata, then let native UpdateBuffs apply
// their effects. Equipment procs and class meters are not potion sources.
public sealed class CompanionPotionBuffs : ModSystem {
    static readonly HashSet<int> potions=new();
    public override void PostSetupContent(){
        potions.Clear();
        foreach(var item in ContentSamples.ItemsByType.Values){
            int type=item.buffType;
            if(!item.consumable||item.buffTime<=0||type<=0||item.useStyle!=ItemUseStyleID.DrinkLiquid||
                ItemID.Sets.IsFood[item.type]||Main.debuff[type]||Main.vanityPet[type]||Main.lightPet[type]||
                BuffID.Sets.IsFedState[type]||BuffID.Sets.TimeLeftDoesNotDecrease[type]||BuffID.Sets.BasicMountData[type]!=null)continue;
            potions.Add(type);
        }
    }
    public override void Unload()=>potions.Clear();

    internal sealed class Sharing {
        readonly HashSet<int> borrowed=new();
        internal void Update(Player actor,Player owner,bool enabled){
            // Only reclaim our short-lived entries. An independently applied
            // longer buff remains the cat's own even when sharing is switched off.
            foreach(int type in borrowed){
                int index=actor.FindBuffIndex(type);
                if(index>=0&&actor.buffTime[index]<=2)actor.DelBuff(index);
            }
            borrowed.Clear();
            if(!enabled)return;
            for(int i=0;i<owner.buffType.Length;i++){
                int type=owner.buffType[i];
                if(owner.buffTime[i]<=0||!potions.Contains(type)||actor.HasBuff(type)||actor.buffImmune[type])continue;
                // Native AddBuff replaces other flasks. Sharing must not erase
                // an imbue the cat already has from its own equipment or potion.
                if(Main.meleeBuff[type]&&System.Array.Exists(actor.buffType,b=>b>0&&Main.meleeBuff[b]))continue;
                // Never evict equipment buffs or debuffs to make room. The
                // owner's native buff sync supplies presence, not remote timers.
                if(System.Array.IndexOf(actor.buffType,0)<0)break;
                actor.AddBuff(type,2,quiet:true);
                if(actor.HasBuff(type))borrowed.Add(type);
            }
        }
    }
}
