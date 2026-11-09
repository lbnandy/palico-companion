using System;
using FargowiltasSouls.Content.UI.Elements;
using FargowiltasSouls.Core.AccessoryEffectSystem;
using FargowiltasSouls.Core.ModPlayers;
using FargowiltasSouls.Core.Toggler;
using FargowiltasSouls.Core.Globals;
using FargowiltasSouls.Core.Systems;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;

namespace PalicoCompanion.Systems;

[ExtendsFromMod("FargowiltasSouls")]
[JITWhenModsEnabled("FargowiltasSouls")]
public sealed class FargoEquipmentBridge : ModSystem {
    Hook cooldownBar,dashes,auras;
    delegate void Aura(NPC npc,float radius,bool reverse,int dust,Color color,int[] buffs);
    delegate void ActivateBar(string name,Texture2D texture,Color color,Func<float> ratio,bool full,int delay,Func<bool> active,int frames);
    public override void Load(){
        cooldownBar=new Hook(((ActivateBar)CooldownBarManager.Activate).Method,ShowCooldownBar);
        auras=new Hook(((Aura)EModeGlobalNPC.Aura).Method,ApplyAura);
        dashes=new Hook(((Action<Player>)DashManager.ManageDashes).Method,ManageDashes);
        CompanionCrossMod.BeforeReset+=BeforeReset;
        CompanionCrossMod.UpdateEquipment+=UpdateEquipment;
        CompanionCrossMod.UpdateInheritedStatus+=UpdateInheritedStatus;
    }
    public override void Unload(){
        auras?.Dispose();auras=null;
        cooldownBar?.Dispose();cooldownBar=null;
        dashes?.Dispose();dashes=null;
        CompanionCrossMod.BeforeReset-=BeforeReset;
        CompanionCrossMod.UpdateEquipment-=UpdateEquipment;
        CompanionCrossMod.UpdateInheritedStatus-=UpdateInheritedStatus;
    }
    static void ApplyAura(Aura orig,NPC npc,float radius,bool reverse,int dust,Color color,int[] buffs){
        orig(npc,radius,reverse,dust,color,buffs);
        if(CompanionNativeUse.Current!=null||Main.gameMenu)return;
        // Reuse Fargo's live radius, reverse-area rules, duration and immunity
        // checks. Do not replay NPC AI or copy the human owner's debuffs.
        foreach(var projectile in Main.ActiveProjectiles){
            if(projectile.ModProjectile is not CompanionCat cat||!cat.Weapons.Present)continue;
            using(new CompanionNativeUse.Scope(cat.Weapons,false,equipmentOwner:true))
                orig(npc,radius,reverse,-1,color,buffs);
        }
    }
    static void ManageDashes(Action<Player> orig,Player player){
        // Companion movement belongs to its AI, not the local player's dash
        // keybinds. Leave the rest of Fargo's equipment callback running.
        if(CompanionNativeUse.Current?.Actor==player)return;
        orig(player);
    }
    static void ShowCooldownBar(ActivateBar orig,string name,Texture2D texture,Color color,Func<float> ratio,bool full,int delay,Func<bool> active,int frames){
        // Equipment callbacks also request human HUD bars. A virtual actor has
        // no such HUD (and dedicated servers have no UI instance at all).
        // Suppress only that presentation side effect, not native passives.
        if(CompanionNativeUse.Current!=null)return;
        orig(name,texture,color,ratio,full,delay,active,frames);
    }
    static void BeforeReset(CompanionNativeUse use){
        var player=use.Actor.GetModPlayer<FargoSoulsPlayer>();
        if(!player.Toggler.Initialized){
            // TryLoad/LoadPlayerToggles share a global dictionary and local UI
            // preferences. Give this actor independent native defaults instead;
            // neither the owner's switches nor another cat's can change them.
            foreach(var pair in ToggleLoader.LoadedToggles)
                player.Toggler.Toggles.Add(pair.Key,new Toggle(pair.Key,pair.Value.Mod));
            player.Toggler.Initialized=true;
            // The effect dispatcher's PostUpdate otherwise opens the human
            // skill-assignment UI. Companions do not use hotkey skills.
            player.HasEquippedSkill=true;
        }
        if(!CompanionEquipment.Independent(use.State))return;
        using var scope=new CompanionNativeUse.Scope(use,false);
        // Fargo PreUpdate also handles NPC dialogue, teleportation and mounts.
        // Keep only its passive hit clock/standing condition; effect callbacks
        // themselves run through Fargo's registered dispatcher.
        if(player.HurtTimer>0)player.HurtTimer--;
        player.IsStandingStill=Math.Abs(use.Actor.velocity.X)<.05f&&Math.Abs(use.Actor.velocity.Y)<.05f;
        use.Actor.GetModPlayer<AccessoryEffectPlayer>().PreUpdate();
    }
    static void UpdateInheritedStatus(CompanionNativeUse use){
        // Native curse/resource processing also applies without independent gear.
        use.Actor.GetModPlayer<FargoSoulsPlayer>().PostUpdateMiscEffects();
    }
    static void UpdateEquipment(CompanionNativeUse use){
        var player=use.Actor.GetModPlayer<FargoSoulsPlayer>();
        var effects=use.Actor.GetModPlayer<AccessoryEffectPlayer>();
        // Existing equipment evaluation owns the scope and calls this once.
        // Reuse registered passives, cooldowns and conditional healing rather
        // than reproducing individual enchantments or endgame attacks.
        player.PostUpdateMiscEffects();
        effects.PostUpdateMiscEffects();
        player.PostUpdate();
        effects.PostUpdate();
    }
}
