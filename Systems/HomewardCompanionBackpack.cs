using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ContinentOfJourney.Items.Accessories.MechArms;
using ContinentOfJourney.Projectiles;

namespace PalicoCompanion.Systems;

[ExtendsFromMod("ContinentOfJourney")]
[JITWhenModsEnabled("ContinentOfJourney")]
public sealed class HomewardCompanionBackpack:ModSystem {
    // Native MechArmPlayer owns capacity, reach, grab rules and claw bonuses.
    ILHook grab;
    public override void Load(){
        CompanionCrossMod.BeforeReset+=Initialize;
        CompanionCrossMod.UpdateEquipment+=Equip;
        CompanionCrossMod.SentryPlacement+=Placement;
        CompanionCrossMod.ArmAccepts=Accepts;
        grab=new ILHook(typeof(ClawProj).GetMethod(nameof(ClawProj.AI)),il=>{
            var cursor=new ILCursor(il);
            if(!cursor.TryGotoNext(MoveType.Before,i=>i.MatchLdfld<Projectile>(nameof(Projectile.sentry))))
                throw new InvalidOperationException("Homeward mechanical backpack sentry query was not found.");
            cursor.Remove();
            cursor.EmitDelegate<Func<Projectile,bool>>(p=>p.sentry&&
                (CompanionNativeUse.Current is {} use?CompanionOwnedShot.Of(p)==use:CompanionOwnedShot.Of(p)==null));
        });
    }
    public override void Unload(){
        CompanionCrossMod.BeforeReset-=Initialize;CompanionCrossMod.UpdateEquipment-=Equip;
        CompanionCrossMod.SentryPlacement-=Placement;CompanionCrossMod.ArmAccepts=null;grab?.Dispose();grab=null;
    }
    static void Initialize(CompanionNativeUse use){
        // Native players initialize this in LoadData; temporary actors never
        // load a player file. Do not copy the owner's installed claws.
        use.Actor.GetModPlayer<MechArmPlayer>().ArmsList??=new Dictionary<byte,int>();
    }
    static void Equip(CompanionNativeUse use){
        var mech=use.Actor.GetModPlayer<MechArmPlayer>();
        var arms=mech.ArmsList;
        arms.Clear();
        int limit=Math.Min(7,Math.Max(0,mech.ArmMaxNum));
        for(int key=0;key<use.State.ArmLayout.Items.Length&&arms.Count<limit;key++){
            var item=use.State.ArmLayout.Items[key];
            if(!item.IsAir&&Legal(mech,key,item.type))arms[(byte)key]=item.type;
        }
    }
    internal static MechArmPlayer Arms(CompanionPlayer s)=>
        s.Cat?.ModProjectile is CompanionCat cat?cat.Weapons.Actor?.GetModPlayer<MechArmPlayer>():null;
    internal static bool Accepts(CompanionPlayer s,Item item,int key){
        if(key is <0 or >=169||key==84)return false;
        if(item.IsAir)return true; // Installed items remain removable without the backpack.
        var mech=Arms(s);
        return mech?.MechPackTier>0&&Legal(mech,key,item.type)&&
            (!s.ArmLayout.Items[key].IsAir||s.ArmLayout.Count<Math.Min(7,mech.ArmMaxNum));
    }
    internal static bool Legal(MechArmPlayer mech,int key,int type){
        if(mech==null||key is <0 or >=169||key==84||!MechArmPlayer.MechArmIDs.ContainsKey(type))return false;
        int range=mech.GetShowRangeFromType(type);
        if(Math.Abs(key%13-6)>Math.Min(6,range+1)||Math.Abs(key/13-6)>range)return false;
        bool existed=mech.ArmsList.TryGetValue((byte)key,out int old);
        try{mech.ArmsList[(byte)key]=type;return mech.isLegalClaw(key);}
        finally{if(existed)mech.ArmsList[(byte)key]=old;else mech.ArmsList.Remove((byte)key);}
    }
    static CompanionSentryPlacement? Placement(CompanionNativeUse use){
        var mech=use.Actor.GetModPlayer<MechArmPlayer>();
        if(mech.MechPackTier<=0||mech.ArmsList==null||mech.ArmsList.Count==0)return null;
        if(mech.clawProj<0||mech.clawProj>=Main.maxProjectiles||
            !Main.projectile[mech.clawProj].active||Main.projectile[mech.clawProj].ModProjectile is not ClawProj claw)return new CompanionSentryPlacement(null,0);
        int index=0,carried=0;Vector2? free=null;
        foreach(var arm in mech.ArmsList){
            if(index>=mech.ArmMaxNum||index>=claw.gripWhoAmI.Length)break;
            int held=claw.gripWhoAmI[index++];
            if(held>=0&&held<Main.maxProjectiles&&Main.projectile[held].active&&
                Main.projectile[held].GetGlobalProjectile<CoJGlobalProjectile_Instance>().clawPosition==arm.Key){
                if(Main.projectile[held].sentry&&CompanionOwnedShot.Of(Main.projectile[held])==use)carried++;
                continue;
            }
            Vector2 point=use.Actor.MountedCenter+mech.getArmVectorPosition(arm.Key);
            if(free==null&&!Collision.SolidCollision(point-new Vector2(12),24,24))free=point;
        }
        return new CompanionSentryPlacement(free,carried);
    }
}

// Homeward's native packet omits clawPosition. Sync only the cat's attachment,
// using the existing projectile channel; native AI handles motion and effects.
[ExtendsFromMod("ContinentOfJourney")]
[JITWhenModsEnabled("ContinentOfJourney")]
public sealed class HomewardCompanionClawSync:GlobalProjectile {
    public override bool InstancePerEntity=>true;
    int previous=-1;
    public override void PostAI(Projectile projectile){
        if(CompanionOwnedShot.Of(projectile) is not {} use)return;
        if(CompanionNetwork.Client&&projectile.ModProjectile is ClawProj claw){
            // The native grab loop skips already attached projectiles. Rebuild
            // its visual grip indices from the authoritative attachment keys.
            Array.Fill(claw.gripWhoAmI,-1);
            var arms=use.Actor.GetModPlayer<MechArmPlayer>().ArmsList;
            if(arms!=null)foreach(var p in Main.projectile){
                if(!p.active||CompanionOwnedShot.Of(p)!=use)continue;
                int key=p.GetGlobalProjectile<CoJGlobalProjectile_Instance>().clawPosition,index=0;
                foreach(var arm in arms){
                    if(index>=claw.gripWhoAmI.Length)break;
                    if(arm.Key==key){claw.gripWhoAmI[index]=p.whoAmI;break;}index++;
                }
            }
        }
        int position=projectile.GetGlobalProjectile<CoJGlobalProjectile_Instance>().clawPosition;
        if(position==previous)return;
        previous=position;if(CompanionNetwork.Server)projectile.netUpdate=true;
    }
    public override void SendExtraAI(Projectile projectile,BitWriter bits,BinaryWriter writer){
        bool cat=CompanionOwnedShot.Of(projectile)!=null;bits.WriteBit(cat);
        if(cat)writer.Write((short)projectile.GetGlobalProjectile<CoJGlobalProjectile_Instance>().clawPosition);
    }
    public override void ReceiveExtraAI(Projectile projectile,BitReader bits,BinaryReader reader){
        if(!bits.ReadBit())return;
        int position=reader.ReadInt16();
        if(CompanionNetwork.Client)projectile.GetGlobalProjectile<CoJGlobalProjectile_Instance>().clawPosition=position is >=0 and <169?position:-1;
    }
}
