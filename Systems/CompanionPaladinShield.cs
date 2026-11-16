using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Extend native Paladin selection to isolated companion actors. Equipment still
// sets hasPaladinShield; upgrades and mods using that effect need no item list.
public sealed class CompanionPaladinShield : ModSystem {
    internal const byte TransferPacket=201;
    const float Radius=800;
    Hook modifiersHook,effectsHook;
    static Protection current;
    static int transferring;
    delegate void ModifiersOriginal(Player player,ref Player.HurtModifiers modifiers);
    delegate void EffectsOriginal(Player player,Player.HurtInfo info);
    public override void Load(){
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        modifiersHook=new Hook(typeof(Player).GetMethod("ApplyVanillaHurtEffectModifiers",flags),Modifiers);
        effectsHook=new Hook(typeof(Player).GetMethod("OnHurt_Part2",flags),Effects);
    }
    public override void Unload(){modifiersHook?.Dispose();effectsHook?.Dispose();modifiersHook=effectsHook=null;current=null;transferring=0;}

    readonly record struct Bearer(Player Player,CompanionNativeUse Cat){
        internal Vector2 Center=>Cat?.Cat.Center??Player.Center;
        internal int Order=>Cat==null?Player.whoAmI:Main.maxPlayers+Player.whoAmI;
    }
    static CompanionNativeUse VictimCat(Player player)=>CompanionNativeUse.Current is {} use&&use.Actor==player?use:null;
    static bool Allies(Player a,Player b)=>a==b||a.team!=0&&a.team==b.team;
    static bool Eligible(Player player)=>player.active&&!player.dead&&!player.immune&&player.hasPaladinShield&&player.statLife>player.statLifeMax2*.25f;
    static bool Eligible(CompanionNativeUse use)=>use.Present&&!use.State.Stowed&&use.Actor.hasPaladinShield&&
        use.State.Vitals.Health>use.State.Vitals.MaxHealth*.25f&&use.State.Vitals.Immunity<=0&&!use.Actor.immune;
    static Bearer? Find(Player victim){
        var cat=VictimCat(victim);var owner=cat?.State.Player??victim;var center=cat?.Cat.Center??victim.Center;
        Bearer? nearest=null;float distance=Radius*Radius;
        void Consider(Bearer b){
            float d=Vector2.DistanceSquared(center,b.Center);
            if(d<distance||d==distance&&nearest.HasValue&&b.Order<nearest.Value.Order){nearest=b;distance=d;}
        }
        for(int i=0;i<Main.maxPlayers;i++){
            var human=Main.player[i];
            if(!human.active||human==CompanionNativeUse.Current?.Actor||!Allies(owner,human))continue;
            if(human!=victim&&Eligible(human))Consider(new(human,null));
            if(human.GetModPlayer<CompanionPlayer>().Cat?.ModProjectile is CompanionCat companion&&companion.Weapons!=cat&&Eligible(companion.Weapons))
                Consider(new(human,companion.Weapons));
        }
        return nearest;
    }

