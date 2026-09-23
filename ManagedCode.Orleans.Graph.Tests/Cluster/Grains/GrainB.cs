using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

public class GrainB : Grain, IGrainB
{
    private int? _oneWayHistoryDepth;

    public async Task<int> MethodB1(int input)
    {
        return await Task.FromResult(input + 1);
    }

    public async Task<int> MethodC2(int input)
    {
        return await GrainFactory.GetGrain<IGrainC>(this.GetPrimaryKeyString())
            .MethodA2(input);
    }

    public Task<int> GetCallHistoryDepthAsync()
    {
        var history = RequestContext.Get(Constants.RequestContextKey) as CallHistory;
        return Task.FromResult(history?.History.Count ?? -1);
    }

    public Task RecordOneWayHistoryDepthAsync()
    {
        var history = RequestContext.Get(Constants.RequestContextKey) as CallHistory;
        _oneWayHistoryDepth = history?.History.Count ?? -1;
        return Task.CompletedTask;
    }

    public Task<int?> GetOneWayHistoryDepthAsync() => Task.FromResult(_oneWayHistoryDepth);
}
