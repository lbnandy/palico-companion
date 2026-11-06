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
    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("CalamityEntropy",out var mod))return;
        holdoutItem=mod.Code.GetType("CalamityEntropy.Content.Items.Weapons.IHoldoutItem");
        var projectile=holdoutItem?.GetProperty("ProjectileType");
        if(projectile?.PropertyType==typeof(int)&&projectile.GetGetMethod()!=null){
            var item=Expression.Parameter(typeof(ModItem));
            holdoutType=Expression.Lambda<Func<ModItem,int>>(
                Expression.Call(Expression.Convert(item,holdoutItem),projectile.GetGetMethod()),item).Compile();
            CompanionCrossMod.MaintainHeldWeapon+=Maintain;
        }
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
    }
}
