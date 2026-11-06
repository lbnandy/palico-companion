using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// MEAC's empty receiver leaves its two integers in the shared ExtraAI stream,
// shifting every later global projectile's data, including companion ownership.
public sealed class MeacProjectileCompatibility:ModSystem {
    ILHook receive;
    internal bool Applied { get; private set; }
    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("MEAC",out var mod))return;
        var type=mod.Code.GetType("MEAC.Projectiles.MEACProj");
        var send=type?.GetMethod(nameof(GlobalProjectile.SendExtraAI));
        var read=type?.GetMethod(nameof(GlobalProjectile.ReceiveExtraAI));
        if(send==null||read==null||!WritesExpectedFields(send))return;
        receive=new ILHook(read,CompleteEmptyReceiver);
    }
    static bool WritesExpectedFields(MethodInfo method){
        using var body=new DynamicMethodDefinition(method);
        var code=body.Definition.Body.Instructions.Where(i=>i.OpCode!=OpCodes.Nop).ToArray();
        return code.Length==9&&Writes(code,0,nameof(Projectile.timeLeft))&&
            Writes(code,4,nameof(Projectile.spriteDirection))&&code[8].MatchRet();
    }
    static bool Writes(Instruction[] code,int index,string field)=>
        code[index].MatchLdarg(3)&&code[index+1].MatchLdarg(1)&&
        code[index+2].MatchLdfld<Projectile>(field)&&
        code[index+3].MatchCallvirt<BinaryWriter>(nameof(BinaryWriter.Write))&&
        code[index+3].Operand is MethodReference write&&write.Parameters.Count==1&&
        write.Parameters[0].ParameterType.MetadataType==MetadataType.Int32;
    void CompleteEmptyReceiver(ILContext il){
        Applied=false;
        // Inspect the composed receiver too: an upstream implementation or an
        // earlier IL fix must keep its own reads instead of consuming twice.
        var code=il.Body.Instructions.Where(i=>i.OpCode!=OpCodes.Nop).ToArray();
        if(code.Length!=1||!code[0].MatchRet())return;
        var cursor=new ILCursor(il);
        cursor.Emit(OpCodes.Ldarg_1);
        cursor.Emit(OpCodes.Ldarg_3);
        cursor.EmitDelegate<Action<Projectile,BinaryReader>>(ReadFields);
        Applied=true;
    }
    static void ReadFields(Projectile projectile,BinaryReader reader){
        projectile.timeLeft=reader.ReadInt32();
        projectile.spriteDirection=reader.ReadInt32();
    }
    public override void Unload(){receive?.Dispose();receive=null;Applied=false;}
}
