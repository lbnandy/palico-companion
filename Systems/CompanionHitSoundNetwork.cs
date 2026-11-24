using System.IO;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria;

namespace PalicoCompanion.Systems;

internal static partial class CompanionNetwork {
    const byte NativeHitSound=51;

    internal static void HitSound(in SoundStyle sound,Vector2 position,Projectile held=null){
        var p=Packet(NativeHitSound);
        p.Write(held!=null);
        if(held!=null){p.Write((byte)held.owner);p.Write((short)held.identity);p.Write7BitEncodedInt(held.type);}
        p.WriteVector2(position);
        p.Write(sound.SoundPath);
        p.Write(sound.Identifier??"");
        p.Write((byte)sound.Type);
        p.Write(sound.Volume);
        p.Write(sound.Pitch);
        p.Write(sound.PitchVariance);
        p.Write(sound.MaxInstances);
        p.Write((byte)sound.SoundLimitBehavior);
        p.Write(sound.LimitsArePerVariant);
        p.Write(sound.RerollAttempts);
        p.Write(sound.PlayOnlyIfFocused);
        p.Write((byte)sound.PauseBehavior);
        p.Write7BitEncodedInt(sound.Variants.Length);
        p.Write(!sound.VariantsWeights.IsEmpty);
        foreach(int suffix in sound.Variants)p.Write(suffix);
        foreach(float weight in sound.VariantsWeights)p.Write(weight);
        p.Send();
    }

    static void ReadHitSound(BinaryReader r){
        bool held=r.ReadBoolean();
        int owner=-1,identity=-1,type=0;
        if(held){owner=r.ReadByte();identity=r.ReadInt16();type=r.Read7BitEncodedInt();}
        var at=r.ReadVector2();
        string path=r.ReadString(),identifier=r.ReadString();
        var style=new SoundStyle(path,(SoundType)r.ReadByte()){
            Identifier=identifier.Length==0?null:identifier,
            Volume=r.ReadSingle(),Pitch=r.ReadSingle(),PitchVariance=r.ReadSingle(),
            MaxInstances=r.ReadInt32(),SoundLimitBehavior=(SoundLimitBehavior)r.ReadByte(),
            LimitsArePerVariant=r.ReadBoolean(),RerollAttempts=r.ReadInt32(),
            PlayOnlyIfFocused=r.ReadBoolean(),PauseBehavior=(PauseBehavior)r.ReadByte()
        };
        int count=r.Read7BitEncodedInt();
        bool weighted=r.ReadBoolean();
        if(!float.IsFinite(at.X)||!float.IsFinite(at.Y)||count<0||count>(r.BaseStream.Length-r.BaseStream.Position)/(weighted?8:4))return;
        if(count>0){
            var variants=new int[count];
            for(int i=0;i<count;i++)variants[i]=r.ReadInt32();
            style.Variants=variants;
            if(weighted){
                var weights=new float[count];
                for(int i=0;i<count;i++)weights[i]=r.ReadSingle();
                style.VariantsWeights=weights;
            }
        }
        if(!held||CompanionHitAudio.ShouldPlayHeldSound(owner,identity,type,path))SoundEngine.PlaySound(style,at);
    }
}
