namespace ManagedCode.Orleans.Graph.Interfaces;

public static class Constants
{
    public const string RequestContextKey = "mc.callhistory";
    public const string ClientCallerId = "ORLEANS_GRAIN_CLIENT";
    public const string UnknownCallerId = "UNKNOWN_CALLER";
    public const string AnyMethod = "*";
    public const string LiveGraphTelemetryGrainKey = "mc.orleans.graph.live";
    public const string TelemetrySuppressionContextKey = "mc.orleans.graph.telemetry.suppressed";
    public const string CurrentCallerContextKey = "mc.orleans.graph.current.caller";
}
