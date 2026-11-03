using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class CallOfVoidCompanionBridge:ModSystem {
    ModPlayer input;
    Action<ModPlayer,Vector2> setAim;
    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("CalamityEntropy",out var mod))return;
        input=mod.GetContent<ModPlayer>().FirstOrDefault(p=>p.GetType().FullName=="CalamityEntropy.Common.EModPlayer");
        var type=input?.GetType();var mouse=type?.GetProperty("MouseWorld");
        if(mouse?.PropertyType!=typeof(Vector2)||mouse.GetSetMethod()==null)return;
        var state=Expression.Parameter(typeof(ModPlayer));var aim=Expression.Parameter(typeof(Vector2));
        setAim=Expression.Lambda<Action<ModPlayer,Vector2>>(
            Expression.Call(Expression.Convert(state,type),mouse.GetSetMethod(),aim),state,aim).Compile();
        CompanionCrossMod.PrepareActor+=Prepare;
    }
    // Call of Void caches aim relative to the player's mounted center. Supply
    // the same world-space input for the isolated actor on every peer, letting
    // its own setter and weapon AI handle offsets, turning and projectiles.
    void Prepare(Player actor,Vector2 aim)=>setAim(actor.GetModPlayer(input),aim);
    public override void Unload(){
        CompanionCrossMod.PrepareActor-=Prepare;input=null;setAim=null;
    }
}
