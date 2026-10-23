using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Reuses Meowmere's native vocal variants; external candidates remain unused.
public sealed class CompanionFeedback {
    ulong voiceAt,purrAt;
    internal static void Show(byte kind,Vector2 at,int value,string key=""){
        if(CompanionNetwork.Server){CompanionNetwork.FeedbackAt(kind,at,value,key);return;}
        if(Vector2.DistanceSquared(Main.LocalPlayer.Center,at)>1800*1800)return;
        var box=new Rectangle((int)at.X-12,(int)at.Y-14,24,28);
        switch(kind){
            case 9:
                float range=Math.Clamp(value/Math.Max(1f,Main.screenHeight/Main.GameViewMatrix.Zoom.Y/2f),0,1);
                var chord=range>5f/6?SoundID.GuitarEm:range>4f/6?SoundID.GuitarD:range>3f/6?SoundID.GuitarC:range>2f/6?SoundID.GuitarBm:range>1f/6?SoundID.GuitarAm:SoundID.GuitarG;
                SoundEngine.PlaySound(chord,at);break;
            case 8:CompanionFishingAudio.Present(value,at);break;
            case 7:SoundEngine.PlaySound(SoundID.Dig,at);break;
            case 6:TrainingFeedback.Draw(value,at,key);break;
            case 0:CombatText.NewText(box,CombatText.DamagedFriendly,value);SoundEngine.PlaySound(SoundID.NPCHit1,at);break;
            case 1:case 2:CombatText.NewText(box,kind==1?CombatText.HealLife:CombatText.HealMana,value);if(key!="skill")SoundEngine.PlaySound(SoundID.Item3,at);break;
            case 3:if(ModContent.GetInstance<CompanionConfig>().CatSounds)SoundEngine.PlaySound(VoiceStyle(key),at);break;
            case 5:SoundEngine.PlaySound(value==1?SoundID.CoinPickup:SoundID.Grab,at);break;
            case 4:if(ContentSamples.ItemsByType.TryGetValue(value,out var item))CompanionItemVisual.BeginUse(item,at);break;
        }
    }
    // Native Meowmere has two built-in cat vocal variants; no bundled recordings.
    public static SoundStyle VoiceStyle(string key) => SoundID.Meowmere with {
        Identifier="PalicoCompanion/Voice",Volume=key=="Pet"?.14f:.20f,
        Pitch=key is "Hurt" or "Downed" or "Boss"?-.12f:0f,PitchVariance=.06f,MaxInstances=1
    };
    public bool PetSound(Projectile cat){
        if(!CompanionNetwork.Server&&!ModContent.GetInstance<CompanionConfig>().CatSounds||Main.GameUpdateCount<purrAt)return false;
        Show(3,cat.Center,0,"Pet");purrAt=Main.GameUpdateCount+600;return true;
    }
    public bool Voice(Projectile cat,string key) {
        if(!CompanionNetwork.Server&&!ModContent.GetInstance<CompanionConfig>().CatSounds||Main.GameUpdateCount<voiceAt)return false;
        if(key=="Pet"||key!="Hurt"&&Array.IndexOf(CompanionDialogue.Events,key)<0)return false;
        Show(3,cat.Center,0,key);voiceAt=Main.GameUpdateCount+240;return true;
    }
    public bool Hurt(CompanionPlayer state,Projectile cat,int damage,bool boss,float penetration=0) {
        if(damage<=0||state.Vitals.Downed||state.Vitals.Immunity>0)return false;
        int before=state.Vitals.Health;
        damage=CompanionVitals.AfterDefense(damage,state.Vitals.EffectiveDefense,penetration);
        damage=state.Training.Shield(state,damage,true);
        if(!state.Vitals.HurtFinal(damage,boss))return false;
        int lost=before-state.Vitals.Health;
        Show(0,cat.Center,lost);
        // Native town-cat damage sound, with a restrained vocal response.
        Voice(cat,state.Vitals.Downed?"Downed":"Hurt");
        return true;
    }
    public static Color ImmuneTint(Color light,int immunity,bool downed) => !downed&&immunity>0&&immunity%12<6 ? light*0.55f : light;
}
