using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent.UI;

namespace PalicoCompanion.Systems;

// Optional content registers facts and localized pools here. Delivery, cooldowns,
// emotes, saved history and owner-only network messages remain in the core.
internal sealed class CompanionSpeechContent:ModSystem {
    internal sealed record Boss(string Family,string Identity,string NameKey,int[] Roots,Func<NPC,bool> Final,Func<NPC,bool> Matches=null);
    internal sealed class WorldEvent {
        internal readonly string Variant;
        internal readonly Func<bool> IsActive;
        internal readonly Func<Player,bool> Nearby;
        internal bool Active;internal int Epoch;
        internal WorldEvent(string variant,Func<bool> active,Func<Player,bool> nearby){Variant=variant;IsActive=active;Nearby=nearby;}
    }
    internal static readonly Dictionary<int,Boss> Bosses=new();
    internal static readonly Dictionary<string,(string Event,int Count)> Pools=new();
    internal static readonly List<WorldEvent> WorldEvents=new();
    internal static Func<Player,string> Place;
    internal static Func<bool> BossMarathon;
    internal static Boss Find(NPC npc)=>Bosses.TryGetValue(npc.type,out var boss)&&(boss.Matches==null||boss.Matches(npc))?boss:null;
    internal static void Pool(string key,string variant,int count=2)=>Pools[key+variant]=(key,count);
    internal static void Register(Boss boss,IEnumerable<int> members,bool rematch=false){
        foreach(int type in members)Bosses.Add(type,boss);
        CompanionMemories.Register(boss.Family,boss.Identity,boss.NameKey);
        Pool("Boss",boss.Family);Pool("BossVictory",boss.Family);
        if(rematch)Pool("Boss","Rematch"+boss.Family);
    }
    public override void OnWorldLoad(){foreach(var e in WorldEvents){e.Active=false;e.Epoch=0;}}
    public override void PostUpdateWorld(){
        if(CompanionNetwork.Client)return;
        foreach(var e in WorldEvents){bool active=e.IsActive();if(active&&!e.Active)e.Epoch++;e.Active=active;}
    }
    public override void Unload(){Bosses.Clear();Pools.Clear();WorldEvents.Clear();Place=null;BossMarathon=null;CompanionMemories.ClearExtensions();}
}

internal sealed class CompanionExtraReactions {
    readonly Dictionary<string,int> announced=new();
    string place;int arrived;
    internal bool Tick(CompanionExpression speech,CompanionPlayer state,Projectile cat,int tick,bool busy){
        bool nearby=false;
        foreach(var e in CompanionSpeechContent.WorldEvents){
            if(!e.Active||!e.Nearby(state.Player))continue;
            nearby=true;
            if(busy||announced.GetValueOrDefault(e.Variant)==e.Epoch)continue;
            if(speech.TrySay(state,cat,"Boss",EmoteID.EmoteFear,variant:e.Variant))
                announced[e.Variant]=e.Epoch;
        }
        string current=CompanionSpeechContent.Place?.Invoke(state.Player);
        if(current!=place){place=current;arrived=tick;}
        // A first visit is a quiet observation, not a danger alert or a tutorial.
        // Wait until the player has actually stayed, and retain pending visits
        // when chatter is muted or a functional report is taking precedence.
        if(!busy&&!nearby&&place!=null&&tick-arrived>=120&&!state.Dialogue.Memories.Visited(place)&&
            CompanionExpression.ChatEnabled(state)&&!CompanionThreat.WorkDanger(state.Player,cat.Center)&&
            speech.TrySay(state,cat,"Rest",EmoteID.EmoteConfused,variant:place)){
            state.Dialogue.Memories.Visit(place);CompanionNetwork.Send(state,state.Player.whoAmI);
        }
        return nearby;
    }
}
