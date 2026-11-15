using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class CompanionPetting:ModPlayer {
    // Reuse vanilla's town-pet arm animation, without inventing an NPC or
    // changing talkNPC, housing, movement controls or held-item hooks.
    static readonly Action<Player,float,Item,Rectangle> NativePose=typeof(Player)
        .GetMethod("ItemCheck_ApplyHoldStyle_Inner",BindingFlags.Instance|BindingFlags.NonPublic)!
        .CreateDelegate<Action<Player,float,Item,Rectangle>>();
    static readonly Item empty=new();
    CompanionPlayer State=>Player.GetModPlayer<CompanionPlayer>();
    CompanionCat Cat=>State.Cat?.ModProjectile as CompanionCat;
    int side;
    bool buttonHeld,continuous;
    ulong pressedAt,lastButton,lastKeep;
    public bool Active=>Cat is {} cat&&(CompanionNetwork.Client?cat.Animation.Motion==CatMotion.Pet:cat.Projectile.ai[1]>0);
    internal bool Touching=>Active&&Cat is {} cat&&Math.Abs(cat.Projectile.Center.X-Player.Center.X)<=34&&Math.Abs(cat.Projectile.Bottom.Y-Player.Bottom.Y)<=12;
    bool Moving=>Player.controlLeft||Player.controlRight||Player.controlJump||Player.velocity.LengthSquared()>.1f||Player.mount.Active;
    string Unavailable()=>Moving?"Busy":CompanionFeeding.Unavailable(State,allowPetting:true);
    internal void Begin(){
        if(CompanionNetwork.Client){CompanionNetwork.Action(2);return;}
        string reason=Unavailable();
        if(reason!=null){State.Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI."+(reason=="Closer"?"PetCloser":"PetBusy"));return;}
        var cat=Cat;side=cat.Projectile.Center.X<Player.Center.X?-1:1;
        cat.Projectile.ai[1]=90;cat.Projectile.netUpdate=true;
        Player.ChangeDir(side);State.Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.PetHappy");
        // Repeated petting keeps the pose without repeating the greeting every click.
        if(State.PetChatterCooldown==0){State.PetChatterCooldown=600;cat.Feedback.PetSound(cat.Projectile);State.Expression.Say(State,cat.Projectile,"Pet",Terraria.GameContent.UI.EmoteID.EmotionLove);}
    }
    internal void Continue(){if(!CompanionNetwork.Client&&Active)Cat.Projectile.ai[1]=90;}
    internal void End(){if(Cat is {} cat){cat.Projectile.ai[1]=0;cat.Projectile.netUpdate=true;}}
    internal bool Hold(CompanionCat cat,out Vector2 goal){
        goal=cat.Projectile.Center;if(!Active)return false;
        if(Unavailable()!=null){End();return false;}
        goal=Player.Bottom+new Vector2(side*24,-cat.Projectile.height/2f);
        Player.ChangeDir(side);cat.FaceWork(Player.Center);
        return true;
    }
    internal void ButtonHeld(){
        ulong now=Main.GameUpdateCount;lastButton=now;
        if(!buttonHeld){buttonHeld=true;continuous=false;pressedAt=now;lastKeep=now;}
        if(now-pressedAt<12)return;
        if(!continuous||now-lastKeep>=30){continuous=true;lastKeep=now;if(CompanionNetwork.Client)CompanionNetwork.Action(4);else Continue();}
    }
    public override void PostUpdate(){
        if(!CompanionNetwork.Client&&Active&&Unavailable()!=null)End();
        if(Player.whoAmI==Main.myPlayer&&buttonHeld&&(!Main.mouseLeft||!Main.hasFocus||!State.PanelOpen||Main.GameUpdateCount-lastButton>2)){
            buttonHeld=false;if(continuous){if(CompanionNetwork.Client)CompanionNetwork.Action(5);else End();}continuous=false;
        }
        if(!Touching||Moving||Player.itemAnimation>0||Player.dead)return;
        Player.ChangeDir(Cat.Projectile.Center.X<Player.Center.X?-1:1);
        bool petting=Player.isPettingAnimal,small=Player.isTheAnimalBeingPetSmall;
        try{Player.isPettingAnimal=true;Player.isTheAnimalBeingPetSmall=false;NativePose(Player,0,empty,Rectangle.Empty);}
        finally{Player.isPettingAnimal=petting;Player.isTheAnimalBeingPetSmall=small;}
    }
}
