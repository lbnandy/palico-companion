using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;
using Terraria.GameContent.UI;
namespace PalicoCompanion.Systems;

public sealed partial class CompanionExpression {
    int tick,flavorAt,bubbleAt,bubbleUntil;
    bool wasDown;
    int ownedBubble=-1;
    int bubblePriority;
    bool ownerReturning;
    Projectile visualAnchor;
    int previousOwnerHealth=-1;
    bool bossNearby,workingLast,greeted;
    bool memoryRest=true;
    readonly CompanionBossEncounter bosses=new();
    readonly CompanionEventReactions worldEvents=new();
    readonly CompanionExtraReactions extraReactions=new();
    bool eventNearby;
    readonly bool greeting;
    public CompanionExpression(bool greeting=true){this.greeting=greeting;}
    readonly Dictionary<string,int> nextEvent=new();
    readonly Dictionary<string,int> nextBubble=new();
    readonly CompanionSpeechPacing pacing=new();
    public int Chats {get;private set;}
    internal void AdvancePresentation()=>tick++;
    public int Bubbles {get;private set;}
    public string LastChatKey {get;private set;}="";
    internal string LastLineKey {get;private set;}="";
    public string FirstChatKey {get;private set;}="";
    enum Channel { Flavor, Work, Discovery, Interaction, Important }
    static int BubblePriority(Channel channel)=>channel switch {Channel.Important=>3,Channel.Interaction=>2,Channel.Discovery=>1,_=>0};
    internal void ClearPendingWork(){pendingGear=null;equipmentPending=0;}
    internal void OwnerDied(CompanionPlayer s,Projectile cat){
        ClearPendingWork();ownerReturning=s.Introduced;
        TrySay(s,cat,"OwnerHurt",EmoteID.EmoteSadness,variant:"Death");
    }
    // Dialogue is a personal report. Remote cat visuals must not advance another
    // player's introduction, dialogue history, pending reports or text cooldowns.
    internal static bool IsLocalListener(CompanionPlayer s) =>
        !Main.dedServ && Main.netMode!=NetmodeID.Server && s?.Player is {} owner &&
        owner.whoAmI==Main.myPlayer;
    static Channel Kind(string key)=>key switch {
        "Boss" or "BossVictory" or "FirstMeet" or "Downed" or "Recovered" or "Heal" or "ManaPotion" or "NoAmmo" or "NoMana"=>Channel.Important,
        "Pet" or "Recall"=>Channel.Interaction,
        "Ore" or "Tree" or "Build" or "Loot" or "FishCaught" or "Delivered" or "RouteRecovered"=>Channel.Work,
        _ when key.StartsWith("Found")=>Channel.Discovery,
        _=>Channel.Flavor
    };
    static int RepeatDelay(string key)=>Kind(key) switch {
        Channel.Discovery=>0, // Discovery owns object/world identity and persistence.
        Channel.Work=>1800,
        Channel.Interaction=>180,
        Channel.Important=>key is "NoAmmo" or "NoMana" or "ManaPotion" or "Heal"?3600:120,
        _=>3600
    };
    public void Tick(CompanionPlayer s,Projectile cat) {
        tick++;
        if(CompanionRestVisual.Hidden(s.Vitals)&&ownedBubble>=0){
            var bubble=EmoteBubble.GetExistingEmoteBubble(ownedBubble);
            if(bubble!=null)bubble.lifeTime=0;
            ownedBubble=-1;visualAnchor=null;
        }
        if(visualAnchor!=null) {
            UpdateAnchor(cat);
            if(tick>=bubbleUntil)visualAnchor=null;
        }
        if(s.Vitals.Downed||s.Player.dead||s.Paused){ClearPendingWork();}
        if(s.Vitals.Downed&&!wasDown)Say(s,cat,"Downed",EmoteID.EmoteSadness);
        if(!s.Vitals.Downed&&wasDown)Say(s,cat,"Recovered",EmoteID.EmoteHappiness);
        wasDown=s.Vitals.Downed;
        if(!s.Paused&&!s.Vitals.Downed) {
            if(ownerReturning&&!s.Player.dead&&TrySay(s,cat,"Welcome",EmoteID.EmoteHappiness,variant:"Revived")){
                ownerReturning=false;greeted=true;previousOwnerHealth=s.Player.statLife;
            }
            // Wait for naming to close. Only a line actually shown completes the
            // introduction; its flag follows the character across worlds.
            if((IsLocalListener(s)||CompanionNetwork.Server)&&greeting&&!greeted&&tick>=120&&s.Bonded&&!Main.inFancyUI&&ChatEnabled(s)) {
                string key=s.Introduced?"Welcome":"FirstMeet";
                int before=Chats;TrySay(s,cat,key,EmoteID.EmoteHappiness);
                if(Chats>before){s.Introduced=true;greeted=true;}
            }
            if(tick%30==0){
                bossNearby=bosses.Tick(this,s,cat,tick);
                if(!CompanionNetwork.Client){
                    eventNearby=worldEvents.Tick(this,s,cat,tick,bossNearby);
                    eventNearby|=extraReactions.Tick(this,s,cat,tick,bossNearby||eventNearby);
                }
            }
            SpeakGear(s,cat);
            // If help already arrived this update, do not first ask whether the
            // owner is hurt and then announce that same help a moment later.
            if(previousOwnerHealth>0&&previousOwnerHealth-s.Player.statLife>=System.Math.Max(10,s.Player.statLifeMax2/10))
                Say(s,cat,"OwnerHurt",EmoteID.ItemLifePotion);
            bool working=s.Mining.Active||s.Trees.Active||s.Garden.Active||(cat.ModProjectile is CompanionCat worker&&(worker.Catching.Active||worker.Fishing.Active));
            if(working&&!workingLast)Say(s,cat,"Working",EmoteID.EmoteNote);
            workingLast=working;
        }
        previousOwnerHealth=s.Player.statLife;
        // Routine work is sampled when it happens, never replayed as a backlog.
        // Discoveries keep their existing rescan/remember-on-delivery behaviour.
        ObserveEquipment(s,cat);
        if(!s.Paused&&!s.Vitals.Downed&&!bossNearby&&!eventNearby&&cat.ModProjectile is CompanionCat c&&c.Animation.Motion==CatMotion.Rest&&tick%1800==0&&!CompanionThreat.WorkDanger(s.Player,cat.Center))
            Rest(s,cat);
    }
    internal bool Rest(CompanionPlayer s,Projectile cat){
        // Share the existing idle chatter budget; never consume a recollection
        // when text is muted or delivery is suppressed by another event.
        int memory=memoryRest&&ChatEnabled(s)?s.Dialogue.Memories.Recollection():-1;
        bool said=TrySay(s,cat,"Rest",EmoteID.EmoteSleep,
            context:memory>=0?CompanionMemories.NameKey(memory):null,
            variant:memory>=0?"Memory":CompanionSpeechContext.RestPlace(s.Player));
        if(said&&ChatEnabled(s)){
            memoryRest=!memoryRest;
            if(memory>=0){s.Dialogue.Memories.Recall(memory);CompanionNetwork.Send(s,s.Player.whoAmI);}
        }
        return said;
    }
    internal static bool ChatEnabled(CompanionPlayer s)=>CompanionNetwork.Server?s.NetChatter:ModContent.GetInstance<CompanionConfig>().Chatter;
    internal static bool EmotesEnabled(CompanionPlayer s)=>CompanionNetwork.Server?s.NetEmotes:ModContent.GetInstance<CompanionConfig>().Emotes;
    // Discoveries are observed privately on the owner's client. Only their native
    // world bubble is requested; no text or arbitrary emote is relayed.
    internal void DiscoveryBubble(CompanionPlayer s,int emote){
        if(!CompanionNetwork.Server||!s.NetEmotes||!s.Enabled||s.Paused||s.Vitals.Downed||tick<bubbleAt&&bubblePriority>=1||s.Cat is not {} cat)return;
        if(emote!=EmoteID.EmoteConfused&&emote!=EmoteID.EmoteHappiness)return;
        StartBubble(cat,emote,1);
    }
    void StartBubble(Projectile cat,int emote,int priority){
        // Vanilla only retires overlapping Player bubbles automatically; our
        // anchor is a projectile. Finish its old bubble through the same native
        // six-frame closing animation and SyncEmoteBubble packet.
        var old=EmoteBubble.GetExistingEmoteBubble(ownedBubble);
        if(old!=null&&old.lifeTime>6){
            old.lifeTime=6;
            if(CompanionNetwork.Server){var anchor=EmoteBubble.SerializeNetAnchor(old.anchor);NetMessage.SendData(MessageID.SyncEmoteBubble,-1,-1,null,ownedBubble,anchor.Item1,anchor.Item2,6,old.emote,old.metadata);}
        }
        if(!CompanionNetwork.Server){visualAnchor=new Projectile();UpdateAnchor(cat);}else visualAnchor=null;
        ownedBubble=EmoteBubble.NewBubble(emote,new WorldUIAnchor(CompanionNetwork.Server?cat:visualAnchor),120);
        bubbleAt=tick+180;bubbleUntil=tick+120;bubblePriority=priority;Bubbles++;
    }
    void UpdateAnchor(Projectile cat) {
        // Use the native Entity anchor formula and flip, but measure the drawn hood
        // rather than the deliberately smaller 24x28 collision box. This proxy is
        // never spawned and is only used by a single-player EmoteBubble.
        visualAnchor.width=cat.width;visualAnchor.height=CompanionSpriteLayout.StandingHeight;
        visualAnchor.Bottom=cat.Bottom+new Vector2(0,cat.gfxOffY);
        visualAnchor.direction=cat.direction;
    }
    public void Say(CompanionPlayer s,Projectile cat,string key,int emote,bool chat=true)=>TrySay(s,cat,key,emote,chat);
    public bool TrySay(CompanionPlayer s,Projectile cat,string key,int emote,bool chat=true,bool requireChat=false,string context=null,string variant=null) {
        if(cat==null||CompanionNetwork.Client&&!IsLocalListener(s)||!s.Enabled)return false;
        bool chatter=ChatEnabled(s),emotes=EmotesEnabled(s);
        bool localChat=IsLocalListener(s)||CompanionNetwork.Server;
        // Keep the first spoken line a proper introduction, not incidental loot.
        if(localChat&&chat&&chatter&&greeting&&!s.Introduced&&key!="FirstMeet")return false;
        bool lifeEvent=key=="OwnerHurt"&&variant=="Death"||key=="Welcome"&&variant=="Revived";
        bool bagFull=key=="Working"&&variant=="BagFull";
        bool gear=variant!=null&&variant.StartsWith("Gear");
        var priority=CompanionSpeechPacing.For(key,variant);
        var channel=lifeEvent||bagFull||priority==CompanionSpeechPacing.Priority.Support?Channel.Important:Kind(key);
        string repeatKey=key is "Boss" or "BossVictory"?CompanionSpeechContext.Pool(key,variant)+":"+context:lifeEvent||bagFull||gear?key+variant:key;
        int repeatDelay=gear?3600:bagFull?3600:lifeEvent?120:RepeatDelay(key);
        // Boss Rush still records every genuine victory; text gets one shared
        // budget so rapid stages do not fill the owner's chat.
        if(key=="BossVictory"&&CompanionSpeechContent.BossMarathon?.Invoke()==true){repeatKey="BossMarathonVictory";repeatDelay=900;}
        bool repeatReady=!nextEvent.TryGetValue(repeatKey,out int next)||tick>=next;
        bool channelReady=gear||channel!=Channel.Flavor||tick>=flavorAt;
        channelReady&=pacing.Ready(tick,priority);
        bool canChat=localChat&&chat&&chatter&&repeatReady&&channelReady;
        if(requireChat&&chatter&&!canChat)return false;
        bool delivered=false,saidLine=false;
        if(!CompanionNetwork.Client&&emotes&&(!chat||!chatter||canChat)&&(tick>=bubbleAt||BubblePriority(channel)>bubblePriority)&&(!nextBubble.TryGetValue(repeatKey,out int nextEmote)||tick>=nextEmote)) {
            StartBubble(cat,emote,BubblePriority(channel));
            nextBubble[repeatKey]=tick+System.Math.Max(180,repeatDelay);
            delivered=true;
        }
        if(canChat) {
            string lineKey=s.Dialogue.Next(key,variant);string line=Language.GetTextValue(lineKey,CompanionMemories.Context(lineKey,context));
            if(CompanionNetwork.Server)CompanionNetwork.Say(s,lineKey,context);else Main.NewText(s.CatName+"："+line,Color.SandyBrown);Chats++;saidLine=true;
            LastChatKey=key;LastLineKey=lineKey;if(FirstChatKey.Length==0)FirstChatKey=key;
            nextEvent[repeatKey]=tick+repeatDelay;
            pacing.Spoken(tick,priority);
            flavorAt=tick+1800;
            delivered=true;
        }
        if(CompanionNetwork.Client&&channel==Channel.Discovery&&emotes&&(saidLine||!chatter)&&tick>=bubbleAt){
            CompanionNetwork.RequestDiscoveryBubble(emote);bubbleAt=tick+180;delivered=true;
        }
        if(saidLine&&cat.ModProjectile is CompanionCat companion)companion.Feedback.Voice(cat,key);
        // A bubble is never proof that the associated text reached the player.
        return chat&&chatter?saidLine:delivered;
    }
}
