namespace PalicoCompanion.Systems;

// A character preference, independent of the individual automatic-work settings.
internal static class CompanionEnvironment {
    internal static void RequestToggle(CompanionPlayer s)=>
        CompanionControl.Request(s,CompanionControlOp.WorkMode,(int)CompanionWorkModes.Next(s.WorkMode));

    internal static void SetPaused(CompanionPlayer s,bool paused) {
        SetMode(s,paused?CompanionWorkMode.Off:CompanionWorkMode.Automatic);
    }
    internal static void SetMode(CompanionPlayer s,CompanionWorkMode mode) {
        s.WorkMode=mode;
        if(mode==CompanionWorkMode.Automatic)return;
        if(mode==CompanionWorkMode.Off)s.Digging.Clear(s);
        s.MiningHelper.Clear();
        if(!s.Mining.OwnerRequested&&(mode==CompanionWorkMode.Off||!s.Mining.Terrain))s.Mining.Cancel();
        if(!s.Trees.OwnerRequested&&(mode==CompanionWorkMode.Off||!s.Trees.Requested))s.Trees.Cancel();
        s.Garden.Cancel();
        if(mode==CompanionWorkMode.Off)s.Construction.Cancel();
        s.Travel.ClearGoal();
    }
}
