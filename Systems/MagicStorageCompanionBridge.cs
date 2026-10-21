using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.ModLoader;
using MagicStorage;
using MagicStorage.Components;
using MagicStorage.Common.Systems;
using MagicStorage.Common.Players;
using MagicStorage.UI.States;
namespace PalicoCompanion.Systems;

[ExtendsFromMod("MagicStorage")]
[JITWhenModsEnabled("MagicStorage")]
public sealed class MagicStorageCompanionBridge:ModSystem,ICompanionStorageProvider {
    // 0.7.0 exposes the permission check but not the component's network id.
    // Read that one id; never reflect into storage contents or mutation code.
    // Fail closed if a future release changes it.
    static readonly System.Reflection.FieldInfo network=typeof(TEStorageComponent).GetField("assignedNetwork",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
    internal static bool Allowed(Player player,TEStorageHeart heart){
        if(network?.GetValue(heart) is not int id)return false;
        if(!CompanionNetwork.Server)return SecuritySystem.CanPlayerAccessImmediately(player,id);
        if(id<0)return true;
        // Mirrors SecuritySystem.CheckNetworkAccess on the server. The client
        // helper also requires a UI-side joined-network cache, even for public
        // storage; a remote non-operator need not have that cache on the server.
        var view=SecuritySystem.GetNetwork(id);
        return view.Valid&&(!view.restricted||view.creatorID==player.GetModPlayer<SecurityPlayer>().UniqueID||
            SecuritySystem.CanPlayerAccessImmediately(player,id));
    }
    readonly ref struct AccessScope {
        readonly SecurityPlayer security;readonly int id;readonly bool temporary;
        readonly SecuritySystem.AccessContext context;
        internal AccessScope(Player player,TEStorageHeart heart){
            id=(int)network.GetValue(heart);security=player.GetModPlayer<SecurityPlayer>();
            // Valid() already applies the native server authorization policy.
            // Bridge only this authorized network into the native API's cache
            // for the synchronous transfer, then restore it even on exceptions.
            temporary=CompanionNetwork.Server&&id>=0&&!security.HasJoinedNetwork(id)&&!security.KnowsPassword(id);
            if(temporary)security.JoinNetwork(id);
            context=SecuritySystem.CreateAccessContext(player.whoAmI);
        }
        public void Dispose(){context.Dispose();if(temporary)security.RemoveNetworkAccess(id);}
    }
    public override void Load()=>CompanionCrossMod.Storage=this;
    public override void Unload(){if(ReferenceEquals(CompanionCrossMod.Storage,this))CompanionCrossMod.Storage=null;}
    static TEStorageComponent Access(Point point){
        if(!WorldGen.InWorld(point.X,point.Y,10)||!Main.tile[point].HasTile||TileLoader.GetTile(Main.tile[point].TileType) is not StorageAccess)return null;
        return TileEntity.ByPosition.TryGetValue(new Point16(point.X,point.Y),out var entity)?entity as TEStorageComponent:null;
    }
    public bool Permission(Player player,Point point,out Point permission){
        permission=default;var heart=Access(point)?.GetHeart();
        if(heart==null||!heart.IsAlive||!Allowed(player,heart))return false;
        permission=new(heart.Position.X,heart.Position.Y);return true;
    }
    public bool Open(out Point access,out Point permission,out Rectangle button){
        access=permission=default;button=default;
        if(!CompanionUIFlow.Visible||!Main.playerInventory||MagicUI.uiInterface?.CurrentState is not BaseStorageUI ui)return false;
        // Configure network access from storage only. Crafting uses the space
        // beside the main panel for recipe details; existing grants still apply.
        if(!MagicUI.IsStorageUIOpen())return false;
        var viewed=StoragePlayer.LocalPlayer.ViewingStorage();access=new(viewed.X,viewed.Y);
        if(!Permission(Main.LocalPlayer,access,out permission))return false;
        // Follow the native movable panel; reserve a side gutter, not its tabs,
        // item cells, close button, or the vanilla bestiary/emote controls.
        int x=(int)ui.PanelRight+6;
        if(x+34>Main.screenWidth/Main.UIScale)x=(int)ui.PanelLeft-40;
        if(x<0)return false;
        button=new(x,(int)ui.PanelTop+8,34,34);return true;
    }
    ICompanionStorageTarget ICompanionStorageProvider.Find(CompanionPlayer state,Projectile cat){
        var allowed=state.Chests.Points.ToHashSet();if(allowed.Count==0)return null;
        // Native component map includes remote access points connected to the
        // same heart. Reach the nearest actual access, not necessarily the core.
        var work=new Dictionary<TEStorageHeart,bool>();
        return TileEntity.ByPosition.Values.OfType<TEStorageComponent>()
            .Where(e=>e is not TEAbstractStorageUnit&&Vector2.DistanceSquared(state.WorkAnchor,e.Position.ToWorldCoordinates(16,16))<=320*320)
            .Select(e=>new Target(e)).Where(t=>allowed.Contains(t.Permission)&&t.Valid(state))
            .OrderBy(t=>Vector2.DistanceSquared(cat.Center,t.Center)).FirstOrDefault(t=>{
                if(!work.TryGetValue(t.Heart,out bool pending))work[t.Heart]=pending=t.HasWork(state);return pending;
            });
    }
    sealed class Target:ICompanionStorageTarget {
        readonly TEStorageComponent access;readonly TEStorageHeart heart;
        internal TEStorageHeart Heart=>heart;
        internal Point Permission=>heart==null?new(-1,-1):new(heart.Position.X,heart.Position.Y);
        public Vector2 Center=>access.Position.ToWorldCoordinates(16,16);
        internal Target(TEStorageComponent access){this.access=access;heart=access.GetHeart();}
        public bool Valid(CompanionPlayer state)=>heart is {IsAlive:true}&&state.Chests.Allows(Permission.X,Permission.Y)&&
            ReferenceEquals(Access(new Point(access.Position.X,access.Position.Y)),access)&&ReferenceEquals(access.GetHeart(),heart)&&
            Allowed(state.Player,heart);
        bool Room(Item item)=>heart.GetStorageUnits().Any(u=>!u.Inactive&&(!u.IsFull||u.HasSpaceInStackFor(item)));
        static bool Storable(CompanionPlayer s,Item i)=>!i.IsAir&&!i.favorited&&!CompanionRestock.Keep(i,s);
        public bool HasWork(CompanionPlayer state){
            if(!Valid(state))return false;var stored=heart.GetStoredItems().ToArray();
            var types=stored.Select(i=>i.type).ToHashSet();var protectedTypes=stored.Where(i=>i.favorited).Select(i=>i.type).ToHashSet();
            return state.Bag.Any(i=>Storable(state,i)&&types.Contains(i.type)&&Room(i))||
                stored.Any(i=>!protectedTypes.Contains(i.type)&&CompanionRestock.Wanted(state,i,true)>0&&InventoryTransfer.HasRoom(i,CompanionRestock.Destination(state,i)));
        }
        public (int Stored,int Supplied) Transfer(CompanionPlayer state){
            if(CompanionNetwork.Client||!Valid(state)||state.Cat is not {} cat||!state.AutoLogistics||state.Paused||state.Vitals.Downed||
                Vector2.DistanceSquared(state.WorkAnchor,Center)>320*320||Vector2.DistanceSquared(cat.Center,Center)>100*100||!Reachability.Clear(cat.Center,Center))return default;
            int deposited=0,supplied=0;var changed=new HashSet<int>();
            // All mutations run synchronously on the authoritative game thread.
            // Native APIs maintain unit metadata/queues and preserve remainders.
            using var context=new AccessScope(state.Player,heart);
            var types=heart.GetStoredItems().Select(i=>i.type).ToHashSet();
            foreach(var item in state.Bag){
                if(!Storable(state,item)||!types.Contains(item.type))continue;
                int type=item.type,before=item.stack;heart.TryDeposit(item,state.Player);
                int moved=before-(item.IsAir?0:item.stack);if(moved>0){deposited+=moved;changed.Add(type);}
            }
            var supplies=heart.GetStoredItems().ToArray();
            var protectedTypes=supplies.Where(i=>i.favorited).Select(i=>i.type).ToHashSet();
            foreach(var sample in supplies.Where(i=>!protectedTypes.Contains(i.type)&&CompanionRestock.Wanted(state,i)>0).Select(i=>i.Clone()).OrderByDescending(i=>state.Bag.Any(b=>b.type==i.type&&!b.IsAir)).ToArray()){
                // Native keepOneIfFavorite preserves one, not the whole stack.
                // Never automatically withdraw a type the owner has favorited.
                if(protectedTypes.Contains(sample.type))continue;int wanted=CompanionRestock.Wanted(state,sample);if(wanted<=0)continue;
                var destination=CompanionRestock.Destination(state,sample);
                int room=(int)Math.Min(int.MaxValue,destination.Sum(i=>i.IsAir?(long)sample.maxStack:i.type==sample.type&&ItemLoader.CanStack(i,sample)?Math.Max(0,i.maxStack-i.stack):0));
                // Capacity queries must not execute OnStack hooks on dummy items.
                int capacity=Math.Min(room,Math.Min(wanted,Math.Min(sample.stack,sample.maxStack)));if(capacity<=0)continue;
                var request=sample.Clone();request.stack=capacity;
                var taken=heart.TryWithdraw(request,true,state.Player);if(taken.IsAir)continue;
                PickupCredit.Clear(taken);int type=taken.type;int moved=InventoryTransfer.Move(taken,destination);
                if(!taken.IsAir){heart.TryDeposit(taken,state.Player);if(!taken.IsAir)state.Player.QuickSpawnItem(state.Player.GetSource_Misc("PalicoStorageRemainder"),taken,taken.stack);}
                supplied+=moved;changed.Add(type);
            }
            if(changed.Count>0){
                if(CompanionNetwork.Server){NetHelper.SendRefreshNetworkItems(heart.Position,false,changed);CompanionNetwork.Send(state);}
                else if(StoragePlayer.IsClientViewingHeart(heart)){MagicUI.SetNextCollectionsToRefresh(changed);MagicUI.SetRefresh();}
            }
            return(deposited,supplied);
        }
    }
}

[ExtendsFromMod("MagicStorage")]
[JITWhenModsEnabled("MagicStorage")]
public sealed class MagicStoragePermissionLifetime:GlobalTile {
    public override void KillTile(int i,int j,int type,ref bool fail,ref bool effectOnly,ref bool noItem){
        if(fail||effectOnly||TileLoader.GetTile(type) is not StorageHeart)return;
        var tile=Main.tile[i,j];int x=i-tile.TileFrameX%36/18,y=j-tile.TileFrameY%36/18;
        foreach(var p in Main.player)if(p.active){var s=p.GetModPlayer<CompanionPlayer>();if(!s.Chests.Allows(x,y))continue;s.Chests.Revoke(x,y);s.Storage.Cancel();if(CompanionNetwork.Server)CompanionNetwork.Send(s);}
    }
}
