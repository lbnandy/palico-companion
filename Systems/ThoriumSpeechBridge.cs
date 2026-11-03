using System;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

[ExtendsFromMod("ThoriumMod")]
public sealed class ThoriumSpeechBridge:ModSystem {
    public override void PostSetupContent(){
        var thorium=ModLoader.GetMod("ThoriumMod");
        int[] Resolve(string names)=>names.Split(' ',StringSplitOptions.RemoveEmptyEntries)
            .Select(name=>thorium.TryFind<ModNPC>(name,out var npc)?npc.Type:-1).Where(type=>type>=0).ToArray();
        void Boss(string key,string roots,string parts="",string normal=null,string expert=null){
            int[] heads=Resolve(roots),ordinary=Resolve(normal??roots),advanced=Resolve(expert??normal??roots);
            if(heads.Length!=roots.Split(' ',StringSplitOptions.RemoveEmptyEntries).Length||ordinary.Length==0||advanced.Length==0){
                Mod.Logger.Warn("Skipping unavailable Thorium speech encounter: "+key);return;
            }
            bool Final(NPC npc)=>(Main.expertMode?advanced:ordinary).Contains(npc.type)&&
                !Main.npc.Any(other=>other!=npc&&other.active&&other.life>0&&heads.Contains(other.type));
            string nameKey="Mods.PalicoCompanion.ThoriumMemory."+key;
            CompanionSpeechContent.Register(new("Thorium"+key,"ThoriumMod/"+key,nameKey,heads,Final),heads.Concat(Resolve(parts)));
        }
        Boss("ThunderBird","TheGrandThunderBird","StormHatchling");
        Boss("Jellyfish","QueenJellyfish","ZealousJellyfish SpittingJellyfish DistractingJellyfish");
        Boss("Viscount","Viscount","BiteyBaby");
        Boss("GraniteStorm","GraniteEnergyStorm","CoalescedEnergy EncroachingEnergy EnergyConduit EnergyBarrier");
        Boss("Champion","BuriedChampion","FallenChampion1 FallenChampion2");
        Boss("StarScouter","StarScouter","BioCore CryoCore PyroCore");
        // Native phase changes reuse/transform the NPC. Only the actual final
        // form can finish an encounter; adds and discarded shells never do.
        Boss("Strider","BoreanStrider BoreanStriderPopped","BoreanHopper BoreanMyte",normal:"BoreanStriderPopped");
        Boss("Beholder","FallenBeholder FallenBeholder2","Beholder",normal:"FallenBeholder",expert:"FallenBeholder2");
        Boss("Lich","Lich LichHeadless","PhylacteryofaThousandSouls",normal:"Lich",expert:"LichHeadless");
        Boss("Forgotten","ForgottenOne ForgottenOneCracked ForgottenOneReleased","AbyssalSpawn",normal:"ForgottenOneCracked",expert:"ForgottenOneReleased");
        // Expert's last primordial summons Dream Eater; that is a transition,
        // not a victory. All four share one encounter and saved memory.
        Boss("Primordials","Aquaius Omnicide SlagFury DreamEater","AquaiusBubble LucidBubble ImpendingDread InnerDespair UnstableAnger",
            normal:"Aquaius Omnicide SlagFury",expert:"DreamEater");
        foreach(string place in new[]{"Depths","Granite","Marble"})CompanionSpeechContent.Pool("Rest","Thorium"+place);
        var places=new[]{("DepthsBiome","ThoriumDepths"),("GraniteBiome","ThoriumGranite"),("MarbleBiome","ThoriumMarble")}
            .Where(pair=>thorium.TryFind<ModBiome>(pair.Item1,out _)).Select(pair=>(Biome:thorium.Find<ModBiome>(pair.Item1),Key:pair.Item2)).ToArray();
        CompanionSpeechContent.RegisterPlace(player=>places.FirstOrDefault(pair=>player.InModBiome(pair.Biome)).Key);
        foreach(string gear in new[]{"Heal","Bard","Rest"})CompanionSpeechContent.Pool("Working","GearThorium"+gear);
    }
}
