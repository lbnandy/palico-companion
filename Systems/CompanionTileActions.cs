using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// PickTile sends action 17 only when run by a client. Our jobs run on the
// server, so a TileSquare alone silently skips the client's native hit effect.
// Scope only actual companion work; ordinary player operations stay untouched.
public sealed class CompanionTileActions:ModSystem {
    static int workDepth,killDepth;
    internal static bool Working=>workDepth>0;
    public override void Load()=>On_WorldGen.KillTile+=Kill;
    public override void Unload(){On_WorldGen.KillTile-=Kill;workDepth=killDepth=0;}
    internal readonly struct WorkScope:IDisposable {
        public WorkScope(){workDepth++;}
        public void Dispose()=>workDepth--;
    }
    internal static WorkScope Begin()=>new();
    static void Kill(On_WorldGen.orig_KillTile orig,int x,int y,bool fail,bool effectOnly,bool noItem){
        bool outer=workDepth>0&&killDepth==0;
        // Send before native framing can remove/replace the client's tile.
        // One root event reproduces native recursive tree effects. Sending
        // every branch would replay them and produce duplicate hit sounds.
        if(outer&&CompanionNetwork.Server&&!effectOnly&&WorldGen.InWorld(x,y)&&Main.tile[x,y].HasTile)
            NetMessage.SendData(MessageID.TileManipulation,-1,-1,null,noItem?4:0,x,y,fail?1:0);
        killDepth++;
        try{orig(x,y,fail,effectOnly,noItem);}finally{killDepth--;}
    }
}
