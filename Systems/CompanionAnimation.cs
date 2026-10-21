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
            CatMotion.Walk=>CompanionSpriteLayout.Walk0+(int)((pose.WalkPhase+elapsed/WalkFrameTicks(pose.Velocity.X))%8),
            CatMotion.Mine when pose.Tool==0&&pose.ItemMax==0=>pose.Frame,
            CatMotion.Mine or CatMotion.Attack=>progress<.45f?CompanionSpriteLayout.WorkWindup:progress<.7f?CompanionSpriteLayout.WorkStrike:CompanionSpriteLayout.WorkRecover,
            CatMotion.Thrust=>CompanionSpriteLayout.WorkRecover,
            CatMotion.Flight=>CompanionSpriteLayout.Rise,
            CatMotion.Swim=>SwimFrame(StateTicks),
            CatMotion.Rest=>StateTicks<15?CompanionSpriteLayout.RestEnter:StateTicks%240>=230?CompanionSpriteLayout.RestBlink:CompanionSpriteLayout.Rest,
            CatMotion.Idle or CatMotion.Blink=>IdleTicks%240>=232?CompanionSpriteLayout.Blink:IdleTicks%120>=60?CompanionSpriteLayout.Breathe:CompanionSpriteLayout.Idle,
            _=>Math.Clamp(pose.Frame,0,CompanionSpriteLayout.Frames-1)
        };
    }
    bool wasAirborne;
    int landingTicks,wakeTicks,recoverTicks;
    // Ground pets speed up their frame clock with movement. Keep a float clock
    // to avoid the integer-speed jumps in vanilla, and cap at 20 frames/second.
    public static double WalkFrameTicks(float vx)=>Math.Clamp(12.0/(1.0+Math.Abs(vx)),3.0,8.0);
    internal static int SwimFrame(int ticks)=>ticks%48<24?CompanionSpriteLayout.Rise:CompanionSpriteLayout.Fall;
    public void Update(float vx,float vy,bool downed,bool paused,bool mining,bool attacking,bool petting,bool alert,bool busy,bool nearOwner,bool grounded=true,float actionProgress=0,bool flying=false,bool thrusting=false,bool swimming=false) {
        bool moving=Math.Abs(vx)>0.5f, airborne=flying||!grounded||Math.Abs(vy)>0.6f;
        if(Motion==CatMotion.Downed&&!downed)recoverTicks=18;
        else if(recoverTicks>0)recoverTicks--;
        if(Motion==CatMotion.Rest&&(busy||alert||petting||paused||moving||airborne))wakeTicks=8;
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
            moving?CatMotion.Walk:alert?CatMotion.Alert:IdleTicks>=360?CatMotion.Rest:IdleTicks%240>=232?CatMotion.Blink:CatMotion.Idle;
        StateTicks=Motion==next?Math.Min(StateTicks+1,36000):0;
        Motion=next;
        if(next==CatMotion.Walk) {
            walkPhase=(walkPhase+1.0/WalkFrameTicks(vx))%8.0;
        } else walkPhase=0;
        SpriteFrame=next switch {
            CatMotion.Walk=>CompanionSpriteLayout.Walk0+(int)walkPhase,
            CatMotion.Blink=>CompanionSpriteLayout.Blink,
            CatMotion.Rise=>CompanionSpriteLayout.Rise,CatMotion.Fall=>CompanionSpriteLayout.Fall,
            CatMotion.Flight=>CompanionSpriteLayout.Rise,
            CatMotion.Swim=>SwimFrame(StateTicks),
            CatMotion.Thrust=>CompanionSpriteLayout.WorkRecover,
            CatMotion.Land=>CompanionSpriteLayout.Land,
            CatMotion.Rest=>StateTicks<15?CompanionSpriteLayout.RestEnter:
                StateTicks%240>=230?CompanionSpriteLayout.RestBlink:CompanionSpriteLayout.Rest,
            CatMotion.Pet=>CompanionSpriteLayout.Pet,
            CatMotion.Wake=>CompanionSpriteLayout.RestEnter,
            CatMotion.Recover=>recoverTicks>10?CompanionSpriteLayout.RestEnter:CompanionSpriteLayout.Pet,
            CatMotion.Mine or CatMotion.Attack=>actionProgress<0.45f?CompanionSpriteLayout.WorkWindup:
                actionProgress<0.7f?CompanionSpriteLayout.WorkStrike:CompanionSpriteLayout.WorkRecover,
            CatMotion.Downed=>CompanionSpriteLayout.Downed,
            CatMotion.Idle when IdleTicks%120>=60=>CompanionSpriteLayout.Breathe,
            _=>CompanionSpriteLayout.Idle
        };
    }
}
