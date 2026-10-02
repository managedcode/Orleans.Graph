using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

public class ReadOnlyCyclePeer : Grain, IReadOnlyCyclePeer
{
    public Task<int> ReadRootAsync(bool interleaveReturn)
    {
        var root = GrainFactory.GetGrain<IReadOnlyCycleRoot>(this.GetPrimaryKeyString());
        return interleaveReturn ? root.ReadInterleavingValueAsync() : root.ReadValueAsync();
    }

    public Task<int> ReadInterleavingRootAsync() =>
        GrainFactory.GetGrain<IReadOnlyCycleRoot>(this.GetPrimaryKeyString()).ReadInterleavingPeerAsync();

    public Task<int> ReadValueAsync() => Task.FromResult(ReadOnlyCycleRoot.Value);
}
