using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;
namespace PalicoCompanion.Systems;

// Character-owned facts. Persist stable content names, never runtime NPC IDs or
// optional-mod registration order. Missing mods retain their dormant memories.
public sealed class CompanionMemories {
    static readonly string[] families={"SlimeKing","Eye","Eater","Brain","Bee","Skeletron","Deerclops","Wall","QueenSlime","Twins","Destroyer","Prime","Plantera","Golem","Duke","Empress","Cultist","MoonLord","Betsy"};
    static readonly int[] types={NPCID.KingSlime,NPCID.EyeofCthulhu,NPCID.EaterofWorldsHead,NPCID.BrainofCthulhu,NPCID.QueenBee,NPCID.SkeletronHead,NPCID.Deerclops,NPCID.WallofFlesh,NPCID.QueenSlimeBoss,NPCID.Retinazer,NPCID.TheDestroyer,NPCID.SkeletronPrime,NPCID.Plantera,NPCID.Golem,NPCID.DukeFishron,NPCID.HallowBoss,NPCID.CultistBoss,NPCID.MoonLordCore,NPCID.DD2Betsy};
    internal sealed record Entry(string Family,string Identity,string NameKey);
    static readonly List<Entry> extra=new();
    const int Limit=512;
    readonly HashSet<string> victories=new(StringComparer.Ordinal),recalled=new(StringComparer.Ordinal),places=new(StringComparer.Ordinal);
    string latest="";
    static string Identity(string family){
        int i=Array.IndexOf(families,family);
        return i>=0?"Terraria/"+family:extra.FirstOrDefault(e=>e.Family==family)?.Identity;
    }
    internal static void Register(string family,string id,string nameKey){
        if(Identity(family)!=null||extra.Any(e=>e.Identity==id))throw new ArgumentException("Duplicate companion memory identity");
        extra.Add(new(family,id,nameKey));
    }
    internal static void ClearExtensions()=>extra.Clear();
    static int Count=>families.Length+extra.Count;
    static string IdentityAt(int index)=>index<families.Length?"Terraria/"+families[index]:extra[index-families.Length].Identity;
    public bool Won(string family)=>Identity(family) is {} id&&victories.Contains(id);
    internal bool Victory(string family){
        string id=Identity(family);if(id==null||victories.Count>=Limit||!victories.Add(id))return false;
        latest=id;return true;
    }
    internal int Recollection(){
        int first=-1;
        for(int i=0;i<Count;i++){string id=IdentityAt(i);if(!victories.Contains(id)||recalled.Contains(id))continue;if(id==latest)return i;if(first<0)first=i;}
        return first;
    }
    internal static string NameKey(int index)=>index>=families.Length?extra[index-families.Length].NameKey:index==9?"Mods.PalicoCompanion.MemoryTwins":Lang.GetNPCName(types[index]).Key;
    internal static string Name(int index)=>Terraria.Localization.Language.GetTextValue(NameKey(index));
    internal static string Context(string line,string context)=>line.StartsWith("Mods.PalicoCompanion.Speech.RestMemory.")
        ?Terraria.Localization.Language.GetTextValue(context??""):CompanionText.Resolve(context??"");
    internal void Recall(int index){if(index>=0&&index<Count&&victories.Contains(IdentityAt(index)))recalled.Add(IdentityAt(index));}
    internal bool Visited(string place)=>places.Contains(place);
    internal void Visit(string place){if(places.Count<Limit&&Valid(place))places.Add(place);}
    static bool Valid(string id)=>!string.IsNullOrWhiteSpace(id)&&id.Length<=160;
    static void WriteSet(BinaryWriter w,HashSet<string> values){w.Write((ushort)values.Count);foreach(string value in values.OrderBy(x=>x,StringComparer.Ordinal))w.Write(value);}
    static void ReadSet(BinaryReader r,HashSet<string> values){int count=r.ReadUInt16();if(count>Limit)throw new InvalidDataException("Too many companion memories");values.Clear();for(int i=0;i<count;i++){string id=r.ReadString();if(!Valid(id))throw new InvalidDataException("Invalid companion memory");values.Add(id);}}
    void Normalize(){recalled.IntersectWith(victories);if(!victories.Contains(latest))latest="";}
    internal void Write(BinaryWriter w){WriteSet(w,victories);WriteSet(w,recalled);w.Write(latest);WriteSet(w,places);}
    internal void Read(BinaryReader r){ReadSet(r,victories);ReadSet(r,recalled);latest=r.ReadString();ReadSet(r,places);Normalize();}
    internal TagCompound Save()=>new(){{"version",2},{"won",victories.OrderBy(x=>x).ToList()},{"said",recalled.OrderBy(x=>x).ToList()},{"last",latest},{"places",places.OrderBy(x=>x).ToList()}};
    internal void Load(TagCompound tag){
        victories.Clear();recalled.Clear();places.Clear();latest="";
        if(tag.ContainsKey("won")){
            foreach(string id in tag.GetList<string>("won").Where(Valid).Take(Limit))victories.Add(id);
            foreach(string id in tag.GetList<string>("said").Where(Valid).Take(Limit))recalled.Add(id);
            foreach(string id in tag.GetList<string>("places").Where(Valid).Take(Limit))places.Add(id);
            latest=tag.GetString("last");
        }else{
            // 0.8.85–0.9.8: the first nineteen bits and latest index were fixed.
            uint won=(uint)tag.GetInt("victories"),said=(uint)tag.GetInt("recalled");
            for(int i=0;i<families.Length;i++){string id=IdentityAt(i);if((won&(1u<<i))!=0)victories.Add(id);if((said&(1u<<i))!=0)recalled.Add(id);}
            int n=tag.ContainsKey("latest")?tag.GetInt("latest"):-1;if(n>=0&&n<families.Length)latest=IdentityAt(n);
        }
        Normalize();
    }
}
