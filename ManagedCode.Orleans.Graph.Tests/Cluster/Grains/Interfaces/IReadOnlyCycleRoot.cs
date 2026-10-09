using Orleans.Concurrency;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IReadOnlyCycleRoot : IGrainWithStringKey
{
    [ReadOnly]
    Task<int> ReadThroughPeerAsync();

    Task<int> WriteThroughPeerAsync(bool interleaveReturn);

    Task<int> WriteThroughPeerAgainAsync();

    [AlwaysInterleave]
    Task<int> WriteThroughSelfAsync();

    [AlwaysInterleave]
    Task<int> InterleavingWriteThroughPeerAsync();

    Task<int> WriteThroughPeerNormallyAsync();

    Task<int> WriteThroughInterleavingPeerAsync();

    [AlwaysInterleave]
    Task<int> InterleavingWriteThroughPeerAgainAsync();

    Task<int> WriteValueAsync();

    [ReadOnly]
    Task<int> ReadValueAsync();

    [AlwaysInterleave]
    Task<int> ReadInterleavingValueAsync();

    [AlwaysInterleave]
    Task<int> ReadInterleavingPeerAsync();
}
