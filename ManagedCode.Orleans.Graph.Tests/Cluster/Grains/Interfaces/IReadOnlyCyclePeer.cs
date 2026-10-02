using Orleans.Concurrency;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IReadOnlyCyclePeer : IGrainWithStringKey
{
    [ReadOnly]
    Task<int> ReadRootAsync(bool interleaveReturn);

    Task<int> ReadInterleavingRootAsync();

    [ReadOnly]
    Task<int> ReadValueAsync();
}
