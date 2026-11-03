using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class TerrariaOverhaulCompanionBridge:ModSystem {
    Hook footsteps,dodgerolls;
    ModPlayer direction;
    Action<ModPlayer,Vector2> setAim;
    public override void Load(){
        if(!ModLoader.TryGetMod("TerrariaOverhaul",out var overhaul))return;
        const BindingFlags flags=BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly;
        if(!Main.dedServ){
            var step=overhaul.Code.GetType("TerrariaOverhaul.Common.Footsteps.PlayerFootsteps")?
                .GetMethod(nameof(ModPlayer.PostItemCheck),flags,null,Type.EmptyTypes,null);
            if(step!=null)footsteps=new Hook(step,(Action<Action<ModPlayer>,ModPlayer>)Footsteps);
        }
        var dodge=overhaul.Code.GetType("TerrariaOverhaul.Common.Dodgerolls.PlayerDodgerolls")?
            .GetMethod(nameof(ModPlayer.PreItemCheck),flags,null,Type.EmptyTypes,null);
        if(dodge!=null)dodgerolls=new Hook(dodge,(Func<Func<ModPlayer,bool>,ModPlayer,bool>)Dodgerolls);
    }
    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("TerrariaOverhaul",out var overhaul))return;
        direction=overhaul.GetContent<ModPlayer>().FirstOrDefault(p=>p.GetType().FullName=="TerrariaOverhaul.Common.Movement.PlayerDirectioning");
        var type=direction?.GetType();
        if(type?.GetProperty("MouseWorld")?.PropertyType!=typeof(Vector2)||type.GetProperty("LookPosition")?.PropertyType!=typeof(Vector2))return;
        var mouse=type?.GetProperty("MouseWorld")?.GetSetMethod(true);
        var look=type?.GetProperty("LookPosition")?.GetSetMethod(true);
        if(mouse==null||look==null)return;
        var state=Expression.Parameter(typeof(ModPlayer));var aim=Expression.Parameter(typeof(Vector2));
        var receiver=Expression.Convert(state,type);
        setAim=Expression.Lambda<Action<ModPlayer,Vector2>>(Expression.Block(
            Expression.Call(receiver,mouse,aim),Expression.Call(receiver,look,aim)),state,aim).Compile();
        CompanionCrossMod.PrepareActor+=Prepare;
    }
    // Populate the same cache Overhaul receives from human input/networking.
    // Reuse the native actor setup path; no player packets or collision changes.
    // Cached setters also work when Overhaul's property reads are JIT-inlined.
    void Prepare(Player actor,Vector2 aim)=>setAim(actor.GetModPlayer(direction),aim);
    static bool IsActor(ModPlayer state)=>ReferenceEquals(state.Player,CompanionNativeUse.Current?.Actor)||
        ReferenceEquals(state.Player,CompanionFishing.Current?.Actor);
    // The cat has its own movement/dodge controller. Its ItemCheck must not
    // consume human roll input, which does not exist on a dedicated server.
    static bool Dodgerolls(Func<ModPlayer,bool> original,ModPlayer state)=>IsActor(state)||original(state);
    static void Footsteps(Action<ModPlayer> original,ModPlayer state){
        // These Players only execute native weapons/tools, not human movement.
        // Skip Overhaul's movement audio for them, including takeoff/landing;
        // keep every real player's footsteps and all item sounds untouched.
        if(IsActor(state))return;
        original(state);
    }
    public override void Unload(){
        footsteps?.Dispose();dodgerolls?.Dispose();footsteps=dodgerolls=null;
        CompanionCrossMod.PrepareActor-=Prepare;direction=null;setAim=null;
    }
}
