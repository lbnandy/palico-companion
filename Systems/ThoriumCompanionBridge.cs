using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.GameContent.UI;
using Terraria.ModLoader;
using ThoriumMod;
using ThoriumMod.Items;
using ThoriumMod.Utilities;

namespace PalicoCompanion.Systems;

[ExtendsFromMod("ThoriumMod")]
[JITWhenModsEnabled("ThoriumMod")]
public sealed class ThoriumCompanionBridge:ModSystem {
    sealed class Resources {internal bool Waiting;internal ulong Tick=ulong.MaxValue;}
    static ConditionalWeakTable<CompanionNativeUse,Resources> resources=new();
    static Action<BardItem,Player,Player> empower;
    static Action<Player,SoundStyle,float> playSound;
    static Action<ThoriumPlayer> findAlly;
    Hook instrumentUse,instrumentSound,instrumentEmpower,playerLookup,mouseLookup,healing;
    delegate int HealOriginal(Player player,int amount,Player healer,bool overMax,bool statistics);
    delegate void UseOriginal(BardItem item,Player player);
    delegate void SoundOriginal(BardItem item,Player player,float pitch);
    delegate ThoriumPlayer PlayerOriginal(Player player);
    delegate bool MouseOriginal(MousePlayer player,out Vector2 position);
    delegate void EmpowerOriginal(BardItem item,Player player,BitsByte info);
    const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
    public override void Load(){
        empower=typeof(BardItem).GetMethod("ApplyEmpowerments",PrivateInstance,null,new[]{typeof(Player),typeof(Player)},null)!.CreateDelegate<Action<BardItem,Player,Player>>();
        playSound=typeof(BardItem).GetMethod("PlaySound",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<Action<Player,SoundStyle,float>>();
        findAlly=typeof(ThoriumPlayer).GetMethod("HandleLowestLifeAlly",PrivateInstance)!.CreateDelegate<Action<ThoriumPlayer>>();
        instrumentUse=new Hook(typeof(BardItem).GetMethod("UseItem_Inner",PrivateInstance)!,UseInstrument);
        instrumentSound=new Hook(typeof(BardItem).GetMethod("PlayInstrument")!,PlayInstrument);
        instrumentEmpower=new Hook(typeof(BardItem).GetMethod("NetApplyEmpowerments")!,EmpowerInstrument);
        playerLookup=new Hook(typeof(PlayerHelper).GetMethod("GetThoriumPlayer")!,GetPlayer);
        mouseLookup=new Hook(typeof(MousePlayer).GetMethod("TryGetMousePosition")!,GetMouse);
        healing=new Hook(typeof(PlayerHelper).GetMethod("HealLife")!,Healed);
        CompanionCrossMod.SupportTool=Support;CompanionCrossMod.SafeThrow+=Throw;
        CompanionCrossMod.InheritStats+=Stats;
        CompanionCrossMod.UpdateResources+=Update;CompanionCrossMod.SupportTarget=Target;
        CompanionCrossMod.ResourceReady+=Ready;CompanionCrossMod.WeaponEffect=Effect;
        CompanionCrossMod.PlanUse=Plan;CompanionCrossMod.WeaponReach+=Range;
    }
    public override void Unload(){
        instrumentUse?.Dispose();instrumentSound?.Dispose();instrumentUse=instrumentSound=null;
        instrumentEmpower?.Dispose();instrumentEmpower=null;
        playerLookup?.Dispose();mouseLookup?.Dispose();playerLookup=mouseLookup=null;
        healing?.Dispose();healing=null;
        CompanionCrossMod.InheritStats-=Stats;
        CompanionCrossMod.UpdateResources-=Update;CompanionCrossMod.SupportTool=null;CompanionCrossMod.SafeThrow-=Throw;
        CompanionCrossMod.SupportTarget=null;CompanionCrossMod.ResourceReady-=Ready;CompanionCrossMod.WeaponEffect=null;
        CompanionCrossMod.PlanUse=null;CompanionCrossMod.WeaponReach-=Range;
        resources=new();empower=null;playSound=null;findAlly=null;
    }
    static void Stats(Player actor,Player owner,float ratio){
        var target=actor.GetModPlayer<ThoriumPlayer>();var source=owner.GetModPlayer<ThoriumPlayer>();
        // Healing amount was already shared before combat inheritance existed.
        target.healBonus=Math.Max(0,source.healBonus);
        target.healingSpeed=CompanionCombatInheritance.Blend(1f,source.healingSpeed,ratio);
        target.bardResourceMax=Math.Clamp(source.bardResourceMax,10,40);
        target.bardResourceMax2=CompanionCombatInheritance.Blend(target.bardResourceMax,Math.Clamp(source.bardResourceMax2,target.bardResourceMax,target.bardResourceMax+20),ratio);
        target.consumedInspirationGem=source.consumedInspirationGem;
        target.inspirationRegenBonus=CompanionCombatInheritance.Blend(target.consumedInspirationGem?1.1f:1,source.inspirationRegenBonus,ratio);
        target.inspirationConsume=source.inspirationConsume*ratio;
        target.throwerExhaustionMax=CompanionCombatInheritance.Blend(1200,source.throwerExhaustionMax,ratio);
        target.throwerExhaustionRegenBonus=source.throwerExhaustionRegenBonus*ratio;
    }
    static bool Support(Item item)=>item.ModItem is ThoriumItem {isHealer:true}&&item.damage<=0&&
        item.CountsAsClass(ThoriumDamageBase<HealerTool>.Instance)&&
        (item.shoot>0||item.ModItem is ThoriumMod.Items.HealerItems.Renew)&&!item.consumable;
    static bool Throw(Item item)=>item.ModItem is ThoriumItem {isThrower:true}&&item.damage>0&&item.shoot>0&&
        item.createTile<0&&item.createWall<0&&item.ammo==0;
    static void Update(CompanionNativeUse use){
        if(CompanionNetwork.Client)return;
        var clock=resources.GetOrCreateValue(use);if(clock.Tick==Main.GameUpdateCount)return;clock.Tick=Main.GameUpdateCount;
        var actor=use.Actor;var tp=actor.GetModPlayer<ThoriumPlayer>();
        actor.team=use.State.Player.team;actor.hostile=use.State.Player.hostile;
        // Stats supplies resource limits on every peer; only the authority
        // advances the companion's own resource and recovery clocks.
        tp.bardResource=Math.Clamp(tp.bardResource,0,tp.bardResourceMax2);
        // Thorium 1.7.2 keeps these clocks inline in PostUpdateEquips. Advance
        // only those clocks once per simulation tick; running that entire hook
        // would also mutate shared UI/world state and trigger equipment effects.
        if(tp.bardResource<tp.bardResourceMax2){
            if(tp.inspirationRegenTimer<8)tp.inspirationRegenTimer+=(int)((1+tp.inspirationRegenBase)*tp.inspirationRegenBonus);
            else {tp.inspirationRegenBase=Math.Min(5,tp.inspirationRegenBase+.15f);tp.inspirationRegenTimer=0;tp.bardResource++;}
            if(tp.bardResource==tp.bardResourceMax2)tp.inspirationRegenBase=0;
        }
        if(!tp.throwerExhaustionPenalty)tp.throwerExhaustionDamage=1;
        tp.throwerExhaustion=Math.Max(0,tp.throwerExhaustion-(tp.throwerExhaustionPenalty?tp.throwerExhaustionMax/1200f:1+tp.throwerExhaustionRegenBonus));
        if(tp.throwerExhaustion==0)tp.throwerExhaustionPenalty=false;
        if(actor.HeldItem.ModItem is ThoriumItem {isHealer:true})
            using(new CompanionNativeUse.Scope(use))findAlly(tp);
    }
    static bool Ready(CompanionNativeUse use){
        var tp=use.Actor.GetModPlayer<ThoriumPlayer>();
        if(use.Actor.HeldItem.ModItem is BardItem bard){
            var clock=resources.GetOrCreateValue(use);
            int input=use.Actor.altFunctionUse,cost;
            try {if(input==1)use.Actor.altFunctionUse=2;cost=bard.GetInspirationCost(use.Actor);}
            finally {use.Actor.altFunctionUse=input;}
            if(tp.bardResource<cost){
                if(!clock.Waiting)use.State.Expression.QueueGear(use.State,"GearThoriumRest",EmoteID.EmoteConfused);
                clock.Waiting=true;
            }
            if(tp.bardResource>=tp.bardResourceMax2)clock.Waiting=false;
            return !clock.Waiting;
        }
        return use.Actor.HeldItem.ModItem is not ThoriumItem {isThrowerNon:true}||!tp.throwerExhaustionPenalty;
    }
    static float? Range(Item item){
        if(item.ModItem is ThoriumMod.Items.BardItems.DrumMallet)return 120;
        // Scythes rotate around their owner; their projectile speed is not reach.
        var shot=CompanionCombatReach.Sample(item);
        return shot?.ModProjectile is ThoriumMod.Projectiles.Scythe.ScythePro?shot.width*shot.scale*.5f+8:null;
    }
    static void Plan(CompanionNativeUse use){
        // Auto-reuse can finish the last animation tick and begin another in
        // the same ItemCheck call, so prepare that final tick as well.
        if(use.Actor.itemAnimation>1)return;
        use.Actor.altFunctionUse=0;
        if(use.Actor.HeldItem.ModItem is not ThoriumMod.Items.BardItems.DrumMallet)return;
        use.Actor.direction=use.Aim.X<use.Actor.Center.X?-1:1;
        // Native drum mallet: place a drum with secondary use, then strike it
        // with primary use. Keep its real cost, drum AI and sound-wave damage.
        Vector2 strike=use.Actor.Center+new Vector2(use.Actor.direction*34,0);
        foreach(var shot in Main.ActiveProjectiles)
            if(shot.type==use.Actor.HeldItem.shoot&&CompanionOwnedShot.Of(shot)==use&&
                (Vector2.DistanceSquared(shot.Center,strike)<28*28||shot.Hitbox.Intersects(use.Actor.Hitbox)))return;
        // ItemCheck promotes a pending secondary input (1) to active use (2).
        use.Actor.altFunctionUse=1;
        use.PlacementAim(use.Actor.Center+new Vector2(use.Actor.direction*60,-12));
    }
    static Player Target(CompanionNativeUse use){
        // Renew is natively a self heal; never redirect it to the owner.
        if(use.Actor.HeldItem.ModItem is ThoriumMod.Items.HealerItems.Renew)
            return use.State.Vitals.Health<use.State.Vitals.MaxHealth?use.Actor:null;
        // Thorium declares who each healing tool is intended for. Include the
        // companion only for native self-healing tools; projectile eligibility,
        // healing amount, pickup range and resource cost remain native.
        var healingType=(use.Actor.HeldItem.ModItem as ThoriumItem)?.healType??HealType.None;
        bool self=healingType is HealType.Player or HealType.AllyAndPlayer;
        Player best=self&&use.State.Vitals.Health<use.State.Vitals.MaxHealth?use.Actor:null;
        float score=best!=null?(float)use.State.Vitals.Health/use.State.Vitals.MaxHealth*1000:float.MaxValue;
        foreach(var player in Main.ActivePlayers){
            if(healingType==HealType.Player||player==use.Actor||player.dead||player.ghost||player.statLife>=player.statLifeMax2||
                use.Actor.team!=0&&player.team!=use.Actor.team||player.hostile&&(player.team==0||player.team!=use.Actor.team))continue;
            float distance=Vector2.DistanceSquared(use.Cat.Center,player.Center);
            if(distance>600*600||!CompanionCombatReach.Clear(use.Cat.Center,player.Center))continue;
            float rank=(float)player.statLife/player.statLifeMax2*1000+MathF.Sqrt(distance);
            if(player==use.State.Player)rank-=200;
            if(rank<score){best=player;score=rank;}
        }
        if(best==null)return null;
        // A channelled healer must remain held while a patient needs it; its
        // native projectile owns the charge time, pulses and recurring cost.
        // For single casts, let a moving heal arrive or a placed field work.
        if(!use.Actor.HeldItem.channel)foreach(var shot in Main.ActiveProjectiles)if(shot.type==use.Actor.HeldItem.shoot&&CompanionOwnedShot.Of(shot)==use&&
            (shot.velocity.LengthSquared()>.1f||Vector2.DistanceSquared(shot.Center,best.Center)<120*120))return null;
        return best;
    }
    static CompanionNativeUse Context(Player player)=>CompanionNativeUse.Current is {} use&&ReferenceEquals(use.Actor,player)?use:null;
    // Thorium's helper caches Main.LocalPlayer. Our scoped myPlayer is a
    // virtual actor, so bypass that cache ONLY for this actor's calls.
    static ThoriumPlayer GetPlayer(PlayerOriginal orig,Player player)=>Context(player)!=null?player.GetModPlayer<ThoriumPlayer>():orig(player);
    static bool GetMouse(MouseOriginal orig,MousePlayer player,out Vector2 position){
        if(Context(player.Player) is {} use){position=use.Aim;return true;}
        return orig(player,out position);
    }
    static void UseInstrument(UseOriginal orig,BardItem item,Player player){
        var use=Context(player);if(use==null){orig(item,player);return;}
        if(item.ItemType!=BardItemType.Instrument)return;
        use.State.Expression.QueueGear(use.State,"GearThoriumBard",EmoteID.EmoteNote);
        Emit(use,item,(byte)((item.EmpowerOnUse?1:0)|4));
        if(item.PlayOnUse&&ItemID.Sets.SkipsInitialUseSound[item.Type])PlayInstrument(null,item,player,0);
    }
    static int Healed(HealOriginal orig,Player player,int amount,Player healer,bool overMax,bool statistics){
        int before=player.statLife,result=orig(player,amount,healer,overMax,statistics);
        if(CompanionNativeUse.Current is {} use&&healer==use.Actor&&player!=use.Actor&&player.statLife>before)
            use.State.Expression.QueueGear(use.State,"GearThoriumHeal",EmoteID.ItemLifePotion);
        return result;
    }
    static void PlayInstrument(SoundOriginal orig,BardItem item,Player player,float pitch){
        var use=Context(player);if(use==null){orig(item,player,pitch);return;}
        Emit(use,item,2,pitch:pitch);
    }
    static void EmpowerInstrument(EmpowerOriginal orig,BardItem item,Player player,BitsByte info){
        var use=Context(player);if(use==null){orig(item,player,info);return;}
        Emit(use,item,1,info);
    }
    static void Emit(CompanionNativeUse use,BardItem item,byte effect,byte? info=null,float pitch=0){
        if(CompanionNetwork.Server)CompanionNetwork.WeaponEffect(use,item.Type,effect,info??item.NetInfo,pitch);
        else if(!CompanionNetwork.Client)Effect(use,item.Type,effect,info??item.NetInfo,pitch);
    }
    static void Effect(CompanionNativeUse use,int type,byte effect,byte info,float pitch){
        if(CompanionNetwork.Server||ItemLoader.GetItem(type) is not BardItem)return;
        var item=(use.Actor.HeldItem.type==type?use.Actor.HeldItem:new Item(type)).ModItem as BardItem;
        if(item==null)return;
        var target=CompanionNetwork.Client?Main.LocalPlayer:use.State.Player;
        use.Actor.team=use.State.Player.team;use.Actor.hostile=use.State.Player.hostile;
        var previous=item.NetInfo;item.NetInfo=info;
        try {
            if((effect&1)!=0)empower(item,use.Actor,target);
            if((effect&4)!=0)item.OnPlayInstrument(use.Actor);
            if((effect&2)!=0&&(item.PlayedSound??item.Item.UseSound??ContentSamples.ItemsByType[type].UseSound) is SoundStyle sound)
                playSound(use.Actor,sound,pitch);
        }finally{item.NetInfo=previous;}
    }
}

[ExtendsFromMod("ThoriumMod")]
[JITWhenModsEnabled("ThoriumMod")]
public sealed class ThoriumCompanionItem:GlobalItem {
    public override void ModifyShootStats(Item item,Player player,ref Vector2 position,ref Vector2 velocity,ref int type,ref int damage,ref float knockback){
        if(CompanionNativeUse.Current?.Actor!=player||
            !ContentSamples.ProjectilesByType.TryGetValue(type,out var shot))return;
        bool drum=player.altFunctionUse==2&&item.ModItem is ThoriumMod.Items.BardItems.DrumMallet;
        // Ordinary healing shots use CompanionLaunch; the stationary drum
        // also needs clearance despite not using tile collision in flight.
        if(!drum)return;
        CompanionWeaponAim.ClearLaunchFloor(ref position,shot);
    }
}
