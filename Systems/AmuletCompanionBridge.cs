using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace PalicoCompanion.Systems;

public sealed class AmuletCompanionBridge:ModSystem {
    ILHook quizChildren;
    ModPlayer pathfinding;
    FieldInfo pathfinderMetas;
    MethodInfo setupPathfinders;
    public override void Load(){
        if(!Main.dedServ||!ModLoader.TryGetMod("AmuletOfManyMinions",out var amulet))return;
        const string ui="AmuletOfManyMinions.UI.CombatPetsQuizUI.CombatQuizClickSuppressor";
        var canUse=amulet.Code.GetType(ui)?.GetMethod(nameof(GlobalItem.CanUseItem),new[]{typeof(Item),typeof(Player)});
        if(canUse==null)return;
        quizChildren=new ILHook(canUse,il=>{
            var cursor=new ILCursor(il);
            if(!cursor.TryGotoNext(MoveType.After,
                i=>i.MatchLdsfld("AmuletOfManyMinions.UI.UserInterfaces","quizUI"),
                i=>i.MatchCallvirt<UIElement>("get_Children"))){
                Mod.Logger.Warn($"Amulet of Many Minions {amulet.Version}: no matching quiz UI query; CanUseItem left unchanged.");
                return;
            }
            // Its quiz UI is client-only. Treat an absent UI as having no hovered
            // children, preserving the rest of CanUseItem and every other hook.
            cursor.Index--;
            cursor.Remove();
            cursor.EmitDelegate<Func<UIElement,IEnumerable<UIElement>>>(ui=>ui?.Children??Enumerable.Empty<UIElement>());
        });
    }
    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("AmuletOfManyMinions",out var amulet))return;
        pathfinding=amulet.GetContent<ModPlayer>().FirstOrDefault(p=>p.Name=="MinionPathfindingPlayer");
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        pathfinderMetas=pathfinding?.GetType().GetField("pathfinderMetas",flags);
        setupPathfinders=pathfinding?.GetType().GetMethod("SetupPathfinderMetas",flags,null,Type.EmptyTypes,null);
        if(pathfinderMetas!=null&&setupPathfinders!=null)CompanionCrossMod.PrepareActor+=Prepare;
    }
    void Prepare(Player actor,Vector2 aim){
        // AoMM initializes this cache on world entry / human-player update,
        // then reads it for every minion hit, including vanilla summons.
        // Use its own initializer once; never replace an existing tactics state.
        var state=actor.GetModPlayer(pathfinding);
        if(pathfinderMetas.GetValue(state)==null)setupPathfinders.Invoke(state,null);
    }
    public override void Unload(){
        quizChildren?.Dispose();quizChildren=null;
        CompanionCrossMod.PrepareActor-=Prepare;
        pathfinding=null;pathfinderMetas=null;setupPathfinders=null;
    }
}
