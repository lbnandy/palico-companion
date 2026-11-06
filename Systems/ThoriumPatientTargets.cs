using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ThoriumMod;
using ThoriumMod.Projectiles.Healer;

namespace PalicoCompanion.Systems;

// Read-only patient views for native healer targeting. These indices belong to
// this adapter, never Main.player or Terraria's player network messages.
[ExtendsFromMod("ThoriumMod")]
[JITWhenModsEnabled("ThoriumMod")]
public sealed class ThoriumPatientTargets:ModSystem {
    internal const int FirstPatient=256;
    const int PatientLimit=FirstPatient+255;
    static Player[] patients;
    static Player missing;
    static ulong snapshotTick=ulong.MaxValue;
    static CompanionNativeUse snapshotScope;
    readonly List<ILHook> hooks=new();
    Hook eligibility;
    static CheckPatient checkPatient;
    delegate bool Eligible(Projectile shot,Player healer,Player patient,float radius,Func<Player,bool> predicate,int specific,bool ignoreHealer);
    delegate void CheckPatient(HomingPro projectile,Player healer,Player patient,ref Player target,ref float distance);

    public override void Load(){
        patients=new Player[PatientLimit];missing=new Player {active=false};
        // Only audited native methods: preserve their own range, homing,
        // penetration, secondary attacks, amounts and per-patient limits.
        foreach(var type in new[]{typeof(LifeSurgeHeal),typeof(LifeSurgeHealExtra),typeof(CosmicFluxStaffPro),
            typeof(TheGigaNeedlePro),typeof(LifeOrb),typeof(Heal),typeof(HealNoBonus),typeof(HealNoEffects)})
            Patch(type,"AI");
        Patch(typeof(UnboundFantasyPro),"OnKill");
        Patch(typeof(MolecularStabilizerPro),"Heal");
        Patch(typeof(MolecularStabilizerPro),"CheckNearestPlayer");
        // This field guards the call itself, before the common healer hook.
        // Permit the server to enter it; native human heals still retain the
        // owner check inside ThoriumHeal, while cat HP is server-authoritative.
        hooks.Add(new ILHook(typeof(LifeZone).GetMethod("AI"),FieldAuthority));
        var helper=typeof(ThoriumPlayer).Assembly.GetType("ThoriumMod.Utilities.ProjectileHelper");
        eligibility=new Hook(helper.GetMethod("CanBeHealed",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic),CanHeal);
        checkPatient=typeof(HomingPro).GetMethod("CheckPlayer",BindingFlags.Instance|BindingFlags.NonPublic).CreateDelegate<CheckPatient>();
        hooks.Add(new ILHook(typeof(HomingPro).GetMethod("AI"),il=>{
            var cursor=new ILCursor(il);
            while(cursor.TryGotoNext(i=>i.MatchCall<HomingPro>("CheckPlayer"))){ReplaceCall(il,cursor.Next,nameof(CheckHomingPatient));cursor.Index++;}
        }));
    }
    void Patch(Type type,string name)=>hooks.Add(new ILHook(type.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly),ExpandPatients));
    public override void Unload(){foreach(var hook in hooks)hook.Dispose();hooks.Clear();eligibility?.Dispose();eligibility=null;checkPatient=null;patients=null;missing=null;snapshotTick=ulong.MaxValue;snapshotScope=null;}
    internal static CompanionPlayer State(int id){
        int owner=id-FirstPatient;
        if(owner<0||owner>=Main.maxPlayers)return null;
        var player=Main.player[owner];if(!player.active||player.dead)return null;
        var state=player.GetModPlayer<CompanionPlayer>();
        return state.Enabled&&!state.Stowed&&!state.Vitals.Downed&&state.Cat?.ModProjectile is CompanionCat?state:null;
    }
    static Player[] Patients(){
        if(snapshotTick==Main.GameUpdateCount&&snapshotScope==CompanionNativeUse.Current)return patients;
        snapshotTick=Main.GameUpdateCount;snapshotScope=CompanionNativeUse.Current;
        Array.Copy(Main.player,patients,FirstPatient);
        for(int i=FirstPatient;i<PatientLimit;i++){
            var state=State(i);
            // A native-use scope already exposes this actor in Main.player;
            // do not offer the same patient twice to an expanded target loop.
            if(state==null||CompanionNativeUse.Current?.State==state){if(patients[i]!=null)patients[i].active=false;patients[i]??=missing;continue;}
            var view=patients[i];if(view==null||ReferenceEquals(view,missing))patients[i]=view=new Player();
            view.whoAmI=i;view.active=true;view.dead=false;view.ghost=false;
            view.statLife=state.Vitals.Health;view.statLifeMax=view.statLifeMax2=state.Vitals.MaxHealth;
            view.position=state.Cat.position;view.width=state.Cat.width;view.height=state.Cat.height;
            view.team=state.Player.team;view.hostile=state.Player.hostile;
        }
        return patients;
    }
    static bool CanHeal(Eligible orig,Projectile shot,Player healer,Player target,float radius,Func<Player,bool> predicate,int specific,bool ignoreHealer){
        if(target.whoAmI>=FirstPatient){
            var state=State(target.whoAmI);if(state==null)return false;
            var caster=shot!=null?CompanionOwnedShot.Of(shot):CompanionNativeUse.Current;
            if(ignoreHealer&&caster?.State==state)return false;
            var owner=caster?.State.Player??healer;
            if(owner!=state.Player&&owner.hostile&&state.Player.hostile&&(owner.team==0||owner.team!=state.Player.team))return false;
            target.statLife=state.Vitals.Health;target.statLifeMax2=state.Vitals.MaxHealth;
        }
        return orig(shot,healer,target,radius,predicate,specific,ignoreHealer);
    }
    static void CheckHomingPatient(HomingPro projectile,Player healer,Player patient,ref Player target,ref float distance){
        checkPatient(projectile,healer,patient,ref target,ref distance);
        // The owner is checked once in both the solo and multiplayer paths.
        // Reuse each projectile's virtual eligibility and distance calculation.
        if(!ReferenceEquals(healer,patient))return;
        var views=Patients();
        for(int i=FirstPatient;i<PatientLimit;i++)if(views[i].active)
            checkPatient(projectile,healer,views[i],ref target,ref distance);
    }
    static void ExpandPatients(ILContext il){
        var c=new ILCursor(il);
        while(c.TryGotoNext(i=>i.MatchLdsfld<Main>(nameof(Main.player)))){
            ReplaceCall(il,c.Next,nameof(Patients));c.Index++;
        }
        c.Index=0;
        while(c.TryGotoNext(i=>i.MatchLdcI4(255)&&i.Next!=null&&
            (i.Next.OpCode==OpCodes.Blt||i.Next.OpCode==OpCodes.Blt_S))){
            c.Next.Operand=PatientLimit;c.Index++;
        }
    }
    static void ReplaceCall(ILContext il,Instruction instruction,string method){
        instruction.OpCode=OpCodes.Call;
        instruction.Operand=il.Import(typeof(ThoriumPatientTargets).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic));
    }
    static void FieldAuthority(ILContext il){
        var c=new ILCursor(il);
        while(c.TryGotoNext(MoveType.After,i=>i.MatchLdsfld<Main>(nameof(Main.myPlayer)))){
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<int,ModProjectile,int>>((local,field)=>CompanionNetwork.Server?field.Projectile.owner:local);
        }
    }
}

