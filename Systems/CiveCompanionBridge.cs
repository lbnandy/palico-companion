using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using CoolerItemVisualEffect.Common.MeleeModify;
using CoolerItemVisualEffect.Common.Config;
using CoolerItemVisualEffect.Common.WeaponGroup;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

[ExtendsFromMod("CoolerItemVisualEffect")]
[JITWhenModsEnabled("CoolerItemVisualEffect")]
public sealed class CiveCompanionBridge:ModSystem {
    sealed class State {internal bool Canvas;internal ulong VisualTick=ulong.MaxValue;internal readonly WeaponGroup Group=new();}
    static readonly ConditionalWeakTable<Player,State> actors=new();
    static readonly Dictionary<CompanionNativeUse,string> canvases=new();
    Hook canvasName,collision;
    delegate string CanvasOriginal(int player);
    public override void Load(){
        CompanionCrossMod.BeforeReset+=Prepare;
        CompanionCrossMod.UpdateEquipment+=Visuals;
        CompanionCrossMod.CiveDefaults=Receive;
        canvasName=new Hook(typeof(MeleeModifyPlayer).GetMethod(nameof(MeleeModifyPlayer.GetCanvasNameViaID))!,CanvasName);
        collision=CiveCompanionCollision.Hook();
    }
    public override void Unload(){
        CompanionCrossMod.BeforeReset-=Prepare;CompanionCrossMod.UpdateEquipment-=Visuals;CompanionCrossMod.CiveDefaults=null;
        Clear();canvasName?.Dispose();collision?.Dispose();canvasName=collision=null;
    }
    static void Clear(){foreach(var name in canvases.Values)LogSpiralLibrary.RenderCanvasSystem.RemoveCanvasFactory(name);canvases.Clear();actors.Clear();}
    public override void OnWorldUnload()=>Clear();
    public override void PostUpdateEverything(){
        if(Main.dedServ||Main.GameUpdateCount%60!=0)return;
        foreach(var pair in canvases){
            var use=pair.Key;
            if(use.Cat is {active:true,ModProjectile:CompanionCat cat}&&ReferenceEquals(cat.Weapons,use))continue;
            LogSpiralLibrary.RenderCanvasSystem.RemoveCanvasFactory(pair.Value);canvases.Remove(use);
        }
    }
    static void Prepare(CompanionNativeUse use){
        var owner=use.State.Player.GetModPlayer<MeleeModifyPlayer>();
        var actor=use.Actor.GetModPlayer<MeleeModifyPlayer>();
        var state=actors.GetOrCreateValue(use.Actor);
        // Actors do not enter a world or load player-local files. Reuse the
        // owner's native grouping/configuration, evaluated against the CAT's item.
        actor.WeaponGroups.Clear();
        foreach(var source in owner.WeaponGroups){
            if(!source.CheckAvailabe(use.Actor.HeldItem))continue;
            // A local view keeps the owner's editable group untouched. Only the
            // selected native group is needed for this actor's current weapon.
            var group=state.Group;group.BasedOnDefaultCondition=source.BasedOnDefaultCondition;
            group.WhiteList=source.WhiteList;group.IsGroupActive=source.IsGroupActive;group.WeaponList=source.WeaponList;
            group.BindConfigName=source.BindConfigName;group.SwooshActionStyle=source.SwooshActionStyle;
            group.IsModifyActive=source.IsModifyActive&&Available(source.SwooshActionStyle);
            actor.WeaponGroups.Add(group);break;
        }
        actor.MeleeConfigs.Clear();foreach(var pair in owner.MeleeConfigs)actor.MeleeConfigs.Add(pair.Key,pair.Value);
        actor.CachedGrouping.Clear();actor.DefaultGroupConfig=owner.DefaultGroupConfig;
        var defaults=use.State.Player.GetModPlayer<CiveCompanionDefaults>();
        bool remote=Main.netMode!=NetmodeID.SinglePlayer&&use.State.Player.whoAmI!=Main.myPlayer;
        actor.IsModifyActiveDefaultGroup=remote&&defaults.Received?defaults.Active:owner.IsModifyActiveDefaultGroup;
        actor.SwooshActionStyleDefaultGroup=remote&&defaults.Received?defaults.Sequence:owner.SwooshActionStyleDefaultGroup;
        actor.IsModifyActiveDefaultGroup&=Available(actor.SwooshActionStyleDefaultGroup);
    }
    // Choose native use before CIVE suppresses vanilla damage/shooting. Pure
    // visual mode needs no sequence; overhaul must have a usable selection.
    static bool Available(CIVESequenceDefinition sequence)=>ServerConfig.Instance.meleeModifyLevel<ServerConfig.MeleeModifyLevel.PresetOnly||sequence?.GetSequence()!=null;
    static string CanvasName(CanvasOriginal original,int player){
        var use=CompanionNativeUse.Current;
        return use!=null&&use.Actor.whoAmI==player?"PalicoCompanion:CIVE:"+use.State.Player.whoAmI+":"+use.Cat.identity:original(player);
    }
    static void Visuals(CompanionNativeUse use){
        if(Main.dedServ)return;
        var actor=use.Actor.GetModPlayer<MeleeModifyPlayer>();var state=actors.GetOrCreateValue(use.Actor);
        if(state.VisualTick==Main.GameUpdateCount)return;state.VisualTick=Main.GameUpdateCount;
        if(!state.Canvas){actor.RegisterCurrentCanvas();canvases[use]=MeleeModifyPlayer.GetCanvasNameViaID(use.Actor.whoAmI);state.Canvas=true;}
        int last=MeleeModifyPlayer.LastWeaponType;
        try{MeleeModifyPlayerUtils.CheckItemChange(use.Actor);actor.RefreshConfigEffects();actor.PreUpdate();}
        finally{MeleeModifyPlayer.LastWeaponType=last;}
    }
    internal static void Send(CiveCompanionDefaults state,int to=-1,int ignore=-1){
        if(state.Player.whoAmI>=Main.maxPlayers)return;
        var packet=ModContent.GetInstance<PalicoCompanion>().GetPacket();packet.Write(CompanionCrossMod.CiveDefaultsPacket);
        packet.Write((byte)state.Player.whoAmI);packet.Write(state.Active);packet.Write(state.Sequence.Name);
        packet.Write((byte)state.Textures.Count);foreach(var pair in state.Textures){packet.Write(pair.Key);packet.Write((short)pair.Value.X);packet.Write((short)pair.Value.Y);}
        packet.Send(to,ignore);
    }
    static void Receive(BinaryReader reader,int sender){
        int owner=reader.ReadByte();bool active=reader.ReadBoolean();string name=reader.ReadString();
        int count=reader.ReadByte();bool valid=owner<Main.maxPlayers&&name.Length<=256&&count<=40&&(Main.netMode!=NetmodeID.Server||owner==sender);
        var textures=new Dictionary<int,Point>();
        for(int i=0;i<count;i++){int type=reader.ReadInt32(),width=reader.ReadInt16(),height=reader.ReadInt16();valid&=type>0&&type<ItemLoader.ItemCount&&width>0&&height>0&&width<=8192&&height<=8192;textures[type]=new(width,height);}
        if(!valid)return;
        var state=Main.player[owner].GetModPlayer<CiveCompanionDefaults>();
        state.Active=active;state.Sequence=new(name);state.Received=true;state.Textures.Clear();foreach(var pair in textures)state.Textures.Add(pair.Key,pair.Value);
        if(Main.netMode==NetmodeID.Server)Send(state,-1,sender);
    }
}

