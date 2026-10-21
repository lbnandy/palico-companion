using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.GameContent.UI;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Read vanilla world state, never infer victory from despawning enemies or dawn.
// Epochs advance even while a companion is stowed, so a later invasion is new.
internal sealed class CompanionWorldEvents:ModSystem {
    internal static readonly string[] Names={"Goblin","Snowman","Pirate","Martian","BloodMoon","Eclipse","PumpkinMoon","FrostMoon","Army","SlimeRain","Pillars"};
    internal static readonly bool[] Active=new bool[Names.Length];
    internal static readonly int[] Epoch=new int[Names.Length];
    public override void OnWorldLoad(){Array.Clear(Active);Array.Clear(Epoch);}
    public override void PostUpdateWorld(){if(!CompanionNetwork.Client)Capture();}
    internal static void Set(int id,bool active){if(active&&!Active[id])Epoch[id]++;Active[id]=active;}
    internal static void Capture(){
        for(int i=0;i<4;i++)Set(i,Main.invasionType==i+1&&Main.invasionSize>0);
        Set(4,Main.bloodMoon&&!Main.dayTime);Set(5,Main.eclipse&&Main.dayTime);
        Set(6,Main.pumpkinMoon&&!Main.dayTime);Set(7,Main.snowMoon&&!Main.dayTime);
        Set(8,DD2Event.Ongoing);Set(9,Main.slimeRain);Set(10,NPC.LunarApocalypseIsUp);
    }
    internal static bool Nearby(int id,Player p){
        if(id==8)return p.ZoneOldOneArmy;
        if(id==10)return p.ZoneTowerSolar||p.ZoneTowerVortex||p.ZoneTowerNebula||p.ZoneTowerStardust;
        bool surface=p.ZoneOverworldHeight||p.ZoneSkyHeight||Main.remixWorld;
        if(!surface)return false;
        if(id>=4)return true;
        if(Main.invasionDelay!=0)return false;
        // Vanilla NPC.SpawnNPC checks the moving invasion front, then towns
        // once it reaches the center. Remove only its random spawn roll.
        if(Math.Abs(p.position.X-Main.invasionX*16)<3000)return true;
        if(Math.Abs(Main.invasionX-Main.maxTilesX/2.0)>5)return false;
        foreach(var npc in Main.npc)if(npc.active&&npc.townNPC&&Math.Abs(p.position.X-npc.Center.X)<3000)return true;
        return false;
    }
}

internal sealed class CompanionEventReactions {
    readonly int[] announced=new int[CompanionWorldEvents.Names.Length];
    readonly bool[] witnessed=new bool[CompanionWorldEvents.Names.Length];
    int nextAt,quietUntil;
    internal bool Tick(CompanionExpression speech,CompanionPlayer s,Projectile cat,int tick,bool bossNearby){
        bool nearby=false,ended=false;
        for(int i=0;i<announced.Length;i++){
            if(!CompanionWorldEvents.Active[i]){ended|=witnessed[i];witnessed[i]=false;continue;}
            if(!CompanionWorldEvents.Nearby(i,s.Player))continue;
            nearby=true;
            if(bossNearby||tick<nextAt||announced[i]==CompanionWorldEvents.Epoch[i])continue;
            if(speech.TrySay(s,cat,"Boss",i==9?EmoteID.EmoteConfused:EmoteID.EmoteFear,variant:"Event"+CompanionWorldEvents.Names[i])){
                announced[i]=CompanionWorldEvents.Epoch[i];witnessed[i]=true;nextAt=tick+600;
            }
        }
        if(nearby||bossNearby){quietUntil=0;return true;}
        if(ended)quietUntil=tick+600;
        if(quietUntil>tick&&!CompanionThreat.WorkDanger(s.Player,cat.Center)&&
            speech.TrySay(s,cat,"Rest",EmoteID.EmoteNote,variant:"EventQuiet"))quietUntil=0;
        return false;
    }
}
