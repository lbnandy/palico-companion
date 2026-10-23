using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
namespace PalicoCompanion.Systems;

// Server simulation; clients send commands and display confirmed snapshots.
internal static partial class CompanionNetwork {
    internal const byte Init=2,Snapshot=3,Command=4,Slot=5,SlotResult=6,Speech=7,Dig=8,Preferences=9,Identity=10,Effect=11,ChestAccess=12,Darkness=13,Bulk=14,BulkResult=15,Feedback=16,CloseRequest=17,CloseAck=18,PickupNotice=19,Resume=20,Chop=21,BuildStroke=22,TrainingNotice=23,TrainingUse=24;
    internal static void TrainingEffect(CompanionPlayer s,byte kind,int value){var p=Packet(TrainingNotice);p.Write(kind);p.Write(value);p.Send(s.Player.whoAmI);}
    internal static void TrainingSpent(byte kind){var p=Packet(TrainingUse);p.Write(kind);p.Send();}
    internal static void BuiltAt(int x,int y,int type){var p=Packet(BuildStroke);p.Write(x);p.Write(y);p.Write(type);p.Send();}
    internal static void ChopAt(int x,int y){var p=Packet(Chop);p.Write(x);p.Write(y);p.Send();}
    internal static void DigAt(Point cell,int type,Vector2 aim){var p=Packet(Dig);p.Write(cell.X);p.Write(cell.Y);p.Write(type);p.Write(aim.X);p.Write(aim.Y);p.Send();}
    internal static void Say(CompanionPlayer s,string line,string context=""){var p=Packet(Speech);p.Write((byte)s.Player.whoAmI);p.Write(line);p.Write(context??"");p.Send(s.Player.whoAmI);}
    internal static bool Client=>Main.netMode==NetmodeID.MultiplayerClient;
    internal static bool Server=>Main.netMode==NetmodeID.Server;
    static int sequence,pending;
    internal static void PickupEffect(CompanionPlayer s,Item item,Vector2 at){var p=Packet(PickupNotice);p.Write(at.X);p.Write(at.Y);ItemIO.Send(item,p,true,true);p.Send(s.Player.whoAmI);}
    internal static void Close(){var p=Packet(CloseRequest);p.Send();}
    internal static void ChestPermission(bool grant){var p=Packet(ChestAccess);p.Write(Main.LocalPlayer.chest);p.Write(grant);p.Send();}
    static bool trainingApplication,trainingReturnToBag;static int trainingUpgradeKind=-1;
    static Item escrow;static Item[] bulkEscrow;
    internal static bool Pending=>pending!=0;
    internal static void UpdateIdentity(CompanionPlayer s){if(!Client)return;var p=Packet(Identity);p.Write(s.CatName);p.Write(s.HoodColor);p.Write(s.Bonded);p.Send();}
    internal static void Configure(CompanionPlayer s){if(!Client||!s.NetReady)return;var p=Packet(Preferences);WritePreferences(s,p);p.Send();}
    static void WritePreferences(CompanionPlayer s,BinaryWriter w){var c=ModContent.GetInstance<CompanionConfig>();w.Write(c.Enabled);w.Write(c.Pickup);w.Write(c.Combat);w.Write(c.Torches);w.Write(c.MiningAssist);w.Write(c.DiggingAssist);w.Write(c.Forestry);w.Write(c.AutoHeal);w.Write(c.Gardening);w.Write(c.CatchCritters);w.Write(c.Fishing);w.Write(c.DeliverRecoveryDrops);w.Write(c.AutoLogistics);w.Write(s.Bridges);w.Write(s.Ropes);w.Write(c.Chatter);w.Write(c.Emotes);CompanionStock.Config.Write(w);CompanionExclusions.Apply(c,s);CompanionExclusions.Write(w,s);}
    static void ReadPreferences(CompanionPlayer s,BinaryReader r){s.NetEnabled=r.ReadBoolean();s.Pickup=r.ReadBoolean();s.Combat=r.ReadBoolean();s.Torches=r.ReadBoolean();s.NetMining=r.ReadBoolean();s.NetDigging=r.ReadBoolean();s.NetForestry=r.ReadBoolean();s.NetAutoHeal=r.ReadBoolean();s.NetGarden=r.ReadBoolean();s.NetCatch=r.ReadBoolean();s.NetFishing=r.ReadBoolean();s.NetSupplies=r.ReadBoolean();s.AutoLogistics=r.ReadBoolean();s.Bridges=r.ReadBoolean();s.Ropes=r.ReadBoolean();s.NetChatter=r.ReadBoolean();s.NetEmotes=r.ReadBoolean();s.NetStock=CompanionStock.Read(r);CompanionExclusions.Read(r,s);s.Enabled=s.NetEnabled&&!s.Stowed;}
    internal static void FeedbackAt(byte kind,Vector2 center,int value=0,string key=""){if(!Server)return;var p=Packet(Feedback);p.Write(kind);p.Write(center.X);p.Write(center.Y);p.Write(value);p.Write(key);p.Send();}
    internal static void Transit(Vector2 center){var p=Packet(Effect);p.Write(center.X);p.Write(center.Y);p.Send();}
    static void Vitals(BinaryWriter w,CompanionVitals v){w.Write(v.Health);w.Write(v.MaxHealth);w.Write(v.Mana);w.Write(v.MaxMana);w.Write(v.Recovery);w.Write(v.RecoveryTotal);w.Write(v.PotionDelay);w.Write(v.ManaSickness);w.Write(v.ManaDelay);w.Write(v.Immunity);w.Write(v.LifeSteal);}
    static void Vitals(BinaryReader r,CompanionVitals v){v.Health=r.ReadInt32();v.MaxHealth=Math.Clamp(r.ReadInt32(),1,100000);v.Mana=r.ReadInt32();v.MaxMana=Math.Clamp(r.ReadInt32(),0,100000);v.Recovery=Math.Clamp(r.ReadInt32(),0,1800);v.RecoveryTotal=Math.Clamp(r.ReadInt32(),1,1800);v.PotionDelay=Math.Clamp(r.ReadInt32(),0,3600);v.ManaSickness=Math.Clamp(r.ReadInt32(),0,Player.manaSickTimeMax);v.ManaDelay=Math.Clamp(r.ReadInt32(),0,3600);v.Immunity=Math.Clamp(r.ReadInt32(),0,3600);float steal=r.ReadSingle();v.LifeSteal=float.IsFinite(steal)?Math.Clamp(steal,-100000,80):80;v.Health=Math.Clamp(v.Health,0,v.MaxHealth);v.Mana=Math.Clamp(v.Mana,0,v.MaxMana);}

