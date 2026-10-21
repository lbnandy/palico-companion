using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
namespace PalicoCompanion.Systems;

// One-time chest permission, opportunistic visits, bounded scan cadence and physical work.
public sealed class AutomaticLogistics
{
    int scan, elapsed, working, cooldown;
    int targetX,targetY;
    ICompanionStorageTarget external;
    readonly Dictionary<Point,ulong> deferred=new();
    public bool Active { get; private set; }
    public int Visits { get; private set; }
    public Vector2 Target => external?.Center??new Vector2(targetX*16+16,targetY*16+16);
    public void Cancel() {Active=false;external=null;elapsed=working=0;cooldown=180;}
    public bool Tick(Player player, CompanionPlayer state, Projectile cat, bool danger) {
        if(!state.AutoLogistics || state.Paused || !state.Enabled || state.Vitals.Downed || state.Order.Active || state.Order.HasPreview || state.Mining.Active || state.Garden.Active || state.Trees.Active) {Cancel();return false;}
        if(danger)return false;
        if(cooldown>0){cooldown--;return false;}
        if(!Active && ++scan<120)return false;
        if(!Active)scan=0;
        if(!Active){
            var candidate=CompanionCrossMod.Storage?.Find(state,cat);
            var normal=Choose(state,cat);
            if(candidate!=null&&(!deferred.TryGetValue(candidate.Center.ToTileCoordinates(),out var until)||until<=Main.GameUpdateCount)){
            if(normal==null||Vector2.DistanceSquared(cat.Center,candidate.Center)<Vector2.DistanceSquared(cat.Center,Logistics.Center(normal))){
                external=candidate;Active=true;elapsed=working=0;
            }
            }
        }
        if(external!=null)return TickExternal(state,cat);
        var chest=Active?Logistics.At(targetX,targetY):Choose(state,cat);
        if(chest==null || InUse(player,chest) || !state.Chests.Allows(chest.x,chest.y)||Vector2.DistanceSquared(state.WorkAnchor,Logistics.Center(chest))>320*320) {if(Active)Cancel();return false;}
        if(!Active) {
            if(!HasWork(state,chest))return false;
            targetX=chest.x;targetY=chest.y;Active=true;elapsed=working=0;
        }
        if(chest.x!=targetX||chest.y!=targetY){Cancel();return false;}
        if(++elapsed>360){deferred[new Point(targetX,targetY)]=Main.GameUpdateCount+900;state.Report(CompanionText.MessageFull("Mods.PalicoCompanion.UI.StorageBlocked"));Cancel();return false;}
        state.Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.StorageWalking");
        if(Vector2.DistanceSquared(cat.Center,Target)>72*72 || !Reachability.Clear(cat.Center,Target)) {working=0;return true;}
        state.Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.StorageWorking");
        (cat.ModProjectile as CompanionCat)?.FaceWork(Target);
        if(++working<45)return true;
        int stored=Logistics.Store(player,state,chest),supplied=Logistics.Supply(player,state,chest);
        if(stored+supplied>0){Visits++;state.Report(CompanionText.MessageFull("Mods.PalicoCompanion.UI.StorageDone",stored,supplied));cat.ai[1]=45;}
        Active=false;elapsed=working=0;cooldown=300;
        return true;
    }
    bool TickExternal(CompanionPlayer state,Projectile cat){
        if(!external.Valid(state)||Vector2.DistanceSquared(state.WorkAnchor,Target)>320*320){Cancel();return false;}
        if(++elapsed>360){deferred[Target.ToTileCoordinates()]=Main.GameUpdateCount+900;Cancel();return false;}
        state.Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.StorageWalking");
        if(Vector2.DistanceSquared(cat.Center,Target)>72*72||!Reachability.Clear(cat.Center,Target)){working=0;return true;}
        state.Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.StorageWorking");(cat.ModProjectile as CompanionCat)?.FaceWork(Target);
        if(++working<45)return true;
        var result=external.Transfer(state);
        if(result.Stored+result.Supplied>0){Visits++;state.Report(CompanionText.MessageFull("Mods.PalicoCompanion.UI.StorageDone",result.Stored,result.Supplied));cat.ai[1]=45;}
        external=null;Active=false;elapsed=working=0;cooldown=300;return true;
    }
    static bool InUse(Player player,Chest chest)=>Main.player.Any(p=>p.active&&p.chest>=0&&p.chest<Main.maxChests&&ReferenceEquals(Main.chest[p.chest],chest));
    internal Chest Choose(CompanionPlayer state,Projectile cat){
        foreach(var key in deferred.Where(e=>e.Value<=Main.GameUpdateCount).Select(e=>e.Key).ToArray())deferred.Remove(key);
        return state.Chests.Available()
            .Where(c=>!InUse(state.Player,c)&&Vector2.DistanceSquared(state.WorkAnchor,Logistics.Center(c))<=320*320&&!deferred.ContainsKey(new Point(c.x,c.y))&&HasWork(state,c))
            .OrderBy(c=>Vector2.DistanceSquared(cat.Center,Logistics.Center(c))).FirstOrDefault();
    }
    static bool HasWork(CompanionPlayer s,Chest chest) {
        foreach(var item in s.Bag)
            if(!item.IsAir&&!item.favorited&&!CompanionRestock.Keep(item,s)&&chest.item.Any(i=>!i.IsAir&&i.type==item.type)&&InventoryTransfer.HasRoom(item,chest.item))return true;
        return CompanionRestock.Needs(s,chest);
    }
}
