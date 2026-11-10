using System;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class CompanionLocalPresentationBridge : ModSystem {
    public override void Load(){
        Hook("CatalystMod","CatalystMod.CatalystPlayer",CatalystBuffs);
        Hook("LansUnlimitedPets","LansUnlimitedPets.PetPlayer",PetBuffs);
    }

    static void Hook(string modName,string typeName,Action<Action<ModPlayer>,ModPlayer> hook){
        if(!ModLoader.TryGetMod(modName,out var mod))return;
        var type=mod.Code.GetType(typeName);
        var method=type?.GetMethod(nameof(ModPlayer.PreUpdateBuffs),Type.EmptyTypes);
        if(method?.DeclaringType!=type||method?.ReturnType!=typeof(void)||
            !typeof(ModPlayer).IsAssignableFrom(type))return;
        MonoModHooks.Add(method,hook);
    }

    static void CatalystBuffs(Action<ModPlayer> orig,ModPlayer self){
        var use=CompanionNativeUse.Current;
        if(use?.Actor!=self.Player){orig(self);return;}
        // Keep biome state updates, but do not let the native-use identity
        // select this actor as the viewport owner (including on a server).
        int player=Main.myPlayer;
        try{Main.myPlayer=use.State.Player.whoAmI;orig(self);}
        finally{Main.myPlayer=player;}
    }

    static void PetBuffs(Action<ModPlayer> orig,ModPlayer self){
        // Lan's builds its global pet inventory UI in PreUpdateBuffs without
        // a local-player check. Companions have no inventory in that UI.
        if(CompanionNativeUse.Current?.Actor==self.Player)return;
        orig(self);
    }
}
