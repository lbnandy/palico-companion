using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
namespace PalicoCompanion.Systems;

// 1.4.5 Cattiva calls Step_Pickaxe_MineSolids. Stable 1.4.4 already contains
// that selector but keeps its usage type private. Call the installed game code;
// do not ship a copied vanilla implementation or mutate the player's cursor.
internal static class VanillaMiningSelector
{
    const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Type usageType=typeof(SmartCursorHelper).GetNestedType("SmartCursorUsageInfo",Any);
    static readonly MethodInfo select=typeof(SmartCursorHelper).GetMethod("Step_Pickaxe_MineSolids",Any|BindingFlags.Static);
    static readonly Dictionary<string,FieldInfo> fields=new();
    static readonly MethodInfo damage=typeof(Player).GetMethod("GetPickaxeDamage",Any|BindingFlags.Instance);
    public static bool Available=>usageType!=null&&select?.GetParameters().Length==5&&damage!=null;
    static void Set(object info,string name,object value) {
        if(!fields.TryGetValue(name,out var f))fields[name]=f=usageType.GetField(name,Any|BindingFlags.Instance);
        f.SetValue(info,value);
    }
    public static bool CanDamage(Player p,Point at,Item tool) {
        // Frame-important objects are never passed: their special damage logic can
        // update hit-buffer coordinates. Ordinary solids are a read-only query.
        if(!Available)return false;
        if(Main.tile[at].TileType==Terraria.ID.TileID.ExposedGems)return tool.pick>0&&WorldGen.CanKillTile(at.X,at.Y);
        if(!DiggingIntent.Allowed(Main.tile[at].TileType))return false;
        return (int)damage.Invoke(p,new object[]{at.X,at.Y,tool.pick,0,Main.tile[at]})>0&&WorldGen.CanKillTile(at.X,at.Y);
    }
    public static bool Select(Player snapshot,Player owner,Vector2 aim,Point origin,List<Tuple<int,int>> skip,out Point at) {
        at=default;if(!Available)return false;
        var info=Activator.CreateInstance(usageType,true);var tool=MiningTools.Best(owner);var range=MiningTools.Range(owner,tool);
        Set(info,"player",snapshot);Set(info,"item",tool);Set(info,"position",snapshot.position);Set(info,"Center",snapshot.Center);
        Set(info,"mouse",aim);Set(info,"screenTargetX",origin.X);Set(info,"screenTargetY",origin.Y);
        var center=owner.Center.ToTileCoordinates();
        Set(info,"reachableStartX",Math.Max(10,center.X-range.X));Set(info,"reachableEndX",Math.Min(Main.maxTilesX-10,center.X+range.X));
        Set(info,"reachableStartY",Math.Max(10,center.Y-range.Y));Set(info,"reachableEndY",Math.Min(Main.maxTilesY-10,center.Y+range.Y));
        object[] args={snapshot,info,skip,-1,-1};select.Invoke(null,args);
        int x=(int)args[3],y=(int)args[4];if(x<0||y<0)return false;at=new(x,y);return true;
    }
}
