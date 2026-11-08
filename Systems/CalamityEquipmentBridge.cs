using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CalamityMod.CalPlayer;
using CalamityMod.Cooldowns;
using CalamityMod.DataStructures;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Projectiles.Summon;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityEquipmentBridge : ModSystem {
    delegate void StackingDebuff(NPC npc,int buffType,ref int buffIndex,ref int damage);
    Action<CalamityPlayer> buffs,shields,defense,limits;
    static Action<CalamityPlayer> Native(string name)=>typeof(CalamityPlayer)
        .GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate<Action<CalamityPlayer>>();
    public override void Load(){
        // Calamity separates these passive stages from movement, environment,
        // UI and input in PostUpdateMiscEffects. Reuse just those stages.
        buffs=Native("OtherBuffEffects");shields=Native("EnergyShields");
        defense=Native("DefenseEffects");limits=Native("Limits");
        MonoModHooks.Add(((StackingDebuff)ShellfishClaps.ShellfishStacking).Method,
            (StackingDebuffHook)ShellfishOwner);
        CompanionCrossMod.BeforeReset+=Tick;
        CompanionCrossMod.UpdateEquipment+=UpdateEquipment;
        MonoModHooks.Add(typeof(GemTechArmorState).GetProperty(nameof(GemTechArmorState.HasInvalidOwner)).GetMethod,
            (Func<Func<GemTechArmorState,bool>,GemTechArmorState,bool>)InvalidGemOwner);
        // Cooldowns belong to the isolated actor. Calamity's player packets
        // cannot represent it; gameplay and feedback use our existing sync.
        MonoModHooks.Add(typeof(CalamityPlayer).GetMethod(nameof(CalamityPlayer.SyncCooldownAddition)),
            (Action<Action<CalamityPlayer,bool,CooldownInstance>,CalamityPlayer,bool,CooldownInstance>)Add);
        MonoModHooks.Add(typeof(CalamityPlayer).GetMethod(nameof(CalamityPlayer.SyncCooldownRemoval)),
            (Action<Action<CalamityPlayer,bool,IList<string>>,CalamityPlayer,bool,IList<string>>)Remove);
        MonoModHooks.Add(typeof(CalamityPlayer).GetMethod(nameof(CalamityPlayer.SyncCooldownDictionary)),
            (Action<Action<CalamityPlayer,bool>,CalamityPlayer,bool>)Sync);
    }
    public override void Unload(){
        CompanionCrossMod.BeforeReset-=Tick;
        CompanionCrossMod.UpdateEquipment-=UpdateEquipment;
        buffs=shields=defense=limits=null;
    }
    delegate void StackingDebuffHook(StackingDebuff orig,NPC npc,int buffType,ref int buffIndex,ref int damage);
    static void ShellfishOwner(StackingDebuff orig,NPC npc,int buffType,ref int buffIndex,ref int damage){
        // NPC regeneration runs outside projectile AI. Match the native
        // last-attached-projectile owner selection, then reuse its complete
        // stacking/scaling calculation with that companion's actor installed.
        Projectile source=null;
        foreach(var shot in Main.ActiveProjectiles)
            if(shot.type==ModContent.ProjectileType<Shellfish>()&&shot.ai[0]==1f&&shot.ai[1]==npc.whoAmI)source=shot;
        // No attached shellfish means zero contribution. Native code otherwise
        // reads player 255 while a lingering debuff waits to expire.
        if(source==null)return;
        if(CompanionProjectileData.TryGet<CompanionOwnedShot>(source,out var tag)&&tag.CatIdentity>=0&&tag.Context==null)return;
        var use=CompanionOwnedShot.Of(source);
        if(use==null){orig(npc,buffType,ref buffIndex,ref damage);return;}
        if(!use.EnsureActorSlot())return;
        using(new CompanionNativeUse.Scope(use,trackHealing:false))orig(npc,buffType,ref buffIndex,ref damage);
    }
    void UpdateEquipment(CompanionNativeUse use){
        var player=use.Actor.GetModPlayer<CalamityPlayer>();
        // OtherBuffEffects temporarily changes the global persistence table
        // for Amalgam and jump height for Bounding. Those must not leak to a
        // real player or another companion, even if an addon hook throws.
        var persistent=Main.persistentBuff;
        var saved=ArrayPool<bool>.Shared.Rent(persistent.Length);
        Array.Copy(persistent,saved,persistent.Length);
        int jumpHeight=Player.jumpHeight;
        try {
            buffs(player);
            if(!CompanionNetwork.Client)shields(player);
            defense(player);limits(player);
            player.GemTechState.Update();
        } finally {
            Array.Copy(saved,persistent,persistent.Length);
            ArrayPool<bool>.Shared.Return(saved);
            Player.jumpHeight=jumpHeight;
        }
    }
    static bool IsActor(CalamityPlayer player)=>CompanionNativeUse.Current?.Actor==player.Player;
    static bool InvalidGemOwner(Func<GemTechArmorState,bool> orig,GemTechArmorState state){
        // Native validity excludes the server sentinel. It is a valid owner
        // only while our existing scope has installed this actor in that slot.
        var actor=CompanionNativeUse.Current?.Actor;
        if(actor!=null&&state.OwnerIndex==actor.whoAmI&&ReferenceEquals(state.Owner,actor))return false;
        return orig(state);
    }
    static void Add(Action<CalamityPlayer,bool,CooldownInstance> orig,CalamityPlayer player,bool server,CooldownInstance cooldown){if(!IsActor(player))orig(player,server,cooldown);}
    static void Remove(Action<CalamityPlayer,bool,IList<string>> orig,CalamityPlayer player,bool server,IList<string> ids){if(!IsActor(player))orig(player,server,ids);}
    static void Sync(Action<CalamityPlayer,bool> orig,CalamityPlayer player,bool server){if(!IsActor(player))orig(player,server);}
    static void Tick(CompanionNativeUse use){
        if(CompanionNetwork.Client)return;
        var player=use.Actor.GetModPlayer<CalamityPlayer>();
        // This proc clock lives in MiscEffects, mixed with human movement and
        // world interactions. Its OnHitNPC hook still owns heart generation.
        if(CompanionEquipment.Independent(use.State)&&player.bloodflareSet&&player.bloodflareHeartTimer>0)
            player.bloodflareHeartTimer--;
        var cooldowns=player.cooldowns;
        if(cooldowns.Count==0)return;
        using var scope=new CompanionNativeUse.Scope(use);
        // Reuse every registered cooldown's own policy and completion hook,
        // including addon handlers, instead of maintaining an accessory list.
        foreach(var pair in cooldowns.ToArray()){
            var cooldown=pair.Value;var handler=cooldown.handler;
            if(handler.CanTickDown)cooldown.timeLeft--;
            handler.Tick();
            if(cooldown.timeLeft>=0)continue;
            handler.OnCompleted();
            if(!Main.dedServ&&handler.ShouldPlayEndSound&&handler.EndSound is {} sound)
                SoundEngine.PlaySound(sound,use.Cat.Center);
            cooldowns.Remove(pair.Key);
        }
    }
}