// Keep an addressed heal attached to the same cat if it is recalled or its
// owner disconnects. An owner slot alone is not a lasting patient identity.
[ExtendsFromMod("ThoriumMod")]
[JITWhenModsEnabled("ThoriumMod")]
public sealed class ThoriumPatientDelivery:GlobalProjectile {
    public override bool InstancePerEntity=>true;
    bool bound;int identity;ulong instance;
    public override bool AppliesToEntity(Projectile entity,bool lateInstantiation)=>entity.ModProjectile is Heal;
    public override void OnSpawn(Projectile shot,IEntitySource source){
        if(source is EntitySource_Parent&&ThoriumPatientTargets.State((int)shot.ai[1]) is {} state){
            bound=true;identity=state.Cat.identity;instance=((CompanionCat)state.Cat.ModProjectile).NetworkInstance;
        }
    }
    internal bool Matches(CompanionPlayer state)=>bound&&state.Cat.identity==identity&&((CompanionCat)state.Cat.ModProjectile).NetworkInstance==instance;
    public override void SendExtraAI(Projectile shot,BitWriter bits,BinaryWriter writer){bits.WriteBit(bound);if(bound){writer.Write(identity);writer.Write(instance);}}
    public override void ReceiveExtraAI(Projectile shot,BitReader bits,BinaryReader reader){bound=bits.ReadBit();if(bound){identity=reader.ReadInt32();instance=reader.ReadUInt64();}}
    public override bool PreAI(Projectile shot){
        if(shot.ai[1]<ThoriumPatientTargets.FirstPatient)return true;
        var state=ThoriumPatientTargets.State((int)shot.ai[1]);
        if(state!=null&&Matches(state))return true;
        if(!CompanionNetwork.Client)shot.Kill();return false;
    }
}
