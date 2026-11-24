using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using OreExcavator;
using OreExcavator.Enumerations;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

[ExtendsFromMod("OreExcavator")]
[JITWhenModsEnabled("OreExcavator")]
public sealed class OreExcavatorCompanionBridge:ModSystem {
    delegate ushort? Excavate(ActionType action,ushort x,ushort y,byte delay,int limit,bool diagonals,
        byte owner,ushort tile,byte paint,int style,int replacement,sbyte replacementStyle,bool puppeting);
    Excavate excavate;
    Mod provider;
    public override void PostSetupContent(){
        provider=ModLoader.GetMod("OreExcavator");
        // The 0.8.9 ExcavateTile mod call is a stub. Bind the same queue used by its pickaxe hook.
        var signature=Array.ConvertAll(typeof(Excavate).GetMethod("Invoke").GetParameters(),p=>p.ParameterType);
        var method=provider.GetType().GetMethod("ModifySpooler",BindingFlags.Static|BindingFlags.NonPublic,null,signature,null);
        if(method?.ReturnType!=typeof(ushort?))return;
        excavate=method.CreateDelegate<Excavate>();
        CompanionCrossMod.ExcavateOre=Mine;
    }
    public override void Unload()=>CompanionCrossMod.ExcavateOre=null;

    void Mine(Player owner,Point cell,int type,int style){
        var server=ModContent.GetInstance<OreExcavatorConfig_Server>();
        if(!server.allowPickaxing||server.recursionLimit<=1||!WorldGen.InWorld(cell.X,cell.Y,10)
            ||Main.tile[cell.X,cell.Y].HasTile||!CompanionExclusions.CanChain(owner,type))return;
        if(CompanionNetwork.Server){
            // Ore Excavator initiates multiplayer excavations on the owning client,
            // whose whitelist, diagonal setting and recursion limit are authoritative there.
            CompanionNetwork.NotifyExcavatedOre(owner,cell,type,style);
            return;
        }
        if(provider.Call("IsWhitelistedTile",type,style) is not true)return;
        var client=ModContent.GetInstance<OreExcavatorConfig_Client>();
        int limit=Math.Min(client.recursionLimit,server.recursionLimit);
        if(limit>1)excavate(ActionType.TileKilled,(ushort)cell.X,(ushort)cell.Y,(byte)client.recursionDelay,
            limit,client.doDiagonals,(byte)owner.whoAmI,(ushort)type,0,style,-1,-1,false);
    }
}
