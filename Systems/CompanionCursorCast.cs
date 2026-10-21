using System;
using MonoMod.Cil;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Native cursor-area weapons trace from a human-sized caster. CanHitLine's
// neighboring-cell clearance rejects the cat's low paw beside a flat floor.
// Keep the native placement loop and stopping point, using the same bounded
// hand ray as companion targeting. Real intervening tiles still stop the cast.
public sealed class CompanionCursorCast : ModSystem {
    public override void Load()=>IL_Player.GetFarthestSpawnPositionOnLine+=Trace;
    public override void Unload()=>IL_Player.GetFarthestSpawnPositionOnLine-=Trace;
    static void Trace(ILContext il){
        var c=new ILCursor(il);
        if(!c.TryGotoNext(MoveType.Before,i=>i.MatchCall(typeof(Collision),nameof(Collision.CanHitLine))))
            throw new InvalidOperationException("Native cursor cast obstruction test changed");
        c.Remove();
        c.EmitDelegate<Func<Vector2,int,int,Vector2,int,int,bool>>((from,width,height,to,targetWidth,targetHeight)=>
            CompanionNativeUse.Current!=null?Reachability.Clear(from,to,CompanionCombatReach.SearchRange+64):
                Collision.CanHitLine(from,width,height,to,targetWidth,targetHeight));
    }
}
