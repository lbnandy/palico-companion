using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class CompanionKiteInput:ModSystem {
    static bool armed,stopping;
    static int pending,heartbeat;
    static sbyte sent;
    static uint session;
    internal static bool Capturing=>pending>0||Main.LocalPlayer.GetModPlayer<CompanionPlayer>().Cat?.ModProjectile is CompanionCat c&&c.Kite.Active;
    public override void OnWorldLoad()=>Reset();
    public override void OnWorldUnload()=>Reset();
    static void Reset(){armed=stopping=false;pending=heartbeat=0;sent=0;session=0;}
    internal static void Result(string reason){if(reason.Length==0)return;pending=0;Main.LocalPlayer.GetModPlayer<CompanionPlayer>().Report(CompanionKite.Message(reason));}
    internal static void Request(CompanionPlayer s){
        if(pending>0)return;
        string reason=CompanionKite.Unavailable(s);
        if(reason!=null){s.Report(CompanionKite.Message(reason));return;}
        armed=false;stopping=false;sent=0;heartbeat=30;
        if(CompanionNetwork.Client){pending=120;CompanionNetwork.KiteRequest(s,0);}
        else if(!CompanionKite.Start(s,out reason))s.Report(CompanionKite.Message(reason));
    }
    internal static bool Blocked(CompanionPlayer s)=>!Main.hasFocus||Main.inFancyUI||Main.mapFullscreen||Main.playerInventory||Main.ingameOptionsWindow||
        Main.blockInput||Main.drawingPlayerChat||Main.editSign||Main.editChest||PlayerInput.WritingText||s.PanelOpen||s.Player.mouseInterface||
        CompanionStatusBar.BlocksInput||!Main.mouseItem.IsAir;
    public override void PostUpdateInput(){
        if(Main.gameMenu||Main.netMode==NetmodeID.Server)return;
        var s=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();
        if(pending>0)pending--;
        if(s.Cat?.ModProjectile is not CompanionCat c||!c.Kite.Active){if(pending==0)Reset();return;}
        pending=0;
        if(session!=c.Kite.Session){session=c.Kite.Session;armed=false;stopping=false;heartbeat=30;}
        if(s.Player.HeldItem.type!=c.Kite.ItemType||s.Player.dead){
            if(!stopping){if(CompanionNetwork.Client)CompanionNetwork.KiteRequest(s,2);else c.Kite.Stop(c);stopping=true;}return;
        }
        // Release the activation click (or a UI click) before accepting controls.
        bool blocked=Blocked(s);
        if(blocked)armed=false;
        else if(!Main.mouseLeft&&!Main.mouseRight)armed=true;
        sbyte direction=!armed||blocked?(sbyte)0:Main.mouseRight?(sbyte)-1:Main.mouseLeft?(sbyte)1:(sbyte)0;
        if(CompanionNetwork.Client){
            if(direction!=sent||++heartbeat>=30){CompanionNetwork.KiteRequest(s,1,direction);sent=direction;heartbeat=0;}
        }else c.Kite.SetInput(direction);
        // Reeling must not also open a nearby chest or talk to an NPC.
        if(!blocked&&Main.mouseRight){Main.npcChatRelease=false;s.Player.tileInteractionHappened=true;}
    }
}

public sealed class CompanionKitePlayer:ModPlayer {
    public override void PostUpdate(){
        if(Player.GetModPlayer<CompanionPlayer>().Cat?.ModProjectile is not CompanionCat c||!c.Kite.Active||Player.HeldItem.type!=c.Kite.ItemType)return;
        Player.SetCompositeArmFront(true,Player.CompositeArmStretchAmount.Full,CompanionKite.ArmAngle(Player,c.Projectile));
    }
}
