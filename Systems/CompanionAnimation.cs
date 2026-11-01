using System;
namespace PalicoCompanion.Systems;

public enum CatMotion { Idle, Blink, Walk, Rise, Fall, Land, Rest, Paused, Mine, Attack, Alert, Pet, Downed, Flight, Thrust, Wake, Recover, Swim }

// Simulation-owned timing; rendering never advances animation or selects by localized text.
// Native sprite frames; visual acceptance remains separate from state correctness.
public sealed class CompanionAnimation {
    public CatMotion Motion { get; private set; } = CatMotion.Idle;
    public int IdleTicks { get; private set; }
    public int StateTicks { get; private set; }
    public int SpriteFrame { get; private set; }
    internal void SetNetworkFrame(int frame){SpriteFrame=Math.Clamp(frame,0,CompanionSpriteLayout.Frames-1);}
    double walkPhase;
    internal double WalkPhase=>walkPhase;
    internal void PlayReplica(CompanionPose pose,double elapsed){
        Motion=pose.Motion;IdleTicks=pose.IdleTicks+(int)elapsed;StateTicks=pose.StateTicks+(int)elapsed;
        float progress=pose.Tool>0?pose.ToolProgress:pose.ItemMax>0?1-pose.ItemAnimation/(float)pose.ItemMax:1;
        SpriteFrame=Motion switch {
            CatMotion.Walk=>CompanionSpriteLayout.Walk0+(int)((pose.WalkPhase+elapsed/WalkFrameTicks(pose.Velocity.X))%CompanionSpriteLayout.WalkFrames),
            CatMotion.Mine when pose.Tool==0&&pose.ItemMax==0=>pose.Frame,
            CatMotion.Mine or CatMotion.Attack=>SwingFrame(progress),
            // Fixed held poses are chosen by the authority and already sent
            // in Frame; replicas must preserve gun/bow versus other use styles.
            CatMotion.Thrust=>Math.Clamp(pose.Frame,0,CompanionSpriteLayout.Frames-1),
            CatMotion.Recover=>RecoveryFrame(StateTicks),
            CatMotion.Flight=>CompanionSpriteLayout.Rise,
            CatMotion.Swim=>SwimFrame(StateTicks),
            CatMotion.Rest=>RestFrame(StateTicks),
            CatMotion.Idle or CatMotion.Blink=>IdleFrame(IdleTicks),
            _=>Math.Clamp(pose.Frame,0,CompanionSpriteLayout.Frames-1)
        };
    }
    // Map the artist's six poses onto native item/tool progress. The two idle
    // endpoints are transitions, not the long pauses of the reference GIF.
    internal static int SwingFrame(float progress)=>CompanionSpriteLayout.Swing0+
        (progress<.08f?0:progress<.4f?1:progress<.58f?2:progress<.76f?3:progress<.94f?4:5);
    internal static int RecoveryFrame(int ticks)=>ticks<11?CompanionSpriteLayout.GetUp0:CompanionSpriteLayout.GetUp1;
    bool wasAirborne;
    int landingTicks,wakeTicks,recoverTicks;
    // Ground pets speed up their frame clock with movement. Keep a float clock
    // to avoid the integer-speed jumps in vanilla, and cap at 20 frames/second.
    public static double WalkFrameTicks(float vx)=>Math.Clamp(12.0/(1.0+Math.Abs(vx)),3.0,8.0);
    internal static int SwimFrame(int ticks)=>ticks%48<24?CompanionSpriteLayout.Rise:CompanionSpriteLayout.Fall;
    // Preserve the artist's 900 ms idle poses and 200 ms sit/stand transitions.
    // Rest keeps the existing four-second blink cadence; the first blink follows
    // the authored 650 ms seated hold and lasts 150 ms. Both peers sample this.
    const int RestTransitionTicks=12;
    internal static int IdleFrame(int ticks)=>ticks%240>=231?CompanionSpriteLayout.Blink:
        ticks%108>=54?CompanionSpriteLayout.Breathe:CompanionSpriteLayout.Idle;
    internal static int RestFrame(int ticks){
        if(ticks<RestTransitionTicks)return CompanionSpriteLayout.RestEnter;
        int phase=(ticks-RestTransitionTicks)%240;
        return phase>=39&&phase<48?CompanionSpriteLayout.RestBlink:CompanionSpriteLayout.Rest;
    }
    public void Update(float vx,float vy,bool downed,bool paused,bool mining,bool attacking,bool petting,bool alert,bool busy,bool nearOwner,bool grounded=true,float actionProgress=0,bool flying=false,bool thrusting=false,bool swimming=false,bool rangedHold=false) {
        bool moving=Math.Abs(vx)>0.5f, airborne=flying||!grounded||Math.Abs(vy)>0.6f;
        if(Motion==CatMotion.Downed&&!downed)recoverTicks=22;
        else if(recoverTicks>0)recoverTicks--;
        if(Motion==CatMotion.Rest&&(busy||alert||petting||paused||moving||airborne||!nearOwner))wakeTicks=RestTransitionTicks;
        else if(wakeTicks>0)wakeTicks--;
        if(wasAirborne&&!airborne)landingTicks=5;
        else if(landingTicks>0)landingTicks--;
        wasAirborne=airborne;
        bool idle=!downed&&!paused&&!mining&&!attacking&&!petting&&!alert&&!busy&&!moving&&!airborne&&nearOwner;
        IdleTicks=idle?Math.Min(IdleTicks+1,36000):0;
        CatMotion next=downed?CatMotion.Downed:
            paused?CatMotion.Paused:mining?CatMotion.Mine:attacking?(thrusting?CatMotion.Thrust:CatMotion.Attack):
            swimming?CatMotion.Swim:
            flying?CatMotion.Flight:
            airborne?(vy<0?CatMotion.Rise:CatMotion.Fall):
            landingTicks>0?CatMotion.Land:petting?CatMotion.Pet:recoverTicks>0?CatMotion.Recover:wakeTicks>0?CatMotion.Wake:
            moving?CatMotion.Walk:alert?CatMotion.Alert:IdleTicks>=360?CatMotion.Rest:IdleTicks%240>=231?CatMotion.Blink:CatMotion.Idle;
        StateTicks=Motion==next?Math.Min(StateTicks+1,36000):0;
        Motion=next;
        if(next==CatMotion.Walk) {
            walkPhase=(walkPhase+1.0/WalkFrameTicks(vx))%CompanionSpriteLayout.WalkFrames;
        } else walkPhase=0;
        SpriteFrame=next switch {
            CatMotion.Walk=>CompanionSpriteLayout.Walk0+(int)walkPhase,
            CatMotion.Blink=>CompanionSpriteLayout.Blink,
            CatMotion.Rise=>CompanionSpriteLayout.Rise,CatMotion.Fall=>CompanionSpriteLayout.Fall,
            CatMotion.Flight=>CompanionSpriteLayout.Rise,
            CatMotion.Swim=>SwimFrame(StateTicks),
            CatMotion.Thrust=>rangedHold?CompanionSpriteLayout.RangedHold:CompanionSpriteLayout.Swing0+4,
            CatMotion.Land=>CompanionSpriteLayout.Land,
            CatMotion.Rest=>RestFrame(StateTicks),
            CatMotion.Pet=>CompanionSpriteLayout.Pet,
            CatMotion.Wake=>CompanionSpriteLayout.RestExit,
            CatMotion.Recover=>RecoveryFrame(StateTicks),
            CatMotion.Mine or CatMotion.Attack=>SwingFrame(actionProgress),
            CatMotion.Downed=>CompanionSpriteLayout.Downed,
            CatMotion.Idle=>IdleFrame(IdleTicks),
            _=>CompanionSpriteLayout.Idle
        };
    }
}
