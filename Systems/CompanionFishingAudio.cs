using System;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using ReLogic.Utilities;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Keep native bobber timing, including splash variants and broken-line sound.
// Only sounds emitted inside our isolated fishing actor are routed. Replicas
// render vanilla particles but do not replay sounds from predicted bobber AI.
public sealed class CompanionFishingAudio:ModSystem {
    Hook hook;
    static bool presenting;
    static readonly SoundStyle[] sounds={SoundID.Splash,SoundID.SplashWeak,SoundID.Shimmer1,SoundID.Shimmer2,SoundID.ShimmerWeak1,SoundID.ShimmerWeak2,SoundID.Item17};
    delegate SlotId Play(in SoundStyle style,Vector2? position,SoundUpdateCallback callback);
    delegate SlotId Intercept(Play orig,in SoundStyle style,Vector2? position,SoundUpdateCallback callback);
    public override void Load(){
        var method=typeof(SoundEngine).GetMethod(nameof(SoundEngine.PlaySound),new[]{typeof(SoundStyle).MakeByRefType(),typeof(Vector2?),typeof(SoundUpdateCallback)});
        hook=new Hook(method,(Intercept)Route);
    }
    public override void Unload(){hook?.Dispose();hook=null;presenting=false;}
    static SlotId Route(Play orig,in SoundStyle style,Vector2? position,SoundUpdateCallback callback){
        if(!presenting&&CompanionFishing.Current is {} fishing){
            int index=-1;for(int i=0;i<sounds.Length;i++)if(sounds[i].SoundPath==style.SoundPath){index=i;break;}
            if(index>=0){
                if(!CompanionNetwork.Client)CompanionFeedback.Show(8,position??fishing.Cat.Center,index);
                return SlotId.Invalid;
            }
        }
        return orig(in style,position,callback);
    }
    internal static void Present(int index,Vector2 position){
        if(index<0||index>=sounds.Length)return;
        bool previous=presenting;presenting=true;
        try{SoundEngine.PlaySound(sounds[index],position);}finally{presenting=previous;}
    }
}
