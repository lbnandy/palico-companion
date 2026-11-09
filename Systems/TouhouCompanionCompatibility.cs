using System;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Native item execution temporarily makes the companion the local actor.
// Touhou's Kogasa hold callback uses that identity to access a client-only
// achievement even for ordinary items. Keep gameplay and human achievements
// intact, but do not let a companion enter those achievement branches.
public sealed class TouhouCompanionCompatibility:ModSystem {
    ILHook hold;
    public override void Load(){
        if(!ModLoader.TryGetMod("TouhouPetsEx",out var pets))return;
        var method=pets.Code.GetType("TouhouPetsEx.Enhance.Achieve.Kogasa")?.GetMethod("ItemHoldItem",new[]{typeof(Item),typeof(Player)});
        if(method==null)return;
        hold=new ILHook(method,il=>{
            if(!GuardAchievements(il))
                Mod.Logger.Warn($"TouhouPetsEx {pets.Version}: Kogasa achievement guards not recognized; hold callback left unchanged.");
        });
    }
    static bool GuardAchievements(ILContext il){
        var cursor=new ILCursor(il);
        var queries=new List<Instruction>();
        while(cursor.TryGotoNext(MoveType.After,
            i=>i.MatchLdarg(2),
            i=>i.MatchCall<Main>("get_LocalPlayer"),
            i=>i.OpCode==OpCodes.Ceq||i.OpCode==OpCodes.Bne_Un||i.OpCode==OpCodes.Bne_Un_S))
            queries.Add(cursor.Prev.Previous);
        // Validate before changing anything. Other LocalPlayer reads in this
        // method position reforging effects and must retain their native value.
        if(queries.Count!=2)return false;
        foreach(var query in queries){
            cursor.Goto(query,MoveType.After);
            cursor.EmitDelegate<Func<Player,Player>>(AchievementPlayer);
        }
        return true;
    }
    static Player AchievementPlayer(Player player)=>Main.dedServ||CompanionAchievementScope.IsCompanion(player)?null:player;
    public override void Unload(){hold?.Dispose();hold=null;}
}
