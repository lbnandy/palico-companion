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
    Type holdoutItem;
    Func<ModItem,int> holdoutType;
    int phantomBuff,phantomType;
    Func<int> phantomDamage;
    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("CalamityEntropy",out var mod)&&!ModLoader.TryGetMod("CalamityEntropyLegacy",out mod))return;
        PreparePhantom(mod);
        if(!Main.dedServ){
            var rendererType=mod.Code.GetType(mod.Name+".Content.Projectiles.YstralynProj");
            var draw=rendererType?.GetMethod("draw_crack",Type.EmptyTypes);
            if(draw?.DeclaringType==rendererType&&draw?.ReturnType==typeof(void)&&typeof(ModProjectile).IsAssignableFrom(rendererType))
                MonoModHooks.Add(draw,(Action<Action<ModProjectile>,ModProjectile>)DrawCrack);
        }
        holdoutItem=mod.Code.GetType(mod.Name+".Content.Items.Weapons.IHoldoutItem");
        var projectile=holdoutItem?.GetProperty("ProjectileType");
        if(projectile?.PropertyType==typeof(int)&&projectile.GetGetMethod()!=null){
            var item=Expression.Parameter(typeof(ModItem));
            holdoutType=Expression.Lambda<Func<ModItem,int>>(
                Expression.Call(Expression.Convert(item,holdoutItem),projectile.GetGetMethod()),item).Compile();
            CompanionCrossMod.MaintainHeldWeapon+=Maintain;
        }
        input=mod.GetContent<ModPlayer>().FirstOrDefault(p=>p.GetType().FullName==mod.Name+".Common.EModPlayer");
        var type=input?.GetType();var mouse=type?.GetProperty("MouseWorld");
        if(mouse?.PropertyType!=typeof(Vector2)||mouse.GetSetMethod()==null)return;
        var state=Expression.Parameter(typeof(ModPlayer));var aim=Expression.Parameter(typeof(Vector2));
        setAim=Expression.Lambda<Action<ModPlayer,Vector2>>(
            Expression.Call(Expression.Convert(state,type),mouse.GetSetMethod(),aim),state,aim).Compile();
        CompanionCrossMod.PrepareActor+=Prepare;
    }
    void PreparePhantom(Mod mod){
        if(!mod.TryFind<ModBuff>("WyrmPhantom",out var buff)||!mod.TryFind<ModProjectile>("PhantomWyrm",out var shot)||
            !mod.TryFind<ModItem>("Ystralyn",out var item))return;
        var damage=item.GetType().GetField("PhantomDamage",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
        if(damage?.FieldType!=typeof(int))return;
        phantomBuff=buff.Type;phantomType=shot.Type;CallOfVoidPhantomBuff.BuffType=buff.Type;
        phantomDamage=Expression.Lambda<Func<int>>(Expression.Field(null,damage)).Compile();
        CompanionCrossMod.UpdateResources+=UpdatePhantom;
    }
    // The whip grants its native buff on hit, but EModPlayer.PostUpdate owns
    // the spawn. Reuse that small contract without running unrelated player
    // updates. The native phantom retains damage, targeting and expiration.
    void UpdatePhantom(CompanionNativeUse use){
        var actor=use.Actor;
        if(CompanionNetwork.Client||!use.Available||!actor.HasBuff(phantomBuff)||actor.ownedProjectileCounts[phantomType]>0)return;
        using(new CompanionNativeUse.Scope(use))
            Projectile.NewProjectile(actor.GetSource_FromThis(),actor.Center,
                new Vector2(Main.rand.NextFloat(-14,14),Main.rand.NextFloat(-14,14)),phantomType,
                (int)actor.GetTotalDamage(DamageClass.Summon).ApplyTo(phantomDamage()),1,actor.whoAmI);
    }
    // The abyss screen pass calls this method directly, outside DrawProj.
    // Resolve the tagged projectile exactly as the ordinary drawing path does.
    static void DrawCrack(Action<ModProjectile> orig,ModProjectile renderer){
        if(!CompanionNativeUse.DrawContext(renderer.Projectile,out var use))return;
        if(use==null){orig(renderer);return;}
        using(new CompanionNativeUse.ReplicaDrawScope(use))
        using(new CompanionNativeUse.Scope(use,trackHealing:false))orig(renderer);
    }
    // Call of Void caches aim relative to the player's mounted center. Supply
    // the same world-space input for the isolated actor on every peer, letting
    // its own setter and weapon AI handle offsets, turning and projectiles.
    void Prepare(Player actor,Vector2 aim)=>setAim(actor.GetModPlayer(input),aim);
    // IHoldoutItem is created by EModPlayer.PostUpdate, not Shoot. Reproduce
    // only that interface contract inside our authoritative native-use scope;
    // running the whole player update would also execute unrelated equipment,
    // world and UI effects. The mod's own projectile still owns all attacks.
    void Maintain(CompanionNativeUse use){
        var actor=use.Actor;var item=actor.HeldItem;
        if(item.ModItem==null||!holdoutItem.IsInstanceOfType(item.ModItem))return;
        int type=holdoutType(item.ModItem);
        if(type<=0||type>=actor.ownedProjectileCounts.Length||actor.ownedProjectileCounts[type]>0)return;
        var velocity=(use.Aim-actor.Center).SafeNormalize(new Vector2(actor.direction,0))*item.shootSpeed;
        Projectile.NewProjectile(actor.GetSource_ItemUse(item),actor.MountedCenter,velocity,type,item.damage,actor.GetWeaponKnockback(item),actor.whoAmI);
    }
    public override void Unload(){
        CompanionCrossMod.PrepareActor-=Prepare;input=null;setAim=null;
        CompanionCrossMod.MaintainHeldWeapon-=Maintain;holdoutItem=null;holdoutType=null;
        CompanionCrossMod.UpdateResources-=UpdatePhantom;phantomDamage=null;phantomBuff=phantomType=CallOfVoidPhantomBuff.BuffType=0;
    }
}

