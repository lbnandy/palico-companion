using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PalicoCompanion.Systems;

// Existing Terraria assets, shaped at the actual recipient/impact position.
// One authoritative cue per event; clients never echo a predicted cast.
internal static class TrainingFeedback {
    internal static SoundStyle Sound(int kind,bool impact)=>kind switch {
        0=>SoundID.Item4,1=>SoundID.Item37,2=>SoundID.Item4,
        3=>new SoundStyle(SoundID.Item35.SoundPath){Volume=SoundID.Item35.Volume,PitchVariance=0},4=>SoundID.DrumTomLow,
        5=>impact?SoundID.Dig:SoundID.Item1,
        6=>SoundID.DoubleJump,7=>impact?SoundID.Item14:SoundID.Item1,_=>SoundID.Item4
    };
    internal static void Draw(int kind,Vector2 at,string phase){
        if(kind<0||kind>=8)return;
        bool impact=phase=="impact";
        // Bell follows Player.ItemCheck's Bell (507); lift follows vanilla's
        // DoubleJump. Item29 is a mana crystal, Item16 a whoopie cushion.
        SoundStyle sound=Sound(kind,impact);
        SoundEngine.PlaySound(sound with {Volume=sound.Volume*.6f,MaxInstances=3},at);
        if(kind==7&&impact){Explosion(at);return;}
        int count=kind==5?(impact?6:3):kind==7?3:kind==1?18:10;
        int dust=kind switch {0=>DustID.Honey,1=>DustID.SilverCoin,2=>DustID.MagicMirror,3=>DustID.MagicMirror,4=>DustID.Torch,5=>DustID.SilverCoin,6=>DustID.Cloud,7=>DustID.Smoke,_=>DustID.MagicMirror};
        for(int i=0;i<count;i++){
            Vector2 direction=(MathHelper.TwoPi*i/count).ToRotationVector2();
            Vector2 origin=kind is 1 or 2?at+direction*18:kind==6?at+new Vector2(direction.X*14,16):at+direction*6;
            var d=Dust.NewDustPerfect(origin,dust);
            d.noGravity=!(kind==5&&impact);d.scale=kind==5&&impact?.65f:.8f;
            d.velocity=kind switch {0=>new Vector2(direction.X*.6f,-1.2f),1=>direction*.5f,2=>direction*1.2f,3=>new Vector2(direction.X,-1.5f),4=>new Vector2(direction.X,-.8f),6=>new Vector2(direction.X*1.8f,1),_=>direction*(impact?2.5f:.6f)};
        }
    }
    static void Explosion(Vector2 at){
        // Establish the blast's visible extent immediately, while its damage is
        // active. Later drifting smoke is only aftermath, not a growing hitbox.
        float radius=TrainingBomb.BlastSize*.5f;
        for(int i=0;i<72;i++){
            bool edge=i<24;
            float angle=edge?MathHelper.TwoPi*(i+Main.rand.NextFloat(-.35f,.35f))/24:Main.rand.NextFloat(MathHelper.TwoPi);
            var direction=angle.ToRotationVector2();
            float distance=radius*(edge?Main.rand.NextFloat(.72f,.84f):MathF.Sqrt(Main.rand.NextFloat())*.65f);
            var d=Dust.NewDustPerfect(at+direction*distance,DustID.Torch);
            d.noGravity=true;d.scale=edge?Main.rand.NextFloat(1.5f,2f):Main.rand.NextFloat(2f,3f);
            d.velocity=direction*(edge?.45f:1.2f);
        }
        for(int i=0;i<20;i++){
            var direction=Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
            var d=Dust.NewDustPerfect(at+direction*radius*Main.rand.NextFloat(.15f,.65f),DustID.Smoke);
            d.noGravity=true;d.scale=Main.rand.NextFloat(2f,2.8f);d.alpha=80;
            d.velocity=direction*.6f-new Vector2(0,.3f);
        }
    }
}
