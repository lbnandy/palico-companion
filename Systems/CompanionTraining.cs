using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.DataStructures;

namespace PalicoCompanion.Systems;

// Character progression and cooldowns are distinct from transferable equipment.
public sealed partial class CompanionTraining {
    public Item[] Slots=InventoryTransfer.Empty(4);
    public int Tier; public bool Expanded;
    public int[] Cooldowns=new int[8],SlotCooldowns=new int[4],SlotCooldownTotals=new int[4];
    public int Drum,ManaDance,ManaTarget,SlowHealing,CleanseTime,CleanseType;
    internal int EffectDuration(int kind)=>kind switch {0=>300,2=>60*(2+Tier),3=>360,4=>600,_=>0};
    internal static int BombDamage(int tier)=>new[]{120,480,1200}[Math.Clamp(tier,0,2)];
    internal int ManaDeficit(int maximum)=>Math.Min(new[]{20,40,60}[Tier],Math.Max(1,(maximum+1)/2));
    public int Count(Player p)=>1+(Expanded?1:0)+(p.extraAccessory&&Main.expertMode?1:0);
    // The native weapon scope temporarily hides sibling projectile owners in multiplayer.
    // Resolve through that scope before looking up a cat by owner number.
    static Projectile CatFor(CompanionPlayer s)=>ReferenceEquals(CompanionNativeUse.Current?.State,s)?CompanionNativeUse.Current.Cat:s.Cat;
    public bool Active(CompanionPlayer s)=>s.Enabled&&!s.Stowed&&!s.Paused&&!s.Player.dead&&!s.Vitals.Downed&&CatFor(s)!=null;
    public int Find(CompanionPlayer s,int kind){for(int i=0;i<Math.Min(3,Count(s.Player));i++)if(TrainingCatalog.Id(Slots[i])==kind)return i;return -1;}
    public bool Work(int kind)=>TrainingCatalog.Id(Slots[3])==kind;
    // Old item flags remain readable for material refunds, but no longer select modes.
    public bool CanUpgrade(int kind)=>kind==13&&!Expanded||kind==14&&Tier==0||kind==15&&Tier==1;
    public bool Ready(CompanionPlayer s,int kind)=>Active(s)&&Find(s,kind) is int i&&i>=0&&Cooldowns[kind]<=0&&SlotCooldowns[i]<=0;
    public bool Use(CompanionPlayer s,int kind,int seconds,Vector2? at=null){if(!Ready(s,kind))return false;int i=Find(s,kind);Cooldowns[kind]=SlotCooldowns[i]=SlotCooldownTotals[i]=seconds*60;if(!CompanionNetwork.Client)Cue(s,kind,at);return true;}
    public bool Accepts(CompanionPlayer s,Item item,int slot){if(slot<0||slot>3)return false;if(item.IsAir)return true;int k=TrainingCatalog.Id(item);if(slot==3)return k>=8&&k<=10;if(slot>=Count(s.Player)||k<0||k>=8)return false;for(int i=0;i<3;i++)if(i!=slot&&TrainingCatalog.Id(Slots[i])==k)return false;return true;}
    internal bool TryEquip(CompanionPlayer s,Item item){if(item.IsAir)return false;for(int i=0;i<4;i++)if(Slots[i].IsAir&&Accepts(s,item,i)){Slots[i]=item.Clone();Slots[i].stack=1;if(--item.stack<=0)item.TurnToAir();return true;}return false;}
    public bool Apply(Item material,int slot){int k=TrainingCatalog.Id(material);bool ok=false;
        if(k==13&&!Expanded){Expanded=true;ok=true;}
        if(k==14&&Tier==0){Tier=1;ok=true;}
        if(k==15&&Tier==1){Tier=2;ok=true;}
        if(ok&&--material.stack<=0)material.TurnToAir();return ok;
    }
    public float WorkSpeed(int kind)=>kind==12?1.3f:Work(kind)?1.2f+.05f*Tier:1;
    public int MiningReach=>Work(9)?Tier+1:0;
    public float Damage(CompanionPlayer s)=>Active(s)&&Find(s,4)>=0&&Drum>0?1.2f+.05f*Tier:1;
    public float Speed(CompanionPlayer s)=>Damage(s)>1?1.1f+.05f*Tier:1;
    internal static int CooldownSeconds(int kind)=>kind switch {0 or 1=>60,2=>40,3 or 5=>35,4=>45,6=>30,7=>18,_=>0};
    public float Summon(CompanionPlayer s)=>Active(s)&&Find(s,4)>=0&&Drum>0?new[]{1.25f,1.35f,1.45f}[Tier]:1;
    internal void ReturnLocked(CompanionPlayer s){if(!Slots[3].IsAir&&TrainingCatalog.Id(Slots[3]) is not (>=8 and <=10))InventoryTransfer.Move(Slots[3],s.Bag);for(int i=Count(s.Player);i<3;i++)if(!Slots[i].IsAir)InventoryTransfer.Move(Slots[i],s.Bag);}
    public TagCompound Save()=>new(){["tier"]=Tier,["expanded"]=Expanded,["items"]=Slots.Select(ItemIO.Save).ToList(),["cooldowns"]=Cooldowns,["slotCooldowns"]=SlotCooldowns,["slotCooldownTotals"]=SlotCooldownTotals};
    public void Load(TagCompound tag){Tier=Math.Clamp(tag.GetInt("tier"),0,2);Expanded=tag.GetBool("expanded");var a=tag.GetList<TagCompound>("items");for(int i=0;i<Math.Min(4,a.Count);i++)Slots[i]=ItemIO.Load(a[i]);Copy(tag.GetIntArray("cooldowns"),Cooldowns);Copy(tag.GetIntArray("slotCooldowns"),SlotCooldowns);Copy(tag.GetIntArray("slotCooldownTotals"),SlotCooldownTotals);}
    static void Copy(int[] from,int[] to){for(int i=0;i<Math.Min(from.Length,to.Length);i++)to[i]=Math.Clamp(from[i],0,4500);}
    internal void Write(BinaryWriter w){w.Write((byte)Tier);w.Write(Expanded);foreach(var item in Slots)ItemIO.Send(item,w,true,true);WriteTimers(w);}
    internal void WriteTimers(BinaryWriter w){foreach(int x in Cooldowns)w.Write(x);foreach(int x in SlotCooldowns)w.Write(x);foreach(int x in SlotCooldownTotals)w.Write(x);w.Write(Drum);w.Write(ManaDance);w.Write(ManaTarget);w.Write(SlowHealing);w.Write(CleanseTime);w.Write(CleanseType);}
    internal void ReadTimers(BinaryReader r){for(int i=0;i<8;i++)Cooldowns[i]=Math.Clamp(r.ReadInt32(),0,4500);for(int i=0;i<4;i++)SlotCooldowns[i]=Math.Clamp(r.ReadInt32(),0,4500);for(int i=0;i<4;i++)SlotCooldownTotals[i]=Math.Clamp(r.ReadInt32(),0,4500);Drum=r.ReadInt32();ManaDance=r.ReadInt32();ManaTarget=r.ReadInt32();SlowHealing=r.ReadInt32();CleanseTime=r.ReadInt32();CleanseType=r.ReadInt32();}
    internal static CompanionTraining Read(BinaryReader r){var t=new CompanionTraining{Tier=Math.Clamp((int)r.ReadByte(),0,2),Expanded=r.ReadBoolean()};for(int i=0;i<4;i++)t.Slots[i]=ItemIO.Receive(r,true,true);for(int i=0;i<8;i++)t.Cooldowns[i]=Math.Clamp(r.ReadInt32(),0,4500);for(int i=0;i<4;i++)t.SlotCooldowns[i]=Math.Clamp(r.ReadInt32(),0,4500);for(int i=0;i<4;i++)t.SlotCooldownTotals[i]=Math.Clamp(r.ReadInt32(),0,4500);t.Drum=r.ReadInt32();t.ManaDance=r.ReadInt32();t.ManaTarget=r.ReadInt32();t.SlowHealing=r.ReadInt32();t.CleanseTime=r.ReadInt32();t.CleanseType=r.ReadInt32();return t;}
    static readonly int[] Cleansable=[BuffID.Poisoned,BuffID.OnFire,BuffID.Venom,BuffID.CursedInferno,BuffID.Frostburn,BuffID.Slow,BuffID.Weak,BuffID.BrokenArmor,BuffID.Bleeding];
    internal static int MostUrgentDebuff(Player p,bool manual=false) {
        int best=0;float score=0;
        foreach(int type in Cleansable){int index=p.FindBuffIndex(type);if(index<0)continue;
            // Compare the harm still to come, rather than the order of an ID list.
            float weight=type==BuffID.Venom||type==BuffID.CursedInferno?6:type==BuffID.Frostburn?4:type==BuffID.OnFire?3:type==BuffID.BrokenArmor?5:type==BuffID.Slow?3:2;
            int time=p.buffTime[index];bool severe=type is BuffID.Venom or BuffID.CursedInferno or BuffID.BrokenArmor;
            // Do not spend forty seconds of protection on a fading nuisance.
            if(!manual&&time<(severe?90:180))continue;
            float value=weight*Math.Min(600,time);
            if(value>score){score=value;best=type;}
        }return best;
    }
    internal static NPC SupportTarget(CompanionPlayer s,Projectile cat,bool bomb) {
        NPC best=null;float bestScore=float.MinValue;float radius=bomb?560:900;
        foreach(var n in Main.npc){float distance=Vector2.DistanceSquared(n.Center,cat.Center);
            if(!n.active||!n.CanBeChasedBy()||distance>radius*radius||!Collision.CanHitLine(cat.Center,1,1,n.position,n.width,n.height))continue;
            if(!bomb&&n.GetGlobalNPC<TrainingMark>().Remaining(s.Player.whoAmI)>120)continue;
            float score;
            if(bomb){int group=0;foreach(var other in Main.npc)if(other.active&&other.CanBeChasedBy()&&Vector2.DistanceSquared(n.Center,other.Center)<80*80)group++;
                score=group*1000+(n.boss?200:0)+Math.Min(n.life,1000)*.1f-distance/(radius*radius);
            }else score=(n.boss?10000:0)+Math.Min(n.life,3000)+n.defense*10-distance/(radius*radius);
            if(score>bestScore){bestScore=score;best=n;}
        }return best;
    }
    public void Tick(CompanionPlayer s){
        if(!CompanionNetwork.Client)ReturnLocked(s);
        for(int i=0;i<8;i++)if(Cooldowns[i]>0)Cooldowns[i]--;for(int i=0;i<4;i++)if(SlotCooldowns[i]>0)SlotCooldowns[i]--;
        if(Drum>0)Drum--;if(ManaDance>0)ManaDance--;if(CleanseTime>0)CleanseTime--;
        if(!Active(s)){Drum=ManaDance=SlowHealing=0;return;}
        ReconcileEffects(s);
        if(CompanionNetwork.Client)return;
        var p=s.Player;var cat=s.Cat;var use=(cat.ModProjectile as CompanionCat)?.Weapons;
        if(SlowHealing>0){if(SlowHealing%60==0)OwnerEffect(s,0,1+Tier);SlowHealing--;}
        foreach(int kind in new[]{0,2,3,4,5,7})Activate(s,kind,false,out _);
    }