    static ModPacket Packet(byte kind){var p=ModContent.GetInstance<PalicoCompanion>().GetPacket();p.Write(kind);return p;}
    static void Items(BinaryWriter w,Item[] items){w.Write((ushort)items.Length);foreach(var i in items)ItemIO.Send(i,w,true,true);}
    static Item[] Items(BinaryReader r,int limit){int n=r.ReadUInt16();if(n>limit)throw new InvalidDataException("Inventory capacity");var a=new Item[n];for(int i=0;i<n;i++)a[i]=ItemIO.Receive(r,true,true);return a;}
    internal static void Enter(CompanionPlayer s) {
        s.NetReady=false;s.NetTicks=0;
        if(Client)EnterResume(s);
    }
    internal static void Tick(CompanionPlayer s) {
        if(Client){
            if(s.Vitals.Immunity>0)s.Vitals.Immunity--;
            if(s.Player.whoAmI==Main.myPlayer&&!s.NetReady&&s.NetTicks++==0){
                var p=Packet(Init);p.Write(s.NetSession);p.Write(s.CatName);p.Write(s.HoodColor);p.Write(s.Bonded);p.Write(s.Stowed);p.Write(s.Enabled);p.Write(s.Introduced);
                Items(p,s.Bag);Items(p,s.Gear);Items(p,s.Dyes);s.Chests.Write(p);s.Dialogue.Write(p);Vitals(p,s.Vitals);WritePreferences(s,p);s.Training.Write(p);s.Loadouts.Write(p);p.Send();
            }
            if(s.Player.whoAmI==Main.myPlayer&&s.NetReady&&!CompanionSession.Closing&&++s.NetTicks%30==0&&s.Cat?.ModProjectile is CompanionCat visual){
                bool dark=CompanionLighting.WantsLight(s.Cat.Center,visual.Light.Holding);
                var p=Packet(Darkness);p.Write(dark);p.Send();
            }
            return;
        }
        if(!Server||!s.NetReady||!s.Player.active||s.NetLeaving)return;
        if(s.PetCooldown>0)s.PetCooldown--;if(s.NoticeTicks>0)s.NoticeTicks--;
        var cat=s.Cat?.ModProjectile as CompanionCat;
        s.Vitals.Sync(s.Player,s.Gear);s.Vitals.Tick(s.Cat?.velocity.X??0,s.Cat?.velocity.Y??0,cat?.Weapons.Busy==true&&cat.Weapons.Actor.HeldItem.mana>0,cat?.Animation.Motion==CatMotion.Rest,Main.expertMode,cat?.Weapons.Actor);
        if(s.Enabled&&!s.Player.dead&&s.Cat==null)
            Projectile.NewProjectile(s.Player.GetSource_Misc("PalicoNetwork"),s.Player.Center,Vector2.Zero,ModContent.ProjectileType<CompanionCat>(),0,0,s.Player.whoAmI);
        if(s.Enabled&&!s.Player.dead){s.Digging.Tick(s.Player,s);s.MiningHelper.Tick(s.Player,s);}
        if(++s.NetTicks%30==0){CompanionCoins.Maintain(s);Periodic(s);}
    }
    internal static void Send(CompanionPlayer s,int to=-1) {
        if(!Server||!s.NetReady)return;
        Cache(s);var p=Packet(Snapshot);WriteState(s,p);p.Send(to);if(to<0)equipmentState[s.Player.whoAmI]=EquipmentState(s);FullSnapshots++;
    }
    static void WriteState(CompanionPlayer s,BinaryWriter p){p.Write((byte)s.Player.whoAmI);p.Write(s.CatName);p.Write(s.HoodColor);p.Write(s.Bonded);p.Write(s.Stowed);p.Write(s.Enabled);p.Write(s.Paused);p.Write(s.Introduced);p.Write(s.Pickup);p.Write(s.Combat);p.Write(s.Torches);p.Write(s.Bridges);p.Write(s.Ropes);p.Write(s.AutoLogistics);p.Write(s.Vitals.EffectiveDefense);
        Vitals(p,s.Vitals);
        p.Write(s.Status??"");s.Chests.Write(p);Items(p,s.Bag);Items(p,s.Gear);Items(p,s.Dyes);s.Training.Write(p);s.Loadouts.Write(p);
        p.Write((ulong)Main.GameUpdateCount);p.Write(s.Enabled&&!s.Player.dead&&!s.NetLeaving&&s.Cat?.ModProjectile is CompanionCat cat?cat.NetworkInstance:0UL);s.Dialogue.Memories.Write(p);
    }
    internal static void Action(byte action){if(!Client)return;var p=Packet(Command);p.Write(action);p.Send();}
    static void ReadState(BinaryReader r,bool transaction=false,CompanionPlayer destination=null,bool apply=true){
            int owner=r.ReadByte();if(owner>=Main.maxPlayers)return;var s=destination??Main.player[owner].GetModPlayer<CompanionPlayer>();
            string name=r.ReadString();int hoodColor=CompanionPalette.Valid(r.ReadInt32());bool bonded=r.ReadBoolean(),stowed=r.ReadBoolean(),enabled=r.ReadBoolean(),paused=r.ReadBoolean();bool introduced=r.ReadBoolean();bool pickup=r.ReadBoolean(),combat=r.ReadBoolean(),torches=r.ReadBoolean(),bridges=r.ReadBoolean(),ropes=r.ReadBoolean(),logistics=r.ReadBoolean();int defense=r.ReadInt32();
            var vitals=new CompanionVitals();Vitals(r,vitals);string status=r.ReadString();var permissions=ChestPermissions.Read(r);
            var bag=Items(r,256);var gear=Items(r,16);var dyes=Items(r,16);var training=CompanionTraining.Read(r);var loadouts=CompanionLoadouts.Read(r);
            ulong rosterTime=r.ReadUInt64(),rosterToken=r.ReadUInt64();var memories=new CompanionMemories();memories.Read(r);if(apply&&destination==null)CompanionRoster.Confirm(owner,rosterToken,rosterTime);
            // Independent from optimistic inventory state: a pending drag must
            // not discard a real victory. TCP preserves snapshot order.
            if(apply)s.Dialogue.Memories=memories;
            if(!apply||owner==Main.myPlayer&&Pending&&!transaction)return;
            s.CatName=name;s.HoodColor=hoodColor;s.Bonded=bonded;s.Stowed=stowed;s.Enabled=enabled;s.Paused=paused;s.Status=status;
            s.Pickup=pickup;s.Combat=combat;s.Torches=torches;s.Bridges=bridges;s.Ropes=ropes;s.AutoLogistics=logistics;s.Vitals=vitals;s.Vitals.EffectiveDefense=defense;s.Introduced=introduced;
            s.Training=training;s.Loadouts=loadouts;s.Bag=bag;s.Gear=gear;s.Dyes=dyes;s.Chests=permissions;s.NetReady=true;
        }
    internal static void Receive(byte kind,BinaryReader r,int sender) {
        if(kind==ChestDiscoveryState){if(Client)DiscoveryChests.Apply(new Point(r.ReadInt16(),r.ReadInt16()),r.ReadBoolean());return;}
        if(kind==ControlResult&&Client){ReadControlResult(r);return;}
        if(kind==Pulse&&Client){ReadPulse(r);return;}
        if(kind==MaterialRequest&&Client){SupplyMaterial(r.ReadInt32());return;}
        if(kind==TrainingNotice&&Client){byte effect=r.ReadByte();int value=r.ReadInt32();CompanionTraining.ApplyOwner(Main.LocalPlayer.GetModPlayer<CompanionPlayer>(),effect,value);return;}
        if(kind==PickupNotice&&Client){var at=new Vector2(r.ReadSingle(),r.ReadSingle());var item=ItemIO.Receive(r,true,true);item.Center=at;if(float.IsFinite(at.X)&&float.IsFinite(at.Y)&&!item.IsAir)PopupText.NewText(PopupTextContext.RegularItemPickup,item,item.stack);return;}
        if(kind==Resume&&Client){ReadState(r,true);RetryRequest();return;}
        if(kind==CloseAck&&Client){ReadState(r,true);EndResume();CompanionSession.Complete();return;}
        if(kind==InstantTrail&&Client){ReadTrail(r);return;}
        if(kind==Feedback&&Client){byte what=r.ReadByte();var at=new Vector2(r.ReadSingle(),r.ReadSingle());int value=r.ReadInt32();string key=r.ReadString();if(float.IsFinite(at.X)&&float.IsFinite(at.Y))CompanionFeedback.Show(what,at,value,key);return;}
        if(kind==Effect&&Client){var point=new Vector2(r.ReadSingle(),r.ReadSingle());if(float.IsFinite(point.X)&&float.IsFinite(point.Y))CompanionTransitEffects.At(point);return;}
        if(kind==Speech&&Client){
            int owner=r.ReadByte();string line=r.ReadString(),context=r.ReadString();
            if(owner==Main.myPlayer){var s=Main.LocalPlayer.GetModPlayer<CompanionPlayer>();s.Expression.ReceiveSpeech(s,line,context);}return;
        }
        if(kind==BulkResult&&Client){ReadBulkResult(r);return;}
        if(kind==QuickTrashResult&&Client){ReadTrashResult(r);return;}
        if(kind==SortResult&&Client){ReadSortResult(r);return;}
        if(kind==Snapshot&&Client){ReadState(r);return;}
        if(kind==SlotResult&&Client){ReadSlotResult(r);return;}
        if(!Server||sender<0||sender>=Main.maxPlayers)return;
        var state=Main.player[sender].GetModPlayer<CompanionPlayer>();
        if(kind==Init){
            string session=r.ReadString();if(!Guid.TryParseExact(session,"N",out _))throw new InvalidDataException("Invalid session");
            string name=r.ReadString();int hoodColor=CompanionPalette.Valid(r.ReadInt32());bool bonded=r.ReadBoolean(),stowed=r.ReadBoolean(),enabled=r.ReadBoolean();bool introduced=r.ReadBoolean();
            var bag=Items(r,256);var gear=Items(r,16);var dyes=Items(r,16);
            var permissions=ChestPermissions.Read(r);
            var history=new CompanionDialogue();history.Read(r);var savedVitals=new CompanionVitals();Vitals(r,savedVitals);ReadPreferences(state,r);var training=CompanionTraining.Read(r);var loadouts=CompanionLoadouts.Read(r);
            if(state.NetReady||bag.Length<24||gear.Length<4||dyes.Length<1)return;
            state.NetSession=session;if(Restore(state)){var resume=Packet(Resume);WriteState(state,resume);resume.Send(sender);Send(state);return;}
            CompanionNaming.Rename(state,name);state.HoodColor=hoodColor;state.Bonded=bonded;state.Introduced=introduced;state.Stowed=stowed;state.Enabled=enabled&&!stowed;state.Paused=false;
            state.Dialogue=history;state.Training=training;state.Loadouts=loadouts;state.NetLeaving=false;state.Chests=permissions;state.Bag=bag;state.Gear=gear;state.Dyes=dyes;state.NetReady=true;state.Vitals=savedVitals;state.Enabled=state.NetEnabled&&!stowed;state.Vitals.Sync(state.Player,gear);var ready=Packet(Resume);WriteState(state,ready);ready.Send(sender);Send(state);return;
        }
        // A light report may already be queued when the final save handshake
        // freezes the cat. Consume its payload even when ignoring that report.
        if(kind==Darkness){bool dark=r.ReadBoolean();if(state.NetReady&&!state.NetLeaving)state.NetDark=dark;
return;}
        if(!state.NetReady)return;
        if(kind==CloseRequest){state.NetLeaving=true;var ack=Packet(CloseAck);WriteState(state,ack);ack.Send(sender);return;}
        if(state.NetLeaving)return;
        if(kind==ControlRequest){ReadControl(state,r);return;}
        if(kind==QuickTrash){ReadTrash(state,r);return;}
        if(kind==TrainingUse){byte skill=r.ReadByte();if(skill is 1 or 6 && state.Training.Use(state,skill,CompanionTraining.CooldownSeconds(skill),state.Player.Center))state.Expression.GearUsed(state,skill);Send(state);return;}
        if(kind==ExternalStorageAccess){var point=new Point(r.ReadInt32(),r.ReadInt32());bool grant=r.ReadBoolean();ExternalPermission(state,point,grant);return;}
        if(kind==ChestAccess){int index=r.ReadInt32();bool grant=r.ReadBoolean();if(index==state.Player.chest&&index>=0&&index<Main.maxChests&&Main.chest[index] is {} chest&&Logistics.At(chest.x,chest.y)==chest&&Vector2.DistanceSquared(state.Player.Center,Logistics.Center(chest))<240*240){if(grant)state.Chests.Grant(chest.x,chest.y);else state.Chests.Revoke(chest.x,chest.y);state.Storage.Cancel();Send(state);}return;}
        if(kind==DiscoveryBubble){state.Expression.DiscoveryBubble(state,r.ReadByte());return;}
        if(kind==Preferences){ReadPreferences(state,r);
Send(state);return;}
        if(kind==Identity){string name=r.ReadString();int color=CompanionPalette.Valid(r.ReadInt32());bool bonded=r.ReadBoolean();CompanionNaming.Rename(state,name);state.HoodColor=color;state.Bonded=bonded;Send(state);return;}
        if(kind==BuildStroke){int x=r.ReadInt32(),y=r.ReadInt32(),type=r.ReadInt32();
            if(WorldGen.InWorld(x,y,10)&&Vector2.DistanceSquared(state.Player.Center,new Vector2(x*16+8,y*16+8))<240*240)state.Construction.Observe(state.Player,new Point(x,y),type);
            return;
        }
        if(kind==Chop){int x=r.ReadInt32(),y=r.ReadInt32();
            if(state.Player.HeldItem.axe>0&&WorldGen.InWorld(x,y,10)&&Vector2.DistanceSquared(state.Player.Center,new Vector2(x*16+8,y*16+8))<240*240&&TreeAssist.FindRoot(x,y,out var root))state.Trees.Assist(state.Player,root);
            return;
        }
        if(kind==Dig){int x=r.ReadInt32(),y=r.ReadInt32(),type=r.ReadInt32();var aim=new Vector2(r.ReadSingle(),r.ReadSingle());
            if(WorldGen.InWorld(x,y,10)&&float.IsFinite(aim.X)&&float.IsFinite(aim.Y)&&DiggingIntent.Allowed(type)&&state.Player.HeldItem.pick>0&&Vector2.DistanceSquared(state.Player.Center,new Vector2(x*16+8,y*16+8))<240*240)
                state.Digging.Observe(state.Player,new Point(x,y),type,aim);
            return;
        }
        if(kind==Command){byte action=r.ReadByte();
switch(action){
            case 0:CompanionLife.Toggle(state);break;case 1:state.Recall();break;case 2:state.Pet();break;
            case 3:state.Paused=!state.Paused;break;
        }Send(state);return;}
        if(kind==Bulk){ReadBulk(state,r,sender);return;}
        if(kind==Slot){ReadSlot(state,r,sender);return;}
        if(kind==SortRequest){ReadSort(state,r,sender);return;}

    }
}
