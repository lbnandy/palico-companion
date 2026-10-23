using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Change only the native wait threshold. Keep bobber physics, fishing power,
// catch rolls, bait consumption and other players' lines on their native path.
public sealed class CompanionFishingSpeed : ModSystem {
    public override void Load()=>IL_Projectile.AI_061_FishingBobber+=AdjustWait;
    public override void Unload()=>IL_Projectile.AI_061_FishingBobber-=AdjustWait;
    static void AdjustWait(ILContext il){
        var c=new ILCursor(il);
        if(!c.TryGotoNext(MoveType.After,i=>i.MatchLdfld<Projectile>(nameof(Projectile.localAI)),
            i=>i.MatchLdcI4(1),i=>i.OpCode==OpCodes.Ldelem_R4,i=>i.MatchLdcR4(660f)))
            throw new InvalidOperationException("Native fishing wait threshold changed");
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate<Func<float,Projectile,float>>(Threshold);
    }
    internal static float Threshold(float original,Projectile bobber)=>
        !CompanionNetwork.Client&&CompanionFishingBobber.Of(bobber)!=null
            ?original/CompanionBalanceConfig.Current.FishingScale:original;
}
