using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Gamepad;

namespace PalicoCompanion.Systems;

// Native navigation moves the UI cursor; the existing mouse/packet handlers
// remain the only implementation of inventory and companion actions.
public sealed class CompanionGamepad:ModSystem {
    static UILinkPage page;
    static readonly Dictionary<string,int> ids=new();
    static readonly Dictionary<int,Vector2> positions=new();
    static readonly HashSet<int> sliders=new();
    static readonly Dictionary<int,Func<string>> instructions=new();
    static string collecting,lastSurface;
    static float scale=1;
    static int mappedSlot=-1,inventoryRight;
    static bool linked;
    Hook slotLookup;
    delegate int SlotOriginal(Item[] items,int context,int slot);
    static bool Appearance=>CompanionAppearance.Current!=null&&Main.InGameUI.CurrentState==CompanionAppearance.Current;
    static bool Visible=>!Main.gameMenu&&(Appearance||Main.playerInventory&&CompanionUIFlow.Visible&&
        (Main.LocalPlayer.GetModPlayer<CompanionPlayer>().PanelOpen||TrainingPanel.Open));
    public override void Load(){
        if(Main.dedServ)return;
        slotLookup=new Hook(typeof(ItemSlot).GetMethod("GetGamepadPointForSlot",BindingFlags.Static|BindingFlags.NonPublic)!,SlotPoint);
    }
    public override void Unload(){
        slotLookup?.Dispose();slotLookup=null;RestoreInventory();
        if(page!=null){
            if(UILinkPointNavigator.CurrentPage==page.ID||UILinkPointNavigator.OldPage==page.ID){
                UILinkPointNavigator.GoToDefaultPage();UILinkPointNavigator.ProcessChanges();
            }
            foreach(int id in ids.Values)UILinkPointNavigator.Points.Remove(id);
            UILinkPointNavigator.Pages.Remove(page.ID);
        }
        page=null;ids.Clear();positions.Clear();sliders.Clear();instructions.Clear();collecting=lastSurface=null;mappedSlot=-1;
    }
    public override void UpdateUI(GameTime gameTime){
        if(!Visible){RestoreInventory();lastSurface=null;}
    }
    public override void OnWorldUnload(){RestoreInventory();instructions.Clear();positions.Clear();lastSurface=null;}
    static int SlotPoint(SlotOriginal original,Item[] items,int context,int slot)=>mappedSlot>=0?mappedSlot:original(items,context,slot);
    internal readonly struct SlotScope:IDisposable {
        readonly int previous;
        internal SlotScope(int id){previous=mappedSlot;mappedSlot=id;}
        public void Dispose()=>mappedSlot=previous;
    }
    internal static IDisposable Slot(CompanionPlayer state,Item[] items,int slot,Rectangle bounds){
        byte group=CompanionInventoryOperations.Group(state,items);
        int id=Add($"slot-{group}-{slot}",bounds,true);
        instructions[id]=()=>SlotInstructions(state,items,slot,group);
        return new SlotScope(id);
    }
    static string SlotInstructions(CompanionPlayer state,Item[] items,int slot,byte group){
        string text=Command(55,"MouseRight");
        if(items[slot].IsAir||!Main.mouseItem.IsAir)return text;
        bool ready=!CompanionNetwork.Pending&&(!CompanionNetwork.Client||state.NetReady)&&CompanionNetwork.CanMoveInventory(state.Player);
        bool canFavorite=CompanionInventoryOperations.CanFavorite(group,slot);
        if(canFavorite)text+=CompanionInventoryOperations.SharedSlot(group,slot)
            ?PlayerInput.BuildCommand(CompanionText.Get("CompanionLoadouts."+(items[slot].favorited?"Unshare":"Share")),false,PlayerInput.ProfileGamepadUI.KeyStatus["SmartCursor"])
            :Command(83,"SmartCursor");
        if(canFavorite&&PlayerInput.AllowExecutionOfGamepadInstructions&&ready&&PlayerInput.Triggers.JustPressed.SmartCursor){
            if(CompanionNetwork.Client)CompanionNetwork.Click(state,items,slot,false,true);
            else CompanionInventoryOperations.Click(items,slot,ref Main.mouseItem,false,true,group).Present(state);
            PlayerInput.LockGamepadButtons("SmartCursor");
        }
        if(!items[slot].favorited){
            text+=PlayerInput.BuildCommand(Terraria.Localization.Language.GetTextValue("Mods.PalicoCompanion.UI.Take"),false,PlayerInput.ProfileGamepadUI.KeyStatus["SmartSelect"]);
            if(PlayerInput.AllowExecutionOfGamepadInstructions&&ready&&PlayerInput.Triggers.JustPressed.SmartSelect){
                if(CompanionNetwork.Client)CompanionNetwork.Transfer(state,false,slot,group);
                else CompanionInventoryOperations.Take(state,items,slot).Present(state,true);
                PlayerInput.LockGamepadButtons("SmartSelect");
            }
        }
        return text;
    }
    internal readonly struct Surface:IDisposable {
        public void Dispose(){Finish();collecting=null;scale=1;}
    }
    internal static Surface Begin(string key,float fit=1){collecting=key;scale=fit;positions.Clear();sliders.Clear();return new Surface();}
    static string Command(int label,string binding)=>PlayerInput.BuildCommand(Lang.misc[label].Value,false,PlayerInput.ProfileGamepadUI.KeyStatus[binding]);
    static void EnsurePage(){
        if(page!=null)return;
        int id=10000;while(UILinkPointNavigator.Pages.ContainsKey(id))id++;
        page=new UILinkPage(id){PageOnLeft=0,PageOnRight=0};
        page.IsValidEvent+=()=>Visible;page.CanEnterEvent+=()=>Visible;
        page.OnSpecialInteracts+=()=>{
            if(Appearance&&PlayerInput.AllowExecutionOfGamepadInstructions&&PlayerInput.Triggers.JustPressed.Inventory){
                CompanionAppearance.Current.Close();PlayerInput.LockGamepadButtons("Inventory");
            }
            return Command(53,"MouseLeft")+Command(56,"Inventory")+(Appearance?"":
                PlayerInput.BuildCommand(Lang.misc[64].Value,true,PlayerInput.ProfileGamepadUI.KeyStatus["HotbarMinus"],PlayerInput.ProfileGamepadUI.KeyStatus["HotbarPlus"]));
        };
        page.UpdateEvent+=()=>PlayerInput.GamepadAllowScrolling=TrainingPanel.Open;
        UILinkPointNavigator.RegisterPage(page,id,false);
    }
    // Keys identify controls, not their captions or enabled state, so cooldowns
    // and network acknowledgements cannot discard the current focus.
    internal static int Add(string key,Rectangle bounds,bool item=false){
        if(collecting==null)return -1;
        EnsurePage();key=collecting+"/"+key;
        if(!ids.TryGetValue(key,out int id)){
            id=90000;while(UILinkPointNavigator.Points.ContainsKey(id))id++;
            var point=new UILinkPoint(id,true,-3,-4,-1,-2);point.SetPage(page.ID);
            if(item){int pointId=id;point.OnSpecialInteracts+=()=>instructions.TryGetValue(pointId,out var hint)?hint():"";}
            page.LinkMap.Add(id,point);UILinkPointNavigator.Points.Add(id,point);ids.Add(key,id);
        }
        var center=bounds.Center.ToVector2()*scale;
        positions[id]=center;UILinkPointNavigator.SetPosition(id,center);return id;
    }
    internal static void Slider(string key,Rectangle bounds)=>sliders.Add(Add(key,bounds));
    static void Finish(){
        if(page==null||positions.Count==0)return;
        // Geometric neighbours follow actual localized/scaled layout, including
        // equipment slots that appear as progression unlocks them.
        int Neighbour(int from,Vector2 axis,int edge){
            var origin=positions[from];float best=float.MaxValue;int result=edge;
            foreach(var pair in positions){
                var delta=pair.Value-origin;float forward=Vector2.Dot(delta,axis);
                if(forward<=1)continue;
                float across=Math.Abs(delta.X*axis.Y-delta.Y*axis.X);
                float score=forward+across*4;
                if(score<best){best=score;result=pair.Key;}
            }
            return result;
        }
        foreach(int id in positions.Keys){var p=page.LinkMap[id];p.Left=sliders.Contains(id)?-3:Neighbour(id,-Vector2.UnitX,-3);p.Right=sliders.Contains(id)?-4:Neighbour(id,Vector2.UnitX,-4);p.Up=Neighbour(id,-Vector2.UnitY,-1);p.Down=Neighbour(id,Vector2.UnitY,-2);}
        page.DefaultPoint=positions.Keys.First();
        if(!positions.ContainsKey(page.CurrentPoint))page.CurrentPoint=page.DefaultPoint;
        page.PageOnLeft=page.PageOnRight=Appearance?page.ID:0;
        if(!Appearance&&!linked&&UILinkPointNavigator.Pages.TryGetValue(0,out var inventory)){
            inventoryRight=inventory.PageOnRight;inventory.PageOnRight=page.ID;linked=true;
        }
        if(lastSurface!=collecting&&PlayerInput.UsingGamepadUI&&Visible)UILinkPointNavigator.ChangePoint(page.DefaultPoint);
        lastSurface=collecting;
    }
    static void RestoreInventory(){
        if(linked&&UILinkPointNavigator.Pages.TryGetValue(0,out var inventory)&&inventory.PageOnRight==page?.ID)inventory.PageOnRight=inventoryRight;
        linked=false;
    }
}