    internal static void OwnerEffect(CompanionPlayer s,byte kind,int value){if(CompanionNetwork.Server){CompanionNetwork.TrainingEffect(s,kind,value);return;}ApplyOwner(s,kind,value);}
    internal static void ApplyOwner(CompanionPlayer s,byte kind,int value){var p=s.Player;var t=s.Training;
        if(kind==TrainingWorkFeedback.NoticeKind){TrainingWorkFeedback.Present(s,value);return;}
        if(kind==0&&!p.dead){int n=Math.Min(value,p.statLifeMax2-p.statLife);if(n>0){p.statLife+=n;p.HealEffect(n);}}
        if(kind==1){int n=Math.Min(value,p.statManaMax2-p.statMana);p.statMana+=n;if(n>0)p.ManaEffect(n);t.ManaDance=t.EffectDuration(3);t.ManaTarget=0;}
        if(kind==2&&Array.IndexOf(Cleansable,value)>=0){p.ClearBuff(value);t.CleanseType=value;t.CleanseTime=t.EffectDuration(2);}
        if(kind==3){if(p.wingTimeMax>0)p.wingTime=Math.Min(p.wingTimeMax,p.wingTime+new[]{36,54,72}[t.Tier]);else p.velocity.Y=p.gravDir*Math.Min(p.velocity.Y*p.gravDir,-6f);}
        if(CompanionNetwork.Client&&kind is 0 or 1)NetMessage.SendData(kind==1?MessageID.PlayerMana:MessageID.PlayerLifeMana,-1,-1,null,p.whoAmI);
    }
    internal static void Cue(CompanionPlayer s,int kind,Vector2? at=null)=>CompanionFeedback.Show(6,at??(CatFor(s)?.Center??s.Player.Center),kind,"cast");
    internal int Shield(CompanionPlayer s,int damage,bool cat,bool consume=true){if(!Ready(s,1)||cat&&(s.Player.statLife<=s.Player.statLifeMax2*.7f||s.Vitals.Health>s.Vitals.MaxHealth*.35f&&damage<s.Vitals.Health)||damage<Math.Max(20,(cat?s.Vitals.MaxHealth:s.Player.statLifeMax2)/10)&&damage<(cat?s.Vitals.Health:s.Player.statLife))return damage;if(consume){if(!Use(s,1,60,cat?CatFor(s)?.Center:s.Player.Center))return damage;s.Expression.GearUsed(s,1,cat);}return Math.Max(1,damage-new[]{15,30,40}[Tier]);}
}

