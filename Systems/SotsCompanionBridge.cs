using System;
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using SOTS.Prim;
using SOTS.Void;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

[ExtendsFromMod("SOTS")]
[JITWhenModsEnabled("SOTS")]
public sealed class SotsCompanionBridge:ModSystem {
    ILHook trailUpdate;
    public override void Load(){
        // SOTS updates primitives separately from Projectile.Update. Give that
        // pass the same short-lived owner context as native projectile AI.
        // Update is an inlineable one-line wrapper. Scope its call site so
        // every trail subclass retains its own OnUpdate implementation.
        if(!Main.dedServ)trailUpdate=new ILHook(typeof(PrimTrailManager).GetMethod(nameof(PrimTrailManager.UpdateTrails))!,ScopeTrails);
        CompanionCrossMod.BeforeReset+=PermanentResources;
        CompanionCrossMod.UpdateResources+=Resources;
        CompanionCrossMod.ResourceReady+=Ready;
    }
    public override void Unload(){
        trailUpdate?.Dispose();trailUpdate=null;
        CompanionCrossMod.BeforeReset-=PermanentResources;
        CompanionCrossMod.UpdateResources-=Resources;
        CompanionCrossMod.ResourceReady-=Ready;
    }
    void ScopeTrails(ILContext il){
        var cursor=new ILCursor(il);
        if(!cursor.TryGotoNext(i=>i.MatchCallvirt<PrimTrail>(nameof(PrimTrail.Update)))){
            Mod.Logger.Warn("SOTS primitive update call not found; trail manager left unchanged.");return;
        }
        cursor.Remove();cursor.EmitDelegate<Action<PrimTrail>>(UpdateTrail);
    }
    static void UpdateTrail(PrimTrail trail){
        var shot=trail.Entity as Projectile;
        var use=shot==null?null:CompanionOwnedShot.Of(shot);
        if(use==null||!shot.active||!use.CanMaintainShot(shot)||!use.EnsureActorSlot()){
            trail.Update();return;
        }
        using(new CompanionNativeUse.Scope(use))trail.Update();
    }
    static void PermanentResources(CompanionNativeUse use){
        var resource=use.Actor.GetModPlayer<VoidPlayer>();
        var owner=use.State.Player.GetModPlayer<VoidPlayer>();
        // ResetEffects rebuilds the maximum; supply permanent progression
        // beforehand so later potion and equipment bonuses are not overwritten.
        resource.voidMeterMax=Math.Max(100,owner.voidMeterMax);
        resource.voidAnkh=owner.voidAnkh;resource.voidStar=owner.voidStar;resource.voidSoul=owner.voidSoul;
    }
    static void Resources(CompanionNativeUse use){
        if(CompanionNetwork.Client)return;
        var resource=use.Actor.GetModPlayer<VoidPlayer>();
        // SOTS.ResetEffects advances regeneration. Only reserve this cat's
        // minion capacity here; never borrow the owner's current meter.
        using(new CompanionNativeUse.Scope(use))resource.VoidMinionConsumption=resource.RegisterVoidMinions();
        resource.voidMeterMax2-=resource.VoidMinionConsumption;
        resource.safetySwitch=true;
    }
    static bool Ready(CompanionNativeUse use){
        if(use.Actor.HeldItem.ModItem is not VoidItem item)return true;
        // Void summons reserve capacity instead of paying the current meter.
        // VoidItem.CanUseItem owns that native capacity check.
        if(item.Item.CountsAsClass(DamageClass.Summon))return true;
        var resource=use.Actor.GetModPlayer<VoidPlayer>();
        // A delayed/channelled use can pay from projectile AI. Release input
        // when depleted; leave its native completion and regeneration intact.
        if(resource.voidMeter<0)return false;
        return use.Actor.itemAnimation>0||resource.voidMeter>=item.VoidCost(use.Actor);
    }
}
