using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Observe the actual Damage pass after vanilla/GlobalProjectile CanDamage and
// Damage_GetHitbox. This keeps telegraphs, special hitboxes and Colliding hooks
// engine-owned, including dedicated-server simulation. Never run a human's
// Hurt or equipment hooks against the owner's Player to injure their companion.
public sealed class CompanionIncomingDamage : ModSystem {
    static readonly List<CompanionCat> cats=new();
    internal static void Register(CompanionCat cat){if(!CompanionNetwork.Client&&!cats.Contains(cat))cats.Add(cat);}
    internal static void Remove(CompanionCat cat)=>cats.Remove(cat);
    public override void OnWorldLoad()=>cats.Clear();
    public override void OnWorldUnload()=>cats.Clear();
    public override void Load()=>IL_Projectile.Damage+=Patch;
    public override void Unload()=>IL_Projectile.Damage-=Patch;
    static void Patch(ILContext il){
        var c=new ILCursor(il);
        if(!c.TryGotoNext(MoveType.After,i=>i.MatchCall<Projectile>("Damage_GetHitbox")))
            throw new InvalidOperationException("PalicoCompanion: native projectile damage hitbox boundary not found");
        c.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
        c.EmitDelegate<Func<Rectangle,Projectile,Rectangle>>((box,shot)=>{Hit(shot,box);return box;});
    }
    internal static bool BossNear(Player owner){
        foreach(var n in Main.ActiveNPCs)if((n.boss||n.type is NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail)&&n.type!=NPCID.MartianSaucerCore&&
            Math.Abs(n.Center.X-owner.Center.X)+Math.Abs(n.Center.Y-owner.Center.Y)<4000)return true;
        return false;
    }
    static void Hit(Projectile shot,Rectangle box){
        if(CompanionNetwork.Client||!shot.hostile||shot.damage<=0)return;
        // Scan actual companions, not all projectiles for every hostile shot.
        for(int i=cats.Count-1;i>=0;i--){
            var cat=cats[i];var p=cat.Projectile;
            if(!p.active||!ReferenceEquals(p.ModProjectile,cat)){cats.RemoveAt(i);continue;}
            var owner=Main.player[cat.OwnerIndex];var state=owner.GetModPlayer<CompanionPlayer>();
            if(!owner.active||owner.dead||!state.Enabled||state.Stowed||state.Paused||state.Vitals.Downed||state.Vitals.Immunity>0||CompanionNetwork.Server&&(!state.NetReady||state.NetLeaving))continue;
            if(!shot.Colliding(box,p.Hitbox))continue;
            // Stable vanilla's hostile projectile path doubles the stored
            // projectile damage, after randomization. The cat has no luck gear.
            int damage=(int)Math.Min(int.MaxValue,(long)Main.DamageVar(shot.damage)*2);
            cat.Feedback.Hurt(state,p,damage,BossNear(owner),shot.ArmorPenetration);
        }
    }
}
