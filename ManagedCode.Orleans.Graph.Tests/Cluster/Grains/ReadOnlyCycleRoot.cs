using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

public class ReadOnlyCycleRoot : Grain, IReadOnlyCycleRoot
{
    public const int Value = 42;

    public Task<int> ReadThroughPeerAsync() =>
        GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).ReadRootAsync(false);

    public Task<int> WriteThroughPeerAsync(bool interleaveReturn) =>
        GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).ReadRootAsync(interleaveReturn);

    public Task<int> WriteThroughPeerAgainAsync() =>
        GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).ReadInterleavingRootAsync();

    public Task<int> ReadValueAsync() => Task.FromResult(Value);

    public Task<int> ReadInterleavingValueAsync() => Task.FromResult(Value);

    public Task<int> ReadInterleavingPeerAsync() =>
        GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).ReadValueAsync();
}
