using System;
using System.Linq.Expressions;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using ThoriumMod;

namespace PalicoCompanion.Systems;

[ExtendsFromMod("ThoriumMod")]
[JITWhenModsEnabled("ThoriumMod")]
public sealed class ThoriumCompanionPatient:ModSystem {
    delegate void HealCall(Projectile shot,int amount,float radius,bool effects,bool bonus,object custom,Func<Player,bool> predicate,int specific,bool ignoreHealer,bool ignoreTarget,bool statistics);
    static Func<Projectile,Player,Player,float,Func<Player,bool>,int,bool,bool> eligible;
    static Func<Projectile,Player,int,bool,bool,bool,bool,object,bool> heal;
    ILHook healing;
    public override void Load(){
        var helper=typeof(ThoriumPlayer).Assembly.GetType("ThoriumMod.Utilities.ProjectileHelper");
        const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        eligible=helper.GetMethod("CanBeHealed",flags).CreateDelegate<Func<Projectile,Player,Player,float,Func<Player,bool>,int,bool,bool>>();
        var target=helper.GetMethod("ThoriumHealTarget",flags);
        var args=target.GetParameters();var parameters=new ParameterExpression[args.Length];var call=new Expression[args.Length];
        for(int i=0;i<args.Length;i++){
            parameters[i]=Expression.Parameter(i==7?typeof(object):args[i].ParameterType);
            call[i]=i==7?Expression.Convert(parameters[i],args[i].ParameterType):parameters[i];
        }
        heal=Expression.Lambda<Func<Projectile,Player,int,bool,bool,bool,bool,object,bool>>(Expression.Call(target,call),parameters).Compile();
        healing=new ILHook(helper.GetMethod("ThoriumHeal",flags),AppendPatients);
    }
    static void AppendPatients(ILContext il){
        var cursor=new ILCursor(il);
        // Keep the human-player pass intact. The server's owner-only early
        // return also reaches this hook; no client-supplied healing amounts.
        while(cursor.TryGotoNext(i=>i.OpCode==OpCodes.Ret)){
            for(int i=0;i<11;i++)cursor.Emit(OpCodes.Ldarg,i);
            cursor.EmitDelegate<HealCall>(HealCats);cursor.Index++;
        }
        // Companion IDs are private to the patient adapter. Never let them
        // enter Thorium's player-ID fallback, which otherwise heals everyone.
        cursor.Index=0;var native=cursor.DefineLabel();
        cursor.Emit(OpCodes.Ldarg,7);cursor.Emit(OpCodes.Ldc_I4,ThoriumPatientTargets.FirstPatient);
        cursor.Emit(OpCodes.Blt,native);
        for(int i=0;i<11;i++)cursor.Emit(OpCodes.Ldarg,i);
        cursor.EmitDelegate<HealCall>(HealCats);cursor.Emit(OpCodes.Ret);cursor.MarkLabel(native);
    }
    static void HealCats(Projectile shot,int amount,float radius,bool effects,bool bonus,object custom,Func<Player,bool> predicate,int specific,bool ignoreHealer,bool ignoreTarget,bool statistics){
        bool addressed=specific>=ThoriumPatientTargets.FirstPatient;
        // The native stabilizer completes all its bounces in one game tick.
        // Its spawn and kill can reach the server in the same batch, without
        // any server AI step. Deliver its chosen patient through Thorium's
        // normal addressed heal carrier, as Life Surge already does.
        if(addressed&&shot.ModProjectile is ThoriumMod.Projectiles.Healer.MolecularStabilizerPro&&Main.netMode!=Terraria.ID.NetmodeID.SinglePlayer){
            if(CompanionNetwork.Client&&shot.owner==Main.myPlayer&&ThoriumPatientTargets.State(specific) is {} target)
                Projectile.NewProjectile(shot.GetSource_FromThis(),target.Cat.Center,Microsoft.Xna.Framework.Vector2.Zero,
                    ModContent.ProjectileType<ThoriumMod.Projectiles.Healer.Heal>(),0,0,shot.owner,amount,specific);
            return;
        }
        if(CompanionNetwork.Client||!shot.active||shot.penetrate==0||specific>=0&&!addressed)return;
        if(!addressed&&CompanionOwnedShot.Of(shot) is {} caster){
            // Thorium's native pass already includes single-player actors.
            // Multiplayer actors use the sentinel outside its human-player
            // loop. Append that missing self target exactly once, retaining
            // the projectile's own self-healing flag and eligibility rules.
            // The enclosing native-use scope commits the actual HP change.
            var actor=caster.Actor;
            if(CompanionNativeUse.Current==caster&&caster.Available&&actor.whoAmI>=Main.maxPlayers&&
                eligible(shot,actor,actor,radius,predicate,-1,ignoreHealer)){
                heal(shot,actor,amount,effects,bonus,ignoreTarget,statistics,custom);
                shot.netUpdate=true;
                if(!shot.active)NetMessage.SendData(Terraria.ID.MessageID.KillProjectile,-1,-1,null,shot.identity,shot.owner);
            }
            return;
        }
        if(shot.owner<0||shot.owner>=Main.maxPlayers)return;
        var healer=Main.player[shot.owner];if(!healer.active||healer.dead)return;
        // Native human healing is owner-client driven. Let a finite projectile
        // finish that pass before the server consumes it for a nearby cat.
        if(!addressed&&CompanionNetwork.Server&&shot.penetrate>0)
            foreach(var human in Main.ActivePlayers)
                if(eligible(shot,healer,human,radius,predicate,-1,ignoreHealer))return;
        foreach(var projectile in Main.ActiveProjectiles){
            if(projectile.ModProjectile is not CompanionCat cat)continue;
            var owner=Main.player[cat.OwnerIndex];var state=owner.GetModPlayer<CompanionPlayer>();var use=cat.Weapons;
            if(use.State!=state||use.Cat!=projectile)continue;
            if(addressed&&(specific!=ThoriumPatientTargets.FirstPatient+cat.OwnerIndex||
                shot.ModProjectile is ThoriumMod.Projectiles.Healer.Heal&&!shot.GetGlobalProjectile<ThoriumPatientDelivery>().Matches(state)))continue;
            if(!owner.active||owner.dead||!state.Enabled||state.Stowed||state.Vitals.Downed||state.Vitals.Health>=state.Vitals.MaxHealth||
                healer!=owner&&healer.hostile&&owner.hostile&&(healer.team==0||healer.team!=owner.team))continue;
            // Use the body's position for receiving heals, not the weapon grip.
            var actor=use.Actor;var position=actor.Center;int before=state.Vitals.Health;
            try {
                using(new CompanionNativeUse.Scope(use,false)){
                    actor.Center=projectile.Center;actor.team=owner.team;actor.dead=false;
                    actor.statLife=before;actor.statLifeMax=actor.statLifeMax2=state.Vitals.MaxHealth;
                    if(!eligible(shot,healer,actor,radius,predicate,-1,ignoreHealer))continue;
                    int local=Main.myPlayer;
                    try {
                        Main.myPlayer=healer.whoAmI;
                        // Native healing, bonuses, custom amount and penetration.
                        // Player-only equipment proc packets cannot address a
                        // companion; do not send them to a borrowed player slot.
                        heal(shot,actor,amount,false,bonus,true,statistics,custom);
                    }finally{Main.myPlayer=local;}
                }
                int restored=Math.Clamp(actor.statLife-before,0,state.Vitals.MaxHealth-before);
                if(restored>0){state.Vitals.Health+=restored;CompanionFeedback.Show(1,projectile.Center,restored,"skill");projectile.netUpdate=true;}
                if(CompanionNetwork.Server){shot.netUpdate=true;if(!shot.active)NetMessage.SendData(Terraria.ID.MessageID.KillProjectile,-1,-1,null,shot.identity,shot.owner);}
            }finally{actor.Center=position;}
            if(!shot.active||shot.penetrate==0)break;
        }
    }
    public override void Unload(){healing?.Dispose();healing=null;eligible=null;heal=null;}
}
