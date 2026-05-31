namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IStatelessWorkerCallStateGrain : IGrainWithStringKey
{
    Task ResetTimerOriginatedCallAsync();

    Task RecordTimerOriginatedCallResultAsync(int result);

    Task RecordTimerOriginatedCallFailureAsync(string failure);

    Task<int?> GetTimerOriginatedCallResultAsync();

    Task<string?> GetTimerOriginatedCallFailureAsync();
}
