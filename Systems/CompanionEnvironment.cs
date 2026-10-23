namespace PalicoCompanion.Systems;

// A character preference, independent of the individual automatic-work settings.
internal static class CompanionEnvironment {
    internal static void RequestToggle(CompanionPlayer s)=>
        CompanionControl.Request(s,CompanionControlOp.Environment,s.EnvironmentPaused?0:1);

    internal static void SetPaused(CompanionPlayer s,bool paused) {
        s.EnvironmentPaused=paused;
        if(!paused)return;
        s.Digging.Clear(s);
        s.MiningHelper.Clear();
        if(!s.Mining.OwnerRequested)s.Mining.Cancel();
        if(!s.Trees.OwnerRequested)s.Trees.Cancel();
        s.Garden.Cancel();
        s.Construction.Cancel();
        s.Travel.ClearGoal();
    }
}
