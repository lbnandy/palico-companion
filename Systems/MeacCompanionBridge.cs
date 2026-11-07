using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class MeacCompanionBridge:ModSystem {
    ModPlayer input,melee;
    Type meleeProjectile;
    Func<ModPlayer,LinkedList<Vector2>> positions;
    Action<ModPlayer,Vector2> updateMouse;
    Func<ModPlayer,bool> usingMelee;
    Action<ModPlayer,bool> setUsingMelee;
    ConditionalWeakTable<Player,InputClock> clocks=new();
    sealed class InputClock { public bool Initialized;public ulong Tick; }

    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("MEAC",out var mod))return;
        input=mod.GetContent<ModPlayer>().FirstOrDefault(p=>p.GetType().FullName=="MEAC.Network.NetInput.NetPlayer");
        melee=mod.GetContent<ModPlayer>().FirstOrDefault(p=>p.GetType().FullName=="MEAC.MEACPlayer");
        meleeProjectile=mod.Code.GetType("MEAC.Projectiles.MeleeProj.MeleeProj");
        var mouse=input?.GetType().GetProperty("MouseWorld");
        var trail=mouse?.PropertyType.GetField("position");
        var update=mouse?.PropertyType.GetMethod("LocalUpdate",new[]{typeof(Vector2)});
        var busy=melee?.GetType().GetField("isUsingMeleeProj");
        var state=Expression.Parameter(typeof(ModPlayer));
        if(mouse?.GetGetMethod()!=null&&trail?.FieldType==typeof(LinkedList<Vector2>)&&update?.ReturnType==typeof(void)){
            var value=Expression.Property(Expression.Convert(state,input.GetType()),mouse);
            var aim=Expression.Parameter(typeof(Vector2));
            positions=Expression.Lambda<Func<ModPlayer,LinkedList<Vector2>>>(Expression.Field(value,trail),state).Compile();
            updateMouse=Expression.Lambda<Action<ModPlayer,Vector2>>(Expression.Call(value,update,aim),state,aim).Compile();
        }
        if(busy?.FieldType==typeof(bool)&&meleeProjectile!=null){
            var field=Expression.Field(Expression.Convert(state,melee.GetType()),busy);
            var value=Expression.Parameter(typeof(bool));
            usingMelee=Expression.Lambda<Func<ModPlayer,bool>>(field,state).Compile();
            setUsingMelee=Expression.Lambda<Action<ModPlayer,bool>>(Expression.Assign(field,value),state,value).Compile();
        }
        if(updateMouse!=null||usingMelee!=null)CompanionCrossMod.PrepareActor+=Prepare;
    }
    void Prepare(Player actor,Vector2 aim){
        var clock=clocks.GetOrCreateValue(actor);
        bool next=!clock.Initialized||clock.Tick!=Main.GameUpdateCount;
        clock.Initialized=true;clock.Tick=Main.GameUpdateCount;
        // MEAC reads its own mouse history, not Main.MouseWorld. Reuse its
        // update method once per game tick; extra AI/draw scopes only refine
        // the current sample, so projectile count cannot speed up the history.
        if(updateMouse!=null){
            var state=actor.GetModPlayer(input);var history=positions(state);
            if(next||history.Last==null)updateMouse(state,aim);
            else history.Last.Value=aim;
        }
        // MEAC.PreUpdate repairs this latch for human players after a held
        // projectile disappears. Actors do not run the whole player update.
        // Match that repair using companion ownership, not a borrowed slot or
        // Main.myPlayer, which could also match another companion's weapon.
        if(next&&usingMelee!=null){
            var state=actor.GetModPlayer(melee);
            if(usingMelee(state)){
                foreach(var p in Main.projectile)
                    if(p.active&&meleeProjectile.IsInstanceOfType(p.ModProjectile)&&CompanionOwnedShot.Of(p)?.Actor==actor)return;
                setUsingMelee(state,false);
            }
        }
    }
    public override void Unload(){
        CompanionCrossMod.PrepareActor-=Prepare;
        input=melee=null;meleeProjectile=null;positions=null;updateMouse=null;usingMelee=null;setUsingMelee=null;clocks=new();
    }
}