// The native phantom checks a player buff on every peer. Carry only its own
// granting buff through the projectile's existing extra-AI packet; a replica
// must not depend on predicting the server's whip hits to keep it visible.
public sealed class CallOfVoidPhantomBuff:GlobalProjectile {
    internal static int BuffType;
    public override bool InstancePerEntity=>true;
    public override bool AppliesToEntity(Projectile entity,bool lateInstantiation)=>
        entity.ModProjectile?.FullName is "CalamityEntropy/PhantomWyrm" or "CalamityEntropyLegacy/PhantomWyrm";
    ulong sentExpiry,receivedAt;
    int duration;
    bool received;
    static int Remaining(CompanionNativeUse use){
        int slot=use.Actor.FindBuffIndex(BuffType);
        return slot>=0?Math.Max(0,use.Actor.buffTime[slot]):0;
    }
    public override void SendExtraAI(Projectile p,Terraria.ModLoader.IO.BitWriter bits,System.IO.BinaryWriter writer){
        var use=CompanionOwnedShot.Of(p);bits.WriteBit(use!=null);
        if(use==null)return;
        int time=Remaining(use);writer.Write(time);
        sentExpiry=time>0?Main.GameUpdateCount+(ulong)time:0;
    }
    public override void ReceiveExtraAI(Projectile p,Terraria.ModLoader.IO.BitReader bits,System.IO.BinaryReader reader){
        bool tagged=bits.ReadBit();int time=tagged?reader.ReadInt32():0;
        if(!CompanionNetwork.Client)return;
        received=tagged;duration=Math.Max(0,time);receivedAt=Main.GameUpdateCount;
    }
    public override bool PreAI(Projectile p){
        var use=CompanionOwnedShot.Of(p);
        if(use==null||BuffType<=0)return true;
        if(CompanionNetwork.Server){
            int time=Remaining(use);ulong expiry=time>0?Main.GameUpdateCount+(ulong)time:0;
            if(expiry!=sentExpiry)p.netUpdate=true;
        }else if(CompanionNetwork.Client&&received){
            int time=(int)Math.Max(0,(long)duration-(long)(Main.GameUpdateCount-receivedAt));
            var actor=use.Actor;int slot=actor.FindBuffIndex(BuffType);
            if(time==0){if(slot>=0)actor.DelBuff(slot);}
            else if(slot>=0)actor.buffTime[slot]=time;
            else actor.AddBuff(BuffType,time,quiet:true);
        }
        return true;
    }
}
