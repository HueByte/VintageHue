using System;
using Vintagestory.API.Server;

namespace HueHordes.Debug;

/// <summary>
/// Minimal debug logging system for the NewAI
/// </summary>
public static class DebugLogger
{
    private static ICoreServerAPI? sapi;
    private static bool enabled = false;
    private static int level = 1;

    public static void Initialize(ICoreServerAPI serverApi, bool enableLogging, int loggingLevel)
    {
        sapi = serverApi;
        enabled = enableLogging;
        level = loggingLevel;
    }

    public static void Event(string title, string message = "", string context = "")
    {
        if (!enabled || sapi == null) return;
        sapi.Logger.Event($"[HueHordes] {title}: {message} [{context}]");
    }

    public static void AIEvent(string title, string message, string entityId)
    {
        if (!enabled || level < 2 || sapi == null) return;
        sapi.Logger.Event($"[HueHordes:AI] {title}: {message} [Entity:{entityId}]");
    }

    public static void AITarget(string entityId, string targetType, string targetName, string message)
    {
        if (!enabled || level < 2 || sapi == null) return;
        sapi.Logger.Event($"[HueHordes:Target] Entity:{entityId} -> {targetType}:{targetName} - {message}");
    }

    public static void AIPath(string entityId, string pathType, string waypoints, string message)
    {
        if (!enabled || level < 3 || sapi == null) return;
        sapi.Logger.Event($"[HueHordes:Path] Entity:{entityId} {pathType} ({waypoints}) - {message}");
    }

    public static void AIState(string entityId, string oldState, string newState, string message)
    {
        if (!enabled || level < 2 || sapi == null) return;
        sapi.Logger.Event($"[HueHordes:State] Entity:{entityId} {oldState} -> {newState} - {message}");
    }

    public static void AISpawn(string entityType, string position, string target)
    {
        if (!enabled || level < 1 || sapi == null) return;
        sapi.Logger.Event($"[HueHordes:Spawn] {entityType} at {position} targeting {target}");
    }

    public static void Error(string message, Exception? ex = null)
    {
        if (sapi == null) return;
        if (ex != null)
            sapi.Logger.Error($"[HueHordes] {message}: {ex.Message}");
        else
            sapi.Logger.Error($"[HueHordes] {message}");
    }

    public static IDisposable TrackMethod()
    {
        // Simple no-op disposable for method tracking compatibility
        return new NoOpDisposable();
    }

    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}