    internal sealed class Protection : IDisposable {
        readonly Protection previous;
        internal readonly Player Victim;
        readonly Bearer? bearer;
        internal bool Handles=>bearer.HasValue||VictimCat(Victim)!=null;
        internal bool Reduces=>bearer.HasValue;
        internal Protection(Player victim){
            Victim=victim;previous=current;
            bool authority=VictimCat(victim)!=null?!CompanionNetwork.Client:!CompanionNetwork.Server&&victim.whoAmI==Main.myPlayer;
            if(transferring==0&&authority){var found=Find(victim);if(VictimCat(victim)!=null||found?.Cat!=null)bearer=found;}
            current=this;
        }
        internal void Transfer(Player.HurtInfo info){
            if(!bearer.HasValue||info.Cancelled)return;
            // Native transfer uses a third of the reduced hit, then evaluates
            // the bearer's own defense, endurance, dodges and death prevention.
            var target=bearer.Value;int damage=info.Damage/3;
            if(CompanionNetwork.Client){Send(target.Player.whoAmI,((CompanionCat)target.Cat.Cat.ModProjectile).NetworkInstance,damage);return;}
            if(target.Cat!=null){Take(target.Cat,damage);return;}
            if(CompanionNetwork.Server)Send(target.Player.whoAmI,0,damage,target.Player.whoAmI);
            else Take(target.Player,damage);
        }
        public void Dispose()=>current=previous;
    }
    internal static Protection Protect(Player player)=>new(player);
    static void Modifiers(ModifiersOriginal orig,Player player,ref Player.HurtModifiers modifiers){
        bool handled=current?.Victim==player&&current.Handles;
        bool flag=player.defendedByPaladin;
        try{if(handled||transferring>0)player.defendedByPaladin=false;orig(player,ref modifiers);}
        finally{player.defendedByPaladin=flag;}
        if(transferring==0&&handled&&current.Reduces)modifiers.FinalDamage*=.75f;
    }
    static void Effects(EffectsOriginal orig,Player player,Player.HurtInfo info){
        bool flag=player.defendedByPaladin;
        // A replicated human hit must not also charge a human shield bearer
        // when the closer companion was selected by the victim's client.
        bool handled=transferring>0||current?.Victim==player&&current.Handles||
            flag&&player.whoAmI!=Main.myPlayer&&Find(player)?.Cat!=null;
        try{if(handled)player.defendedByPaladin=false;orig(player,info);}
        finally{player.defendedByPaladin=flag;}
        if(current?.Victim==player&&transferring==0)current.Transfer(info);
    }
    static void Take(CompanionNativeUse cat,int damage){
        transferring++;
        try{cat.ReceiveSharedDamage(damage);CompanionNetwork.Send(cat.State);}
        finally{transferring--;}
    }
    static void Take(Player human,int damage){
        transferring++;int local=Main.myPlayer;
        try{
            Main.myPlayer=human.whoAmI;
            // The transferred hit runs native hooks, but is not broadcast as a
            // fresh attack that other shield wearers could transfer again.
            human.Hurt(PlayerDeathReason.LegacyEmpty(),damage,0,quiet:true);
            if(CompanionNetwork.Client)NetMessage.SendData(MessageID.PlayerLifeMana,number:human.whoAmI);
        }finally{Main.myPlayer=local;transferring--;}
    }
    static void Send(int owner,ulong instance,int damage,int to=-1){
        var packet=ModContent.GetInstance<PalicoCompanion>().GetPacket();packet.Write(TransferPacket);
        packet.Write((byte)owner);packet.Write(instance);packet.Write(damage);packet.Send(to);
    }
    internal static void Receive(BinaryReader reader,int sender){
        int owner=reader.ReadByte();ulong instance=reader.ReadUInt64();int damage=reader.ReadInt32();
        if(owner>=Main.maxPlayers||damage<0)return;
        if(CompanionNetwork.Client){if(owner==Main.myPlayer&&instance==0&&!Main.LocalPlayer.dead)Take(Main.LocalPlayer,damage);return;}
        if(!CompanionNetwork.Server||sender<0||sender>=Main.maxPlayers)return;
        var victim=Main.player[sender];var state=Main.player[owner].GetModPlayer<CompanionPlayer>();
        if(!victim.active||!state.NetReady||state.NetLeaving||state.Cat?.ModProjectile is not CompanionCat cat||cat.NetworkInstance!=instance||instance==0)return;
        var nearest=Find(victim);
        if(nearest?.Cat==cat.Weapons)Take(cat.Weapons,damage);
    }
}

public sealed partial class CompanionNativeUse {
    internal void ReceiveSharedDamage(int damage){
        var v=State.Vitals;
        if(CompanionNetwork.Client||!Present||v.Immunity>0||!EnsureActorSlot())return;
        using var scope=new Scope(this,false);
        var position=Actor.position;int width=Actor.width,height=Actor.height;bool receiving=ReceivingHit;
        try{
            ReceivingHit=true;Actor.position=Cat.position;Actor.width=Cat.width;Actor.height=Cat.height;
            Actor.dead=false;Actor.immune=false;Actor.immuneTime=0;Array.Clear(Actor.hurtCooldowns);
            int before=Actor.statLife=v.Health;
            Actor.Hurt(PlayerDeathReason.LegacyEmpty(),damage,0,quiet:true);
            CommitHitHealth(before,CompanionIncomingDamage.BossNear(State.Player),60,CompanionNetwork.Server);
        }finally{
            ReceivingHit=receiving;Actor.position=position;Actor.width=width;Actor.height=height;Actor.statLife=v.Health;Actor.dead=false;
        }
    }
}
