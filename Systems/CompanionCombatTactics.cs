namespace PalicoCompanion.Systems;

// A living companion can change its spacing without changing weapon damage or
// refusing to defend itself. Different enter/exit thresholds prevent oscillation.
internal sealed class CompanionCombatTactics {
    internal bool Cautious {get;private set;}
    internal bool Returning {get;private set;}
    internal bool Approaching {get;set;}
    internal bool FollowingOwner {get;set;}
    // Keep the existing return conditions without latching past their boundary.
    internal bool ReturnToOwner(float distance,bool outside,bool leaving)=>Returning=outside||leaving&&distance>480;
    internal void EndReturn()=>Returning=false;
    internal void Update(CompanionPlayer state,bool target) {
        if(!target||!state.AttacksEnabled||state.Paused||state.Vitals.Downed){
            Cautious=false;Returning=Approaching=FollowingOwner=false;return;
        }
        float health=state.Vitals.Health/(float)System.Math.Max(1,state.Vitals.MaxHealth);
        if(health<=.3f)Cautious=true;
        else if(health>=.45f)Cautious=false;
    }
}
