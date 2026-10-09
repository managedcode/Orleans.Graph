using Orleans.Concurrency;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IReadOnlyCyclePeer : IGrainWithStringKey
{
    [ReadOnly]
    Task<int> ReadRootAsync(bool interleaveReturn);

    Task<int> ReadInterleavingRootAsync();

    Task<int> WriteRootAsync();

    [AlwaysInterleave]
    Task<int> WriteInterleavingRootAsync();

    Task<int> WriteThroughRootAsync();

    Task<int> WriteThroughRootWithOwnScopeAsync();

    Task<int> WriteRootWithSuppressedScopeAsync();

    Task<int> WriteRootWithNewScopeAsync();

    Task<int> WriteNamedRootAsync(string rootKey);

    Task<int> WriteDifferentRootAsync();

    [ReadOnly]
    Task<int> ReadValueAsync();
}
