namespace PalicoCompanion.Systems;

// A living companion can change its spacing without changing weapon damage or
// refusing to defend itself. Different enter/exit thresholds prevent oscillation.
internal sealed class CompanionCombatTactics {
    internal bool Cautious {get;private set;}
    internal void Update(CompanionPlayer state,bool target) {
        if(!target||!state.Combat||state.Paused||state.Vitals.Downed){Cautious=false;return;}
        float health=state.Vitals.Health/(float)System.Math.Max(1,state.Vitals.MaxHealth);
        if(health<=.3f)Cautious=true;
        else if(health>=.45f)Cautious=false;
    }
}
