using System;
using System.Collections.Generic;
using Terraria.ModLoader.IO;
namespace PalicoCompanion.Systems;

// Rotation belongs to the saved companion, not to the disposable bubble renderer.
public sealed class CompanionDialogue {
    public static readonly string[] Events={"FirstMeet","Heal","Downed","Recovered","Tree","Pet","Recall","Rest","Welcome","OwnerHurt","Ore","Loot","RouteRecovered","Build","FoundCrystal","FoundChest","FoundFruit","FoundBulb","FoundRescue","NoAmmo","NoMana","ManaPotion","Boss","Working","FishCaught","Delivered","BossVictory"};
    readonly Dictionary<string,int> spoken=new();
    public CompanionMemories Memories {get;internal set;}=new();
    internal void Write(System.IO.BinaryWriter w){foreach(string key in Events)w.Write((byte)(spoken.TryGetValue(key,out int count)?count:0));Memories.Write(w);}
    internal void Read(System.IO.BinaryReader r){spoken.Clear();foreach(string key in Events)spoken[key]=Math.Clamp((int)r.ReadByte(),0,5);Memories=new();Memories.Read(r);}
    internal void Observe(string line){foreach(string pool in CompanionSpeechContext.Pools)for(int i=1;i<=CompanionSpeechContext.Count(pool);i++)if(line=="Mods.PalicoCompanion.Speech."+pool+".Line"+i){spoken[CompanionSpeechContext.Event(pool)]=i%CompanionSpeechContext.Count(pool);return;}}
    public string Next(string key,string variant=null) {
        if(Array.IndexOf(Events,key)<0)throw new ArgumentException("Unknown companion speech event",nameof(key));
        string pool=CompanionSpeechContext.Pool(key,variant);int size=CompanionSpeechContext.Count(pool);
        spoken.TryGetValue(key,out int count);
        count%=size;spoken[key]=(count+1)%size;
        return "Mods.PalicoCompanion.Speech."+pool+".Line"+(count+1);
    }
    public TagCompound Save() {
        var tag=new TagCompound();foreach(var pair in spoken)tag[pair.Key]=pair.Value;tag["memories"]=Memories.Save();return tag;
    }
    public void Load(TagCompound tag) {
        spoken.Clear();foreach(string key in Events)if(tag.ContainsKey(key))spoken[key]=Math.Clamp(tag.GetInt(key),0,5);
        Memories=new();if(tag.ContainsKey("memories"))Memories.Load(tag.GetCompound("memories"));
    }
}
