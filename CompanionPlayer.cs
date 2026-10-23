using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using PalicoCompanion.Systems;
namespace PalicoCompanion;
public sealed class CompanionPlayer : ModPlayer
{
    internal readonly System.Collections.Generic.HashSet<int> ExcludedMining=new(),ExcludedPickup=new();
    internal string NetSession="";internal TagCompound NetResume=new();
    public override void PlayerDisconnect()=>CompanionNetwork.Cache(this);
    public override void Kill(double damage,int hitDirection,bool pvp,Terraria.DataStructures.PlayerDeathReason damageSource){
        if(!CompanionNetwork.Client&&Enabled&&!Stowed&&!Vitals.Downed&&Cat is {} cat)Expression.OwnerDied(this,cat);
    }
    internal CompanionProfile NetworkProfile;
    internal bool HasNetworkProfile;
    internal bool NetGarden=true,NetCatch=true,NetFishing=true,NetSupplies=true,NetDark;
    internal CompanionStock NetStock=CompanionStock.Default;
    internal bool NetEnabled=true,NetMining=true,NetDigging=true,NetForestry=true,NetAutoHeal=true;
    internal bool NetChatter=true,NetEmotes=true;
    internal ulong NetMaterialAt;
    internal bool NetLeaving;internal bool NetReady;internal int NetTicks,NetCommand;
    internal int NetSpeechCount;internal string NetLastSpeech="";
    public override void SyncPlayer(int toWho,int fromWho,bool newPlayer){CompanionProfileNet.Send(this,toWho,fromWho);CompanionNetwork.Send(this,toWho);}
    public override void CopyClientState(ModPlayer targetCopy) {
        var copy=(CompanionPlayer)targetCopy;
        copy.NetworkProfile=CompanionProfile.Capture(this);
    }
    public override void SendClientChanges(ModPlayer clientPlayer) {
        if(CompanionProfile.Capture(this)!=((CompanionPlayer)clientPlayer).NetworkProfile)
            CompanionProfileNet.Send(this);
    }
    public int HoodColor=CompanionPalette.Default;
    public Item[] Dyes = InventoryTransfer.Empty(1);
    public Item[] Gear = InventoryTransfer.Empty(4);
    public CompanionVitals Vitals = new();
    public CompanionTraining Training = new();
    internal CompanionLoadouts Loadouts=new();
    internal CompanionDirective Directive=new();
    internal System.Collections.Generic.List<Item> DeliveryRemainder=new();
    internal int DeliveryTicks;
    public Item[] Bag = InventoryTransfer.Empty(24);
    public bool Enabled, Paused, Pickup=true, Combat=true, Torches=true, Bridges, Ropes;
    public bool EnvironmentPaused;
    public bool AutoLogistics=true;
    public AutomaticLogistics Storage=new();
    public CompanionExpression Expression=new();
    public CompanionDialogue Dialogue=new();
    public CompanionDiscovery Discovery=new();
    public ConstructionAssist Construction=new();
    public TravelAssist Travel=new();
    int maintenanceTick;
    public bool PanelOpen;
    public bool Bonded, Stowed, Introduced; public Vector2 RecoveryPosition; public string RecoveryWorld=""; int meetingTicks; bool meetingShown;
    public Vector2 WorkAnchor=>Player.Center;
    public string Notice=""; public int NoticeTicks;
    public void Report(string text) {Notice=text;NoticeTicks=300;}
    public WorkOrders Order=new();
    public FieldAssist Mining=new();
    public MiningIntent MiningHelper=new();
    public DiggingIntent Digging=new();
    public GardenAssist Garden=new();
    public TreeAssist Trees=new();
    public string CatName=CompanionLife.L("DefaultName"), Status="Ready";
    public int Experience, Fur, PetCooldown;
    public Vector2 Home;
    public string HomeWorld="";
    public ChestPermissions Chests=new();
    public override void SaveData(TagCompound tag) {
        tag["deliveryRemainder"]=DeliveryRemainder.Select(ItemIO.Save).ToList();
        tag["training"]=Training.Save();tag["loadouts"]=Loadouts.SaveData();
        tag["networkSession"]=NetSession;tag["networkResume"]=CompanionNetwork.SaveResume(this);
        tag["hoodColor"]=HoodColor;tag["dye"]=ItemIO.Save(Dyes[0]);tag["introduced"]=Introduced;
        tag["bonded"]=Bonded;tag["stowed"]=Stowed;tag["recoveryTotal"]=Vitals.RecoveryTotal;tag["recoveryPosition"]=RecoveryPosition;tag["recoveryWorld"]=RecoveryWorld;
        tag["lifeSteal"]=Vitals.LifeSteal;tag["manaSickness"]=Vitals.ManaSickness;tag["manaDelay"]=Vitals.ManaDelay;
        tag["dialogue"]=Dialogue.Save();
        tag["discoveries"]=Discovery.Save();
        tag["potionDelay"]=Vitals.PotionDelay; tag["autoLogistics"]=AutoLogistics; tag["schema"]=5; tag["maxHealth"]=Vitals.MaxHealth;tag["maxMana"]=Vitals.MaxMana;tag["mana"]=Vitals.Mana; tag["gear"]=Gear.Select(ItemIO.Save).ToList(); tag["health"]=Vitals.Health;tag["recovery"]=Vitals.Recovery; tag["bag"]=Bag.Select(ItemIO.Save).ToList();
        tag["environmentPaused"]=EnvironmentPaused;
        tag["enabled"]=Enabled; tag["paused"]=Paused; tag["name"]=CatName;
        tag["pickup"]=Pickup; tag["combat"]=Combat; tag["torches"]=Torches; tag["bridges"]=Bridges; tag["ropes"]=Ropes;
        tag["xp"]=Experience; tag["fur"]=Fur; tag["home"]=Home; tag["homeWorld"]=HomeWorld;
        tag["chestPermissions"]=Chests.Save();
    }
    public override void LoadData(TagCompound tag) {
        DeliveryRemainder=tag.GetList<TagCompound>("deliveryRemainder").Select(ItemIO.Load).ToList();
        Training=new();if(tag.ContainsKey("training"))Training.Load(tag.GetCompound("training"));
        Loadouts=new();if(tag.ContainsKey("loadouts"))Loadouts.LoadData(tag.GetCompound("loadouts"));
        NetSession=tag.GetString("networkSession");NetResume=tag.ContainsKey("networkResume")?tag.GetCompound("networkResume"):new();
        HoodColor=tag.ContainsKey("hoodColor")?CompanionPalette.Valid(tag.GetInt("hoodColor")):CompanionPalette.Default;
        Dyes=InventoryTransfer.Empty(1);if(tag.ContainsKey("dye"))Dyes[0]=ItemIO.Load(tag.GetCompound("dye"));
        Bonded=tag.ContainsKey("bonded")?tag.GetBool("bonded"):tag.ContainsKey("schema");Stowed=tag.GetBool("stowed");
        Introduced=tag.ContainsKey("introduced")?tag.GetBool("introduced"):Bonded;
        RecoveryPosition=tag.Get<Vector2>("recoveryPosition");RecoveryWorld=tag.GetString("recoveryWorld");
        Dialogue=new();if(tag.ContainsKey("dialogue"))Dialogue.Load(tag.GetCompound("dialogue"));
        Discovery=new();if(tag.ContainsKey("discoveries"))Discovery.Load(tag.GetCompound("discoveries"));
        AutoLogistics=!tag.ContainsKey("autoLogistics")||tag.GetBool("autoLogistics");
        var gear=tag.GetList<TagCompound>("gear");
        Gear=InventoryTransfer.Empty(Math.Max(4,gear.Count));
        for(int i=0;i<gear.Count;i++)Gear[i]=ItemIO.Load(gear[i]);
        int savedMax=tag.ContainsKey("maxHealth")?Math.Clamp(tag.GetInt("maxHealth"),1,100000):CompanionVitals.LegacyMaxHealth;
        int savedMana=tag.ContainsKey("maxMana")?Math.Clamp(tag.GetInt("maxMana"),0,100000):20;
        Vitals=new() {PotionDelay=Math.Clamp(tag.GetInt("potionDelay"),0,Item.potionDelay),MaxHealth=savedMax,MaxMana=savedMana,Mana=tag.ContainsKey("mana")?Math.Clamp(tag.GetInt("mana"),0,savedMana):savedMana,
            Health=tag.ContainsKey("health")?Math.Clamp(tag.GetInt("health"),0,savedMax):savedMax,Recovery=Math.Clamp(tag.GetInt("recovery"),0,CompanionVitals.RecoveryTime)};
        Vitals.RecoveryTotal=Math.Clamp(tag.ContainsKey("recoveryTotal")?tag.GetInt("recoveryTotal"):Math.Max(Vitals.Recovery,600),1,CompanionVitals.RecoveryTime);
        if(Vitals.Health==0 && !Vitals.Downed)Vitals.Recovery=CompanionVitals.RecoveryTime;
        float lifeSteal=tag.ContainsKey("lifeSteal")?tag.GetFloat("lifeSteal"):80;Vitals.LifeSteal=float.IsFinite(lifeSteal)?Math.Clamp(lifeSteal,-100000,80):80;
        Vitals.ManaSickness=Math.Clamp(tag.GetInt("manaSickness"),0,Terraria.Player.manaSickTimeMax);Vitals.ManaDelay=Math.Clamp(tag.GetInt("manaDelay"),0,3600);
        var items=tag.GetList<TagCompound>("bag");
        // Preserve every saved slot if capacity ever shrinks in a future version.
        Bag=InventoryTransfer.Empty(Math.Max(24,items.Count));
        for(int i=0;i<items.Count;i++) Bag[i]=ItemIO.Load(items[i]);
        EnvironmentPaused=tag.GetBool("environmentPaused");
        Enabled=tag.GetBool("enabled"); Paused=tag.GetBool("paused");
        CatName=tag.ContainsKey("name")?tag.GetString("name"):CompanionLife.L("DefaultName");
        Pickup=!tag.ContainsKey("pickup")||tag.GetBool("pickup"); Combat=!tag.ContainsKey("combat")||tag.GetBool("combat");
        Torches=!tag.ContainsKey("torches")||tag.GetBool("torches"); Bridges=tag.GetBool("bridges"); Ropes=tag.GetBool("ropes");
        Experience=tag.GetInt("xp"); Fur=tag.GetInt("fur"); Home=tag.Get<Vector2>("home"); HomeWorld=tag.GetString("homeWorld");
        Chests=new();Chests.Load(tag);
    }
    public override void OnEnterWorld() {
        CompanionNetwork.Enter(this);
        var config=ModContent.GetInstance<CompanionConfig>();
        config.Bridges=Bridges;config.Ropes=Ropes;config.Apply(this);
        Storage=new();Expression=new();Travel=new();Construction=new();maintenanceTick=0;
        Discovery.ResetSession();
        Order=new();Directive=new();
        Mining=new();MiningHelper=new();Digging=new();Garden=new();Trees=new();
        PanelOpen=false;meetingTicks=0;meetingShown=false;

    }
    public override void ProcessTriggers(TriggersSet triggersSet) {
        CompanionControlUI.Input(this);
        if(Main.inFancyUI||Terraria.GameInput.PlayerInput.WritingText) return;
        if(PalicoCompanion.PanelKey.JustPressed&&!Main.inFancyUI&&!Main.mapFullscreen) CompanionUIFlow.Toggle(this);
        if(PalicoCompanion.EnvironmentKey.JustPressed&&!Main.mapFullscreen)CompanionEnvironment.RequestToggle(this);
        if(CompanionNetwork.Client){if(PalicoCompanion.RecallKey.JustPressed)CompanionNetwork.Action(1);return;}
        if(PalicoCompanion.RecallKey.JustPressed) Recall();
    }
    public override bool PreItemCheck() {
        // Interface input belongs to the human, never the isolated weapon actor.
        if(ReferenceEquals(Player,CompanionNativeUse.Current?.Actor)||ReferenceEquals(Player,CompanionFishing.Current?.Actor))return true;
        if(Main.dedServ||Player.whoAmI!=Main.myPlayer)return true;
        if(CompanionControlUI.Aiming)return false;
        if(CompanionNetwork.Client&&CompanionNetwork.BlocksItemUse)return false;
        if(CompanionInteraction.Hovered&&Main.mouseRight||CompanionStatusBar.BlocksInput)return false;
        Digging.ObserveConstruction(this);
        if(Main.netMode==NetmodeID.SinglePlayer && Enabled && !Paused && !EnvironmentPaused && !Vitals.Downed && ModContent.GetInstance<CompanionConfig>().MiningAssist)
            MiningHelper.Observe(Player,new Point(Terraria.Player.tileTargetX,Terraria.Player.tileTargetY));
        return true;
    }
    public override void PostUpdate() {
        LegacyTrainingMigration.Tick(this);
        CompanionRestock.ExpireEventResources(this);
        CompanionDelivery.Tick(this);
        if(Main.netMode!=NetmodeID.SinglePlayer){CompanionNetwork.Tick(this);
            if(CompanionNetwork.Client&&Player.whoAmI==Main.myPlayer){if(NoticeTicks>0)NoticeTicks--;if(NetReady&&!Bonded&&!meetingShown&&Enabled&&!Player.dead&&++meetingTicks>=180&&!Main.inFancyUI){meetingShown=true;CompanionNaming.Open(this,true);}}
            return;}
        if(Main.netMode==NetmodeID.SinglePlayer){
            Vitals.Sync(Player,Gear);
            var companion=Cat?.ModProjectile as CompanionCat;
            Vitals.Tick(Cat?.velocity.X??0,Cat?.velocity.Y??0,
                companion?.Weapons.Busy==true&&companion.Weapons.Actor.HeldItem.mana>0,
                companion?.Animation.Motion==CatMotion.Rest,Main.expertMode,companion?.Weapons.Actor);
            if(++maintenanceTick%30==0)CompanionCoins.Maintain(this);
        }
        if(PetCooldown>0) PetCooldown--;
        if(NoticeTicks>0) NoticeTicks--;
        if(Main.netMode==NetmodeID.SinglePlayer&&!Bonded&&!meetingShown&&Enabled&&!Player.dead&&++meetingTicks>=180&&!Terraria.GameInput.PlayerInput.WritingText&&!Main.inFancyUI&&!Main.gameMenu&&!CompanionThreat.WorkDanger(Player,Player.Center)){meetingShown=true;CompanionNaming.Open(this,true);}
        if(Main.netMode!=NetmodeID.SinglePlayer || !Enabled || Player.dead) {Digging.Clear(this);return;}
        // Flight is a means of travel, not a reason to erase a new owner stroke.
        // Distant return still cancels old work in CompanionCat.CancelTransitWork.
        Digging.Tick(Player,this);
        MiningHelper.Tick(Player,this);
        if(Player.ownedProjectileCounts[ModContent.ProjectileType<CompanionCat>()]==0)
            Projectile.NewProjectile(Player.GetSource_Misc("PalicoCompanion"), Vitals.Downed&&RecoveryWorld==CompanionLife.World?RecoveryPosition:Player.Center, Vector2.Zero, ModContent.ProjectileType<CompanionCat>(), 0, 0, Player.whoAmI);
    }
    public Projectile Cat => Main.projectile.FirstOrDefault(p=>p.active && p.ModProjectile is CompanionCat c && c.OwnerIndex==Player.whoAmI);
    public void Recall() {if(CompanionNetwork.Client){CompanionNetwork.Action(1);return;} if(Vitals.Downed){Report(CompanionText.MessageFull("Mods.PalicoCompanion.UI.RecoveryWait"));return;}CompanionLife.Follow(this);if(Cat is { } cat) { CompanionTransitEffects.Move(cat,Player.Center);cat.netUpdate=true; Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.Following");Expression.Say(this,cat,"Recall",Terraria.GameContent.UI.EmoteID.EmoteConfused); } }
    // Only explicit, visible companion inventory sessions redirect shift-click.
    public override bool ShiftClickSlot(Item[] inventory,int context,int slot) {
        if(!PanelOpen || slot<0 || slot>=inventory.Length)return false;
        if(CompanionNetwork.Client){if(ReferenceEquals(inventory,Player.inventory)&&slot<50){CompanionNetwork.Transfer(this,true,slot,(byte)(TrainingCatalog.Id(inventory[slot]) is >=0 and <13?3:0));return true;}return false;}
        bool player=ReferenceEquals(inventory,Player.inventory)&&slot<50&&context==Terraria.UI.ItemSlot.Context.InventoryItem;
        bool chest=Player.chest>=0&&Main.chest[Player.chest] is {} c&&ReferenceEquals(inventory,c.item);
        if(!player&&!chest)return false;
        CompanionInventoryOperations.Deposit(this,new[]{inventory[slot]},equip:true).Present(this,true);
        return true; // Full bag / favorite must not fall through to trash or another storage.
    }
    public void QuickStack() {if(CompanionNetwork.Client){CompanionNetwork.Transfer(this,true);return;}
        CompanionInventoryOperations.Deposit(this,Player.inventory.Take(50).ToArray(),matchingOnly:true).Present(this,true);
    }
    public void GiveHeld() {
        if(Player.HeldItem.favorited) { Status="Unfavorite the item first"; return; }
        Status=$"Stored {InventoryTransfer.Move(Player.HeldItem,Bag)} items";
    }
    public void AuthorizeChest() {
        if(CompanionNetwork.Client){CompanionNetwork.ChestPermission(true);return;}
        int index=Player.chest;
        if(Main.netMode==NetmodeID.MultiplayerClient||index<0 || index>=Main.maxChests || Main.chest[index] is not {} chest||Logistics.At(chest.x,chest.y)!=chest) {Status="Open a normal chest to authorize it";return;}
        Chests.Grant(chest.x,chest.y);Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.StorageLinked");Storage.Cancel();
    }
    public void UnlinkChest(){
        if(CompanionNetwork.Client){CompanionNetwork.ChestPermission(false);return;}
        if(Main.netMode==NetmodeID.MultiplayerClient||Player.chest<0||Player.chest>=Main.maxChests||Main.chest[Player.chest] is not {} chest)return;
        Chests.Revoke(chest.x,chest.y);Storage.Cancel();Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.StorageUnlinked");
    }
    public void TakeAll(){if(CompanionNetwork.Client){CompanionNetwork.Transfer(this,false);return;}CompanionInventoryOperations.Take(this,Bag).Present(this,true);}
    public void Take(int slot) {if(CompanionNetwork.Client){CompanionNetwork.Transfer(this,false,slot);return;}
        if(slot<0||slot>=Bag.Length)return;
        CompanionInventoryOperations.Take(this,Bag,slot).Present(this,true);
    }
    public void Pet() {if(CompanionNetwork.Client){CompanionNetwork.Action(2);return;} if(PetCooldown>0||Stowed||Vitals.Downed) return; if(Cat is not {} cat || Vector2.Distance(cat.Center,Player.Center)>100) { Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.PetCloser");return; } Experience++; PetCooldown=600; cat.ai[1]=90; Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.PetHappy");if(cat.ModProjectile is CompanionCat companion)companion.Feedback.PetSound(cat);Expression.Say(this,cat,"Pet",Terraria.GameContent.UI.EmoteID.EmotionLove); }
}