[ExtendsFromMod("CoolerItemVisualEffect")]
[JITWhenModsEnabled("CoolerItemVisualEffect")]
public sealed class CiveCompanionDefaults:ModPlayer {
    internal bool Received,Active;
    internal CIVESequenceDefinition Sequence=new("");
    internal readonly Dictionary<int,Point> Textures=new();
    readonly Dictionary<int,Point> sampledTextures=new();
    bool Capture(){
        var native=Player.GetModPlayer<MeleeModifyPlayer>();string name=native.SwooshActionStyleDefaultGroup?.Name??"";
        bool changed=!Received||Active!=native.IsModifyActiveDefaultGroup||Sequence.Name!=name;
        sampledTextures.Clear();
        foreach(var item in Player.GetModPlayer<CompanionPlayer>().Gear){
            if(item.IsAir||item.damage<=0)continue;
            Main.instance.LoadItem(item.type);var texture=Terraria.GameContent.TextureAssets.Item[item.type].Value;if(texture==null)continue;
            var size=new Point(texture.Width,texture.Height);sampledTextures[item.type]=size;
            if(!Textures.TryGetValue(item.type,out var previous)||previous!=size)changed=true;
        }
        changed|=sampledTextures.Count!=Textures.Count;if(!changed)return false;
        Textures.Clear();foreach(var pair in sampledTextures)Textures.Add(pair.Key,pair.Value);
        Active=native.IsModifyActiveDefaultGroup;Sequence=new(name);Received=true;return true;
    }
    public override void PostUpdate(){
        if(Main.netMode==NetmodeID.MultiplayerClient&&Player.whoAmI==Main.myPlayer&&Capture())CiveCompanionBridge.Send(this);
    }
    public override void SyncPlayer(int toWho,int fromWho,bool newPlayer){
        // CIVE already synchronizes custom groups and visual configs. Only its
        // otherwise local default selection needs this small companion message.
        if(Player.whoAmI==Main.myPlayer)Capture();
        if(Received)CiveCompanionBridge.Send(this,toWho,fromWho);
    }
}
