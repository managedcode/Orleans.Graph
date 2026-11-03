namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IGrainA : IGrainWithStringKey
{
    Task<int> MethodA1(int input);

    Task<string> CallPeerAsync(string peerKey, int remainingHops);

    Task<int> MethodB1(int input);
    Task<int> MethodB2(int input);

    Task<int> MethodC1(int input);

    Task<int> MethodComplexFlow(int input);

    Task<int> MethodGrainOnlyComplexFlow(int input, DateTimeOffset startedAt);

    Task<int[]> ObserveParallelCallHistoryAsync(int calls);

    Task StartOneWayHistoryProbeAsync();

    Task StartTimerOriginatedCallAsync();

    Task<int?> GetTimerOriginatedCallResultAsync();

    Task<string?> GetTimerOriginatedCallFailureAsync();

    Task StartReminderOriginatedCallAsync();

    Task<int?> GetReminderOriginatedCallResultAsync();

    Task<string?> GetReminderOriginatedCallFailureAsync();
}
