using System;

namespace PalicoCompanion.Systems;

// Scale completed work, not target scans, pathfinding, permissions or failure backoff.
internal static class CompanionWorkTiming {
    internal static float Exact(float ticks)=>Math.Max(1,ticks/CompanionBalanceConfig.Current.WorkScale);
    internal static int Ticks(float ticks)=>(int)Math.Ceiling(Exact(ticks));
    internal static int Timeout(int ticks)=>Math.Max(ticks,Ticks(ticks));
    internal static int ExtraDelay(int ticks)=>Math.Max(0,Ticks(ticks)-ticks);
    internal static int FishingTimeout(int ticks)=>(int)Math.Ceiling(ticks/Math.Min(1,CompanionBalanceConfig.Current.FishingScale));
}
