namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IStatelessWorkerCallerGrain : IGrainWithStringKey
{
    Task<int> CallGrainBAsync(int input);

    Task<StatelessWorkerCallResult> CallGrainBWithActivationAsync(int input, int delayMilliseconds);

    Task StartTimerOriginatedCallAsync();

    Task<int?> GetTimerOriginatedCallResultAsync();

    Task<string?> GetTimerOriginatedCallFailureAsync();
}
