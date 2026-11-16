using System;
using System.Linq;
using System.Collections.Generic;
using MagicStorage.Components;
using System.Reflection;
using System.Threading;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.Utils;
using Terraria;
using Terraria.ModLoader;
using MagicStorage;
using MagicStorage.Common.Systems.RecurrentRecipes;

namespace PalicoCompanion.Systems;

[ExtendsFromMod("MagicStorage")]
[JITWhenModsEnabled("MagicStorage")]
internal sealed class MagicStorageCraftingPlayer : ModSystem {
    static Player human;
    static Player Human()=>Volatile.Read(ref human)??Main.LocalPlayer;
    static StoragePlayer Storage()=>Human().GetModPlayer<StoragePlayer>();
    static TEStorageHeart Heart()=>Storage().GetStorageHeart();
    static TECraftingAccess Crafting()=>Storage().GetCraftingAccess();
    static List<Item> Stations()=>Crafting()?.stations??new();

    public override void Load() {
        On_Main.Update+=Update;
        Patch(typeof(CraftingGUI));
        Patch(typeof(OrderedRecipeTree));
    }

    // The crafting UI and its background workers belong to the human. Capture
    // that reference before native companion scopes borrow Main.myPlayer.
    static void Update(On_Main.orig_Update orig,Main self,GameTime time) {
        Volatile.Write(ref human,Main.player[Main.myPlayer]);
        orig(self,time);
    }

    static bool PlayerRead(Mono.Cecil.Cil.Instruction i)=>
        i.MatchCall<Main>("get_LocalPlayer")||i.MatchCall<StoragePlayer>("get_LocalPlayer")||
        i.MatchCall(typeof(CraftingGUI),"GetHeart")||i.MatchCall(typeof(CraftingGUI),"GetCraftingEntity")||
        i.MatchCall(typeof(CraftingGUI),"GetCraftingStations");

    static void Patch(Type type) {
        const BindingFlags flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
        foreach(var method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags))) {
            if(method.GetMethodBody()==null||method.ContainsGenericParameters)continue;
            using var body=new DynamicMethodDefinition(method);
            if(body.Definition.Body.Instructions.Any(PlayerRead))MonoModHooks.Modify(method,ReadPlayer);
        }
        foreach(var nested in type.GetNestedTypes(flags))Patch(nested);
    }

    // Replace reads at their call sites: the tiny native getters are already
    // inlined during tML's pre-JIT, so detouring only their getters is too late.
    // Recipe availability, recursion, consumption and storage mutations remain
    // Magic Storage's own implementation.
    static void ReadPlayer(ILContext il) {
        var c=new ILCursor(il);
        while(c.TryGotoNext(MoveType.Before,PlayerRead)) {
            var instruction=c.Next;
            string name=instruction.MatchCall<Main>("get_LocalPlayer")?nameof(Human):
                instruction.MatchCall<StoragePlayer>("get_LocalPlayer")?nameof(Storage):
                instruction.MatchCall(typeof(CraftingGUI),"GetHeart")?nameof(Heart):
                instruction.MatchCall(typeof(CraftingGUI),"GetCraftingEntity")?nameof(Crafting):nameof(Stations);
            instruction.Operand=il.Import(typeof(MagicStorageCraftingPlayer).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic));
            c.Index++;
        }
    }

    public override void Unload(){On_Main.Update-=Update;Volatile.Write(ref human,null);}
}