public sealed class TrainingHooks:ModPlayer {
    bool guarded;int jumpIntent,airTicks;
    public override void OnHurt(Player.HurtInfo info){if(guarded){guarded=false;if(State.Training.Use(State,1,60,Player.Center)){if(CompanionNetwork.Client)CompanionNetwork.TrainingSpent(1);else State.Expression.GearUsed(State,1);}}}
    CompanionPlayer State=>Player.GetModPlayer<CompanionPlayer>();
    public override void PostUpdate(){if(ReferenceEquals(Player,CompanionNativeUse.Current?.Actor)||ReferenceEquals(Player,CompanionFishing.Current?.Actor))return;var s=State;if(CompanionNetwork.Server&&(!s.NetReady||s.NetLeaving))return;s.Training.Tick(s);
        bool airborne=Player.velocity.Y!=0;
        airTicks=airborne?airTicks+1:0;jumpIntent=airborne&&Player.controlJump?jumpIntent+1:0;
        bool climbing=Player.wingTimeMax>0?airTicks>=30&&jumpIntent>=18&&Player.wingTime<=2:airTicks>=35&&jumpIntent>=30&&Player.velocity.Y*Player.gravDir>=-1;
        if(Player.whoAmI==Main.myPlayer&&!Main.dedServ&&s.Training.Ready(s,6)&&!Player.mount.Active&&climbing&&Player.jump==0){if(s.Training.Use(s,6,30,Player.Center)){CompanionTraining.ApplyOwner(s,3,0);if(CompanionNetwork.Client)CompanionNetwork.TrainingSpent(6);else s.Expression.GearUsed(s,6);}}
    }
    public override void PostUpdateMiscEffects(){var s=State;var t=s.Training;if(!t.Active(s))return;if(t.Find(s,3)>=0&&t.ManaDance>0&&t.ManaTarget==0)Player.manaCost*=.9f-.05f*t.Tier;if(t.Find(s,2)>=0&&t.CleanseTime>0&&t.CleanseType>0&&t.CleanseType<Player.buffImmune.Length)Player.buffImmune[t.CleanseType]=true;}
    public override void ModifyHurt(ref Player.HurtModifiers modifiers){if(Player.whoAmI!=Main.myPlayer||ReferenceEquals(Player,CompanionNativeUse.Current?.Actor))return;modifiers.ModifyHurtInfo+=(ref Player.HurtInfo info)=>{int before=info.Damage;info.Damage=State.Training.Shield(State,info.Damage,false,false);guarded=info.Damage<before;};}
    // Fishing bait consumption remains native; the basket adds fishing power only.
}

