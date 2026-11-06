using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

public sealed class HomuraCompanionBridge:ModSystem {
    sealed class Burst {internal bool Recovering;internal ulong Tick=ulong.MaxValue;}
    ConditionalWeakTable<CompanionNativeUse,Burst> bursts=new();
    Type gun;FieldInfo recoil,timeBag;ModPlayer storage;ILHook inventory;
    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("Homura",out var mod))return;
        gun=mod.Code.GetType("Homura.Items.Weapons.枪械");recoil=gun?.GetField("当前偏移");
        storage=mod.GetContent<ModPlayer>().FirstOrDefault(p=>p.GetType().FullName=="Homura.UI.时空间ModPlayer");
        timeBag=storage?.GetType().GetField("TimeBag");
        var update=gun?.GetMethod("UpdateInventory");
        if(recoil?.FieldType!=typeof(float)||update==null)return;
        inventory=new ILHook(update,ProtectEquipment);
        CompanionCrossMod.UpdateResources+=Update;
        CompanionCrossMod.ResourceReady+=Ready;
    }
    bool Applies(Item item)=>item.ModItem!=null&&gun.IsInstanceOfType(item.ModItem);
    void Update(CompanionNativeUse use){
        if(!Applies(use.Actor.HeldItem))return;
        var clock=bursts.GetOrCreateValue(use);if(clock.Tick==Main.GameUpdateCount)return;clock.Tick=Main.GameUpdateCount;
        // Homura initializes its storage cells in the human's UI update. The
        // companion has no such UI or stored guns; initialize only empty cells
        // so its native ammo lookup can continue into the mapped inventory.
        if(timeBag?.GetValue(use.Actor.GetModPlayer(storage)) is Item[] bag)
            for(int i=0;i<bag.Length;i++)bag[i]??=new Item();
        // Reload timing, ammo-box lookup/consumption and recoil decay remain
        // owned by the item, using only the companion's mapped inventory.
        using(new CompanionNativeUse.Scope(use))use.Actor.HeldItem.ModItem.UpdateInventory(use.Actor);
    }
    bool Ready(CompanionNativeUse use){
        if(!Applies(use.Actor.HeldItem))return true;
        float kick=(float)recoil.GetValue(use.Actor.HeldItem.ModItem);
        var burst=bursts.GetOrCreateValue(use);
        // Short controlled bursts let native recoil settle. Do not zero the
        // weapon's recoil or remove its random spread from emitted shots.
        if(kick>=.15f)burst.Recovering=true;
        if(kick<=.02f)burst.Recovering=false;
        return !burst.Recovering;
    }
    static void ProtectEquipment(ILContext il){
        var cursor=new ILCursor(il);
        // This same native updater also reads the human's discard hotkey and
        // auto-discard preference. Neither is an instruction to trash cat gear.
        while(cursor.TryGotoNext(i=>i.Operand is MethodReference m&&m.Name=="get_JustPressed")){
            cursor.Remove();
            cursor.EmitDelegate<Func<ModKeybind,bool>>(key=>CompanionNativeUse.Current==null&&key?.JustPressed==true);
        }
        cursor.Index=0;
        while(cursor.TryGotoNext(MoveType.After,i=>
            i.OpCode==OpCodes.Ldsfld&&i.Operand is FieldReference field&&field.Name=="自动丢弃枪械bool"||
            i.Operand is MethodReference method&&method.Name=="get_自动丢弃枪械bool")){
            cursor.EmitDelegate<Func<bool,bool>>(pressed=>CompanionNativeUse.Current==null&&pressed);
        }
    }
    public override void Unload(){inventory?.Dispose();inventory=null;CompanionCrossMod.UpdateResources-=Update;CompanionCrossMod.ResourceReady-=Ready;gun=null;recoil=timeBag=null;storage=null;bursts=new();}
}
