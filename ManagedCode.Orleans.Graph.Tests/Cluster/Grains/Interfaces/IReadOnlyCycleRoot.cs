using Orleans.Concurrency;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IReadOnlyCycleRoot : IGrainWithStringKey
{
    [ReadOnly]
    Task<int> ReadThroughPeerAsync();

    Task<int> WriteThroughPeerAsync(bool interleaveReturn);

    Task<int> WriteThroughPeerAgainAsync();

    [ReadOnly]
    Task<int> ReadValueAsync();

    [AlwaysInterleave]
    Task<int> ReadInterleavingValueAsync();

    [AlwaysInterleave]
    Task<int> ReadInterleavingPeerAsync();
}
