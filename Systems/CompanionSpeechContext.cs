using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.GameContent.UI;
namespace PalicoCompanion.Systems;

// Variants share their event's saved cursor and cooldown channel. The existing
// fixed event-byte wire layout stays intact; speech packets already carry keys.
internal static class CompanionSpeechContext {
    internal static readonly string[] BossFamilies={"SlimeKing","Eye","Eater","Brain","Bee","Skeletron","Deerclops","Wall","QueenSlime","Twins","Destroyer","Prime","Plantera","Golem","Duke","Empress","Cultist","MoonLord","Betsy"};
    static readonly Dictionary<string,string> variants=Create();
    static Dictionary<string,string> Create(){
        var map=new Dictionary<string,string>();
        foreach(string family in BossFamilies){map["Boss"+family]="Boss";map["BossRematch"+family]="Boss";map["BossVictory"+family]="BossVictory";}
        map["RestMemory"]="Rest";
        foreach(string name in CompanionWorldEvents.Names)map["BossEvent"+name]="Boss";
        map["RestEventQuiet"]="Rest";
        map["OwnerHurtDeath"]="OwnerHurt";map["WelcomeRevived"]="Welcome";map["WorkingBagFull"]="Working";
        foreach(string place in new[]{"Rain","Snow","Desert","Jungle","Evil","Underworld","Ocean","Mushroom"})map["Rest"+place]="Rest";
        map["DeliveredLife"]=map["DeliveredMana"]=map["DeliveredUpgrade"]="Delivered";
        map["LootPlunder"]="Loot";
        map["FishCaughtCrate"]=map["FishCaughtQuest"]="FishCaught";
        // Equipment reactions share the existing saved work cursor and wire
        // layout; each variant still has its own repeat cooldown.
        foreach(string gear in new[]{"Honey","Guard","GuardSelf","Cleanse","Bell","BellSelf","Drum","Dart","Lift","Bomb","EquippedWeapon","EquippedSupport"})map["WorkingGear"+gear]="Working";
        return map;
    }
    internal static IEnumerable<string> Pools=>CompanionDialogue.Events.Concat(variants.Keys).Concat(CompanionSpeechContent.Pools.Keys);
    internal static string Event(string pool)=>CompanionSpeechContent.Pools.TryGetValue(pool,out var extra)?extra.Event:variants.TryGetValue(pool,out string key)?key:pool;
    internal static int Count(string pool)=>CompanionSpeechContent.Pools.TryGetValue(pool,out var extra)?extra.Count:variants.ContainsKey(pool)?3:6;
    internal static string Pool(string key,string variant){
        if(variant==null)return key;
        if(Event(key+variant)==key)return key+variant;
        // Optional content can supply only encounter/victory pools; rematches
        // then reuse its own encounter wording instead of a generic boss line.
        if(variant.StartsWith("Rematch")&&Event(key+variant[7..])==key)return key+variant[7..];
        return key;
    }
    internal static string BossFamily(NPC npc)=>npc.type switch {
        NPCID.KingSlime=>"SlimeKing",NPCID.EyeofCthulhu=>"Eye",
        NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail=>"Eater",
        NPCID.BrainofCthulhu=>"Brain",NPCID.QueenBee=>"Bee",NPCID.SkeletronHead=>"Skeletron",NPCID.Deerclops=>"Deerclops",
        NPCID.WallofFlesh=>"Wall",NPCID.QueenSlimeBoss=>"QueenSlime",NPCID.Retinazer or NPCID.Spazmatism=>"Twins",
        NPCID.TheDestroyer=>"Destroyer",NPCID.SkeletronPrime=>"Prime",NPCID.Plantera=>"Plantera",NPCID.Golem=>"Golem",
        NPCID.DukeFishron=>"Duke",NPCID.HallowBoss=>"Empress",NPCID.CultistBoss=>"Cultist",NPCID.MoonLordCore=>"MoonLord",NPCID.DD2Betsy=>"Betsy",_=>CompanionSpeechContent.Find(npc)?.Family
    };
    internal static bool EncounterBoss(NPC n)=>n.active&&n.life>0&&(CompanionSpeechContent.Find(n) is {} boss?boss.Roots.Contains(n.type)&&n.boss:n.boss||n.type==NPCID.EaterofWorldsHead);
    internal static string RestPlace(Player p){
        if(CompanionSpeechContent.Place?.Invoke(p) is {} place)return place;
        if(p.ZoneUnderworldHeight)return "Underworld";
        if(p.ZoneCorrupt||p.ZoneCrimson)return "Evil";
        if(p.ZoneSnow)return "Snow";
        if(p.ZoneDesert)return "Desert";
        if(p.ZoneJungle)return "Jungle";
        if(p.ZoneGlowshroom)return "Mushroom";
        if(Main.raining&&p.ZoneOverworldHeight)return "Rain";
        if(p.ZoneBeach)return "Ocean";
        return null;
    }
}

internal sealed class CompanionBossEncounter {
    readonly Dictionary<string,int> seen=new();
    readonly HashSet<string> announced=new();
    // One encounter survives a short excursion off screen. Failed text delivery
    // is retried while the boss remains nearby, not marked as already spoken.
    int marathonAt;
    internal bool Tick(CompanionExpression expression,CompanionPlayer state,Projectile cat,int tick){
        var present=new Dictionary<string,NPC>();
        foreach(var n in Main.npc)if(CompanionSpeechContext.EncounterBoss(n)&&Vector2.DistanceSquared(n.Center,cat.Center)<900*900){
            string id=CompanionSpeechContext.BossFamily(n)??"NPC:"+n.type;
            if(!present.TryGetValue(id,out var old)||Vector2.DistanceSquared(n.Center,cat.Center)<Vector2.DistanceSquared(old.Center,cat.Center))present[id]=n;
            seen[id]=tick;
        }
        foreach(string id in new List<string>(seen.Keys))if(tick-seen[id]>=600){seen.Remove(id);announced.Remove(id);}
        foreach(var pair in present)if(!announced.Contains(pair.Key)){
            if(CompanionSpeechContent.BossMarathon?.Invoke()==true&&tick<marathonAt)break;
            var n=pair.Value;
            string family=CompanionSpeechContext.BossFamily(n);
            string variant=state.Dialogue.Memories.Won(family)?"Rematch"+family:family;
            if(expression.TrySay(state,cat,"Boss",EmoteID.EmoteFear,context:CompanionText.MessageFull(Lang.GetNPCName(n.type).Key),variant:variant)){announced.Add(pair.Key);marathonAt=tick+1800;}
            break;
        }
        return present.Count>0;
    }
}
