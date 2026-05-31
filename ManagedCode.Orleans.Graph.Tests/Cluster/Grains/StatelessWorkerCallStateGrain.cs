using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

public class StatelessWorkerCallStateGrain : Grain, IStatelessWorkerCallStateGrain
{
    private int? _timerOriginatedCallResult;
    private string? _timerOriginatedCallFailure;

    public Task ResetTimerOriginatedCallAsync()
    {
        _timerOriginatedCallResult = null;
        _timerOriginatedCallFailure = null;

        return Task.CompletedTask;
    }

    public Task RecordTimerOriginatedCallResultAsync(int result)
    {
        _timerOriginatedCallResult = result;

        return Task.CompletedTask;
    }

    public Task RecordTimerOriginatedCallFailureAsync(string failure)
    {
        _timerOriginatedCallFailure = failure;

        return Task.CompletedTask;
    }

    public Task<int?> GetTimerOriginatedCallResultAsync()
    {
        return Task.FromResult(_timerOriginatedCallResult);
    }

    public Task<string?> GetTimerOriginatedCallFailureAsync()
    {
        return Task.FromResult(_timerOriginatedCallFailure);
    }
}
