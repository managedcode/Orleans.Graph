namespace ManagedCode.Orleans.Graph.Tests.SystemTargetCluster;

internal static class SystemTargetTestProtocol
{
    internal const string SharedClusterKey = nameof(SystemTargetTestCluster);
    internal const string ProbeAlias = "Graph.Tests.SystemTargetProbe";
    internal const string ReplyAlias = "Graph.Tests.SystemTargetProbeReply";
    internal const string CallerAlias = "Graph.Tests.SystemTargetCaller";
    internal const string ProbeMethodAlias = "probe";
    internal const string ServiceMethodAlias = "call-service";
    internal const string AllowedMethodAlias = "call-allowed";
    internal const string DeniedMethodAlias = "call-denied";
    internal const string TransitionDeniedPrefix = "Transition from";
    internal const int SiloCount = 2;
    internal const int ApplicationInput = 41;
    internal const int ApplicationResult = 42;
    internal const int TestTimeoutMilliseconds = 60_000;
    internal static TimeSpan RpcTimeout { get; } = TimeSpan.FromSeconds(10);
}
