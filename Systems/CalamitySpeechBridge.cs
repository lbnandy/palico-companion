using System;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
using CalamityMod;
using CalamityMod.Events;
using CalamityMod.NPCs.AstrumDeus;
using CalamityMod.NPCs.SupremeCalamitas;

namespace PalicoCompanion.Systems;

// Calamity 2.2.2: optional typed content adapter. Resolve NPC types once after
// content setup; do not compare translated names, reflect into AI, or poll the
// world's downed flags to invent a personal victory.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamitySpeechBridge:ModSystem {
    public override void PostSetupContent(){
        var calamity=ModLoader.GetMod("CalamityMod");
        int[] Resolve(string names)=>names.Split(' ',StringSplitOptions.RemoveEmptyEntries)
            .Select(name=>calamity.TryFind<ModNPC>(name,out var npc)?npc.Type:-1).Where(type=>type>=0).ToArray();
        void Boss(string key,string roots,string parts="",bool rematch=false,Func<NPC,bool> finish=null,Func<NPC,bool> matches=null){
            int[] heads=Resolve(roots);
            if(heads.Length!=roots.Split(' ',StringSplitOptions.RemoveEmptyEntries).Length){Mod.Logger.Warn("Skipping unavailable Calamity speech encounter: "+key);return;}
            bool Final(NPC npc)=>heads.Contains(npc.type)&&(finish?.Invoke(npc)??!Main.npc.Any(other=>other!=npc&&other.active&&other.life>0&&heads.Contains(other.type)));
            CompanionSpeechContent.Register(new("Calamity"+key,"CalamityMod/"+key,"Mods.PalicoCompanion.CalamityMemory."+key,heads,Final,matches),heads.Concat(Resolve(parts)),rematch);
        }
        Boss("DesertScourge","DesertScourgeHead","DesertScourgeBody DesertScourgeTail");
        Boss("Crabulon","Crabulon");
        Boss("HiveMind","HiveMind");
        Boss("Perforators","PerforatorHive","PerforatorHeadSmall PerforatorHeadMedium PerforatorHeadLarge PerforatorBodySmall PerforatorBodyMedium PerforatorBodyLarge PerforatorTailSmall PerforatorTailMedium PerforatorTailLarge");
        // The core awards the victory while fleeing after its paladins fall.
        Boss("SlimeGod","SlimeGodCore","CrimulanPaladin EbonianPaladin SplitCrimulanPaladin SplitEbonianPaladin");
        Boss("Cryogen","Cryogen");
        Boss("AquaticScourge","AquaticScourgeHead","AquaticScourgeBody AquaticScourgeBodyAlt AquaticScourgeTail");
        Boss("BrimstoneElemental","BrimstoneElemental");
        Boss("CalamitasClone","CalamitasClone","Cataclysm Catastrophe SoulSeeker");
        Boss("Leviathan","Anahita Leviathan",rematch:true);
        Boss("AstrumAureus","AstrumAureus");
        Boss("Plaguebringer","PlaguebringerGoliath");
        Boss("Ravager","RavagerBody","RavagerHead RavagerHead2 RavagerClawLeft RavagerClawRight RavagerLegLeft RavagerLegRight");
        // Native loot eligibility distinguishes the split transition from the
        // winning head; merely waiting for the last head alive is incorrect.
        Boss("AstrumDeus","AstrumDeusHead","AstrumDeusBody AstrumDeusTail",finish:n=>!AstrumDeusHead.ShouldNotDropThings(n));
        Boss("Guardians","ProfanedGuardianCommander","ProfanedGuardianDefender ProfanedGuardianHealer");
        Boss("Dragonfolly","Dragonfolly");
        Boss("Providence","Providence","ProvSpawnDefense ProvSpawnHealer ProvSpawnOffense",rematch:true);
        Boss("CeaselessVoid","CeaselessVoid","DarkEnergy");
        Boss("StormWeaver","StormWeaverHead","StormWeaverBody StormWeaverTail");
        Boss("Signus","Signus");
        Boss("Polterghast","Polterghast","PolterPhantom");
        Boss("OldDuke","OldDuke");
        Boss("Devourer","DevourerofGodsHead","DevourerofGodsBody DevourerofGodsTail",rematch:true);
        Boss("Yharon","Yharon",rematch:true);
        // Artemis shares life/death with Apollo. Arms and segments never end
        // this encounter; parked but active machines must still be counted.
        Boss("ExoMechs","AresBody Apollo ThanatosHead","Artemis AresGaussNuke AresLaserCannon AresPlasmaFlamethrower AresTeslaCannon ThanatosBody1 ThanatosBody2 ThanatosTail",rematch:true);
        Boss("Calamitas","SupremeCalamitas","SupremeCataclysm SupremeCatastrophe SupremeSoulSeeker SepulcherHead SepulcherBody SepulcherBodyEnergyBall SepulcherTail",true,
            matches:n=>n.ModNPC is not SupremeCalamitas witch||!witch.permafrost);
        Boss("PrimordialWyrm","PrimordialWyrmHead","PrimordialWyrmBody PrimordialWyrmBodyAlt PrimordialWyrmTail");
        foreach(string place in new[]{"Sulphur","SunkenSea","Brimstone","Astral","Abyss","DeepAbyss"})CompanionSpeechContent.Pool("Rest","Calamity"+place);
        CompanionSpeechContent.Place=Place;
        CompanionSpeechContent.BossMarathon=()=>BossRushEvent.BossRushActive;
        CompanionSpeechContent.Pool("Boss","EventCalamityAcidRain");
        CompanionSpeechContent.Pool("Boss","EventCalamityBossRush");
        CompanionSpeechContent.WorldEvents.Add(new("EventCalamityAcidRain",()=>AcidRainEvent.AcidRainEventIsOngoing,p=>ModCalls.GetInZone(p,"sulphur")&&!ModCalls.GetInZone(p,"abyss")));
        CompanionSpeechContent.WorldEvents.Add(new("EventCalamityBossRush",()=>BossRushEvent.BossRushActive,p=>true));
    }
    internal static string Place(Player player){
        // These are Calamity's public queries, including its current biome
        // definitions and depth checks, rather than our own tile thresholds.
        if(ModCalls.GetInZone(player,"abyss4"))return "CalamityDeepAbyss";
        if(ModCalls.GetInZone(player,"abyss"))return "CalamityAbyss";
        if(ModCalls.GetInZone(player,"sulphur"))return "CalamitySulphur";
        if(ModCalls.GetInZone(player,"sunkensea"))return "CalamitySunkenSea";
        if(ModCalls.GetInZone(player,"crags"))return "CalamityBrimstone";
        if(ModCalls.GetInZone(player,"astral"))return "CalamityAstral";
        return null;
    }
}