public sealed class TrainingMark:GlobalNPC {
    public override bool InstancePerEntity=>true;
    int[] until=new int[256],power=new int[256];
    public override void SendExtraAI(NPC npc,BitWriter bits,BinaryWriter w){w.Write((byte)until.Count(v=>v>0));for(int i=0;i<256;i++)if(until[i]>0){w.Write((byte)i);w.Write((ushort)until[i]);w.Write((byte)power[i]);}}
    public override void ReceiveExtraAI(NPC npc,BitReader bits,BinaryReader r){Array.Clear(until);Array.Clear(power);int count=r.ReadByte();for(int i=0;i<count;i++){int owner=r.ReadByte();until[owner]=r.ReadUInt16();power[owner]=r.ReadByte();}}
    internal int Remaining(int owner)=>owner>=0&&owner<256?until[owner]:0;
    internal void Mark(int owner,int tier){if(owner<0||owner>=256)return;until[owner]=new[]{900,1080,1200}[tier];power[owner]=new[]{4,8,12}[tier];}
    public override void PostAI(NPC npc){for(int i=0;i<256;i++)if(until[i]>0)until[i]--;}
    void Modify(int owner,ref NPC.HitModifiers m){if(owner>=0&&owner<256&&until[owner]>0)m.ArmorPenetration+=power[owner];}
    public override void ModifyHitByItem(NPC npc,Player player,Item item,ref NPC.HitModifiers m)=>Modify(CompanionNativeUse.Current?.State.Player.whoAmI??player.whoAmI,ref m);
    public override void ModifyHitByProjectile(NPC npc,Projectile p,ref NPC.HitModifiers m){var use=CompanionOwnedShot.Of(p);Modify(use?.State.Player.whoAmI??p.owner,ref m);if(use!=null&&(p.minion||ProjectileID.Sets.MinionShot[p.type]))m.SourceDamage*=use.State.Training.Summon(use.State);}
}
public sealed class TrainingDart:ModProjectile {
    public override string Texture=>"PalicoCompanion/Assets/Training/PiercingBoomerang";
    public override void SetDefaults(){Projectile.width=8;Projectile.height=8;Projectile.friendly=true;Projectile.penetrate=1;Projectile.timeLeft=90;Projectile.tileCollide=true;}
    public override void AI()=>Projectile.rotation+=.4f*(Projectile.velocity.X<0?-1:1);
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone){var s=CompanionOwnedShot.Of(Projectile)?.State;if(s==null)return;target.GetGlobalNPC<TrainingMark>().Mark(s.Player.whoAmI,s.Training.Tier);target.netUpdate=true;CompanionFeedback.Show(6,target.Center,5,"impact");}
}
public sealed class TrainingBomb:ModProjectile {
    internal const int BlastSize=160;
    readonly System.Collections.Generic.HashSet<int> roots=new();
    public override bool? CanHitNPC(NPC target)=>roots.Contains(target.realLife>=0?target.realLife:target.whoAmI)?false:null;
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)=>roots.Add(target.realLife>=0?target.realLife:target.whoAmI);
    public override string Texture=>"PalicoCompanion/Assets/Training/BombPot";
    public override void SetDefaults(){Projectile.width=14;Projectile.height=14;Projectile.friendly=true;Projectile.timeLeft=45;Projectile.penetrate=-1;Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=-1;}
    public override void AI(){if(Projectile.localAI[0]++==0&&Projectile.ai[0]>0)Projectile.timeLeft=(int)Projectile.ai[0];Projectile.rotation+=.2f;Projectile.velocity.Y+=.1f;if(Projectile.timeLeft==2){Projectile.Resize(BlastSize,BlastSize);Projectile.velocity=Vector2.Zero;Projectile.tileCollide=false;if(!CompanionNetwork.Client)CompanionFeedback.Show(6,Projectile.Center,7,"impact");}}
    public override bool PreDraw(ref Color lightColor)=>Projectile.timeLeft>2;
    public override bool? CanDamage()=>Projectile.timeLeft<=2;
    public override bool OnTileCollide(Vector2 oldVelocity){Projectile.velocity=Vector2.Zero;Projectile.timeLeft=Math.Min(Projectile.timeLeft,3);return false;}
}
