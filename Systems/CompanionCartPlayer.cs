using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// A local death-screen presentation, never a world entity or respawn authority.
public sealed class CompanionCartPlayer : ModPlayer {
    internal const int Duration=180,Interval=5*60*60;
    internal int Age {get;private set;}=-1;
    internal int Cooldown {get;private set;}
    internal Player Passenger {get;private set;}
    internal string Caption {get;private set;}
    internal int HoodColor {get;private set;}
    internal int Dye {get;private set;}
    bool prepared,pending,presented;
    internal long LostCoins {get;private set;}
    internal bool ReplacesDeathSpeech=>Local&&Player.dead&&(presented||(prepared||pending)&&Player.respawnTimer>=Duration);
    internal bool Active=>Age>=0&&Age<Duration&&Passenger!=null;
    bool Local=>!Main.dedServ&&Player.whoAmI==Main.myPlayer;
    internal static bool Available(CatCartMode mode,int cooldown,bool companionReady,int difficulty)=>
        companionReady&&difficulty!=2&&(mode==CatCartMode.EveryTime||mode==CatCartMode.Occasional&&cooldown==0);

    public override void OnEnterWorld(){Cancel();Cooldown=0;presented=false;}
    public override bool PreKill(double damage,int hitDirection,bool pvp,ref bool playSound,ref bool genDust,ref PlayerDeathReason damageSource){
        if(!Local)return true;
        Cancel();presented=false;
        var s=Player.GetModPlayer<CompanionPlayer>();var config=ModContent.GetInstance<CompanionConfig>();
        bool ready=config.Enabled&&s.Enabled&&!s.Stowed&&!s.Vitals.Downed&&s.Cat?.ModProjectile is CompanionCat;
        if(!Available(config.CatCart,Cooldown,ready,Player.difficulty))return true;
        // Capture before Mediumcore removes equipment. This independent preview
        // player follows the native character-preview update/drawing path.
        try{
            Passenger=new Player();Passenger.CopyVisuals(Player);
            // A clockwise quarter-turn puts a left-facing character face up.
            Passenger.direction=-1;Passenger.velocity=Vector2.Zero;Passenger.dead=false;
            Passenger.selectedItem=0;Passenger.inventory[0].TurnToAir();Passenger.position=Vector2.Zero;
            HoodColor=s.HoodColor;Dye=CompanionDye.Shader(s);prepared=true;
        }catch(Exception e){Cancel();Mod.Logger.Warn("Unable to prepare the cat cart passenger.",e);}
        return true;
    }
    public override void Kill(double damage,int hitDirection,bool pvp,PlayerDeathReason damageSource){
        if(Local&&prepared)pending=true;
    }
    public override void OnRespawn(){Cancel();presented=false;}
    internal void Cancel(){Age=-1;Passenger=null;Caption=null;prepared=pending=false;}
    internal void UpdatePresentation(){
        if(Cooldown>0)Cooldown--;
        var config=ModContent.GetInstance<CompanionConfig>();
        if(!Local||!Player.active||!Player.dead||Player.ghost||Player.difficulty==2||!config.Enabled||config.CatCart==CatCartMode.Off){Cancel();return;}
        if(pending){
            pending=prepared=false;
            // Fast-respawn mods own their timer. Skip instead of holding it open.
            if(Player.respawnTimer<Duration){Cancel();return;}
            Age=0;Cooldown=Interval;presented=true;LostCoins=Math.Max(0,Player.lostCoins);
            var s=Player.GetModPlayer<CompanionPlayer>();
            if(s.Expression.TryCartCaption(s))Caption="Mods.PalicoCompanion.CartSpeech."+Fare(LostCoins);
        }else if(Active){
            if(++Age>=Duration){Cancel();return;}
        }
        if(!Active)return;
        using(new Main.CurrentPlayerOverride(Passenger)){
            Passenger.ResetEffects();Passenger.ResetVisibleAccessories();Passenger.UpdateMiscCounter();
            Passenger.UpdateDyes();Passenger.PlayerFrame();
            Passenger.socialIgnoreLight=true;
            Passenger.bodyFrame.Y=Passenger.legFrame.Y=Passenger.headFrame.Y=0;
        }
    }
    internal static string Fare(long coins)=>coins<=0?"Free":coins<10000?"Small":coins<100000?"Gold":"Costly";
}
