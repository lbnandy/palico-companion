using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Low-priority local work; actual NPC.CheckCatchNPC owns the capture and drop.
public sealed class CompanionCatching {
    readonly Player actor=new();
    NPC target;int clock,age;
    public int SwingTicks {get;private set;}
    public int Duration {get;private set;}=30;
    public Item Tool {get;private set;}
    public Vector2 Target {get;private set;}
    public bool Active=>target!=null;
    static Item basic;
    public static Item Best(Player owner) {
        Item best=basic??=new Item(ItemID.BugNet);
        int Rank(Item i)=>i.type==ItemID.GoldenBugNet?3:ItemID.Sets.LavaproofCatchingTool[i.type]?2:1;
        for(int n=0;n<Math.Min(50,owner.inventory.Length);n++){
            var i=owner.inventory[n];if(i.IsAir||!ItemID.Sets.CatchingTool[i.type])continue;
            if(Rank(i)>Rank(best)||Rank(i)==Rank(best)&&i.useAnimation<best.useAnimation)best=i;
        }
        return best;
    }
    public static bool Candidate(NPC n,Item tool) => n.active&&n.catchItem>0&&!n.townNPC&&
        !n.SpawnedFromStatue&&n.releaseOwner==255&&n.type!=NPCID.BoundTownSlimeYellow&&
        n.type!=NPCID.FairyCritterPink&&n.type!=NPCID.FairyCritterGreen&&n.type!=NPCID.FairyCritterBlue&&
        (!ItemID.Sets.IsLavaBait[n.catchItem]||ItemID.Sets.LavaproofCatchingTool[tool.type]);
    public void Cancel(){target=null;SwingTicks=age=0;}
    public bool Tick(Player owner,CompanionPlayer s,Projectile cat,ref Vector2 goal) {
        clock++;
        if(s.Paused||s.Vitals.Downed||!(CompanionNetwork.Server?s.NetCatch:ModContent.GetInstance<CompanionConfig>().CatchCritters)){Cancel();return false;}
        Tool=Best(owner);
        bool Good(NPC n)=>n!=null&&Candidate(n,Tool)&&Vector2.DistanceSquared(n.Center,owner.Center)<240*240&&
            Vector2.DistanceSquared(n.Center,cat.Center)<240*240&&Reachability.Clear(cat.Center,n.Center)&&
            InventoryTransfer.HasRoom(new Item(n.catchItem),s.Bag);
        if(!Good(target)||age>CompanionWorkTiming.Timeout(180))Cancel();
        if(target==null&&clock%30==0) {
            float distance=float.MaxValue;
            foreach(var n in Main.npc)if(Good(n)){float d=Vector2.DistanceSquared(cat.Center,n.Center);if(d<distance){distance=d;target=n;}}
        }
        if(target==null)return false;
        age++;Target=target.Center;goal=Target;s.Status=CompanionText.Message("CompanionCatching.CatchingCritters");
        if(Vector2.DistanceSquared(cat.Center,Target)>52*52){
            // Approach a free body pocket within net reach, not soil under the critter.
            if(((CompanionCat)cat.ModProjectile).Navigation.Riding){
                bool found=false;
                foreach(int dy in new[]{0,-16,-24,-32})foreach(int dx in new[]{0,-16,16}){
                    var at=Target+new Vector2(dx,dy);
                    if(!found&&CompanionNavigation.ClearFlight(cat.Center,at)){goal=at;found=true;}
                }
                if(!found){Cancel();return false;}
            }
            return true;
        }
        goal=cat.Center;((CompanionCat)cat.ModProjectile).FaceWork(Target);
        if(SwingTicks==0){Duration=CompanionWorkTiming.Ticks(Math.Clamp(Tool.useAnimation,12,60));SwingTicks=Duration;CompanionItemVisual.BeginUse(Tool,cat.Center);}
        if(--SwingTicks==Duration/3) {
            // Never replace a live player. Scope is synchronous and restored even on hook failure.
            int slot=CompanionNetwork.Server?owner.whoAmI:254;if(!CompanionNetwork.Server&&Main.player[slot].active){Cancel();return false;}
            Player backup=Main.player[slot];int mine=Main.myPlayer;
            actor.whoAmI=slot;actor.Center=cat.Center;actor.inventory[0]=Tool.Clone();actor.selectedItem=0;actor.active=true;
            var rect=new Rectangle((int)cat.Center.X-56,(int)cat.Center.Y-56,112,112);
            try {
                Main.player[slot]=actor; // Keep the real local owner for native drop reservation.
                if(Good(target))NPC.CheckCatchNPC(target,rect,Tool,actor,ItemID.Sets.LavaproofCatchingTool[Tool.type]);
            } finally {Main.player[slot]=backup;Main.myPlayer=mine;actor.active=false;}
            // Captured item is a real native ground drop at the cat, collected by
            // the existing transactional path. No invented catch item/duplicate.
            Vector2 same=cat.Center;((CompanionCat)cat.ModProjectile).Pickup.Tick(owner,s,cat,ref same,false);
        }
        if(SwingTicks==0||!target.active)target=null;
        return true;
    }
}
