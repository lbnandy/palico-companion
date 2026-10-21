using Terraria;
using Terraria.ModLoader;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System;
namespace PalicoCompanion.Systems;

// Native GetByUUID excludes server owner 255. Companions use that owner to
// retain server authority, but their native child projectiles still need UUID
// lookup. Extend that boundary only inside the matching companion's scope;
// keep player/world projectile lookup and native chain AI unchanged.
public sealed class CompanionProjectileLinks:ModSystem {
    public override void Load(){On_Projectile.GetByUUID_int_int+=Find;IL_Projectile.ProjectileFixDesperation+=AllowServerActor;On_Projectile.ProjectileFixDesperation+=Repair;}
    public override void Unload(){On_Projectile.GetByUUID_int_int-=Find;IL_Projectile.ProjectileFixDesperation-=AllowServerActor;On_Projectile.ProjectileFixDesperation-=Repair;}
    static void AllowServerActor(ILContext il){
        var c=new ILCursor(il);
        if(!c.TryGotoNext(MoveType.Before,i=>i.MatchLdfld<Projectile>(nameof(Projectile.owner)),i=>i.MatchLdcI4(255)))throw new InvalidOperationException("Native projectile link owner guard changed");
        c.Index++;
        // Only the early-out's operand changes. Actual owner comparisons and
        // all native projectile-type/link-field decisions remain untouched.
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate<Func<int,Projectile,int>>((owner,p)=>owner==255&&CompanionNativeUse.Current!=null&&CompanionOwnedShot.Of(p)==CompanionNativeUse.Current?0:owner);
    }
    static void Repair(On_Projectile.orig_ProjectileFixDesperation orig,Projectile p){
        var context=CompanionOwnedShot.Of(p);
        if(p.owner==255&&context!=null){using(new CompanionNativeUse.Scope(context))orig(p);p.GetGlobalProjectile<CompanionOwnedShot>().PendingNativeLinkRepair=false;}
        else orig(p);
    }
    static int Find(On_Projectile.orig_GetByUUID_int_int orig,int owner,int uuid){
        var context=CompanionNativeUse.Current;
        if(owner!=255||context==null||context.Actor.whoAmI!=owner)return orig(owner,uuid);
        if(uuid<0||uuid>=Main.maxProjectiles)return -1;
        int index=Main.projectileIdentity[owner,uuid];
        if(index>=0&&index<Main.maxProjectiles&&Matches(Main.projectile[index],context,owner,uuid))return index;
        // Receivers can allocate a different array slot than the server. Resolve
        // by the synchronized UUID, never ai's numeric value as a local index.
        foreach(var p in Main.projectile)if(Matches(p,context,owner,uuid))return p.whoAmI;
        return -1;
    }
    static bool Matches(Projectile p,CompanionNativeUse context,int owner,int uuid)=>p.active&&p.owner==owner&&p.projUUID==uuid&&CompanionOwnedShot.Of(p)==context;
}
