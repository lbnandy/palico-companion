using System;
using Terraria;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// A normal multiplayer quit waits for inventory replies and a final server
// snapshot. The server freezes this cat before acknowledging the save boundary.
public sealed class CompanionSession : ModSystem {
    static On_WorldGen.orig_SaveAndQuit save;static Action callback;static DateTime began,lastRequest;static bool requested;
    internal static bool Closing=>save!=null;
    public override void Load()=>On_WorldGen.SaveAndQuit+=Quit;
    public override void Unload(){On_WorldGen.SaveAndQuit-=Quit;save=null;callback=null;}
    static void Quit(On_WorldGen.orig_SaveAndQuit orig,Action done){
        if(!CompanionNetwork.Client||!Main.LocalPlayer.GetModPlayer<CompanionPlayer>().NetReady){orig(done);return;}
        // IngameOptions sets gameMenu BEFORE calling SaveAndQuit. Keep the
        // world/network pump alive until its authoritative save boundary arrives.
        Main.gameMenu=false;
        if(Closing)return;save=orig;callback=done;began=lastRequest=DateTime.UtcNow;requested=false;
    }
    public override void PostUpdateEverything(){
        if(!Closing)return;
        Main.gameMenu=false;
        if(!CompanionNetwork.Client||Netplay.Disconnect){Complete();return;}
        if(!requested&&!CompanionNetwork.Pending){requested=true;CompanionNetwork.Close();}
        if((DateTime.UtcNow-lastRequest).TotalSeconds>=2){
            lastRequest=DateTime.UtcNow;
            if(CompanionNetwork.Pending)CompanionNetwork.RetryClosingRequest();
            else CompanionNetwork.Close();
        }
        if((DateTime.UtcNow-began).TotalSeconds>=15){
            // SaveResume retains unresolved escrow and the exact request. Do not
            // refund an uncertain transfer or clear its reconnect journal.
            Mod.Logger.Warn("Palico save handshake timed out; preserving reconnect journal and saving locally.");
            Complete();
        }
    }
    internal static void Complete(){var orig=save;var done=callback;save=null;callback=null;requested=false;orig?.Invoke(done);}
}
