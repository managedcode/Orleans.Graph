using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
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

    public Task<int> WriteRootAsync() =>
        GrainFactory.GetGrain<IReadOnlyCycleRoot>(this.GetPrimaryKeyString()).WriteValueAsync();

    public Task<int> WriteInterleavingRootAsync() => WriteRootAsync();

    public Task<int> WriteThroughRootAsync() =>
        GrainFactory.GetGrain<IReadOnlyCycleRoot>(this.GetPrimaryKeyString()).WriteThroughPeerNormallyAsync();

    public Task<int> WriteThroughRootWithOwnScopeAsync() =>
        this.WithCallChainReentrancyAsync(WriteThroughRootAsync);

    public async Task<int> WriteRootWithSuppressedScopeAsync()
    {
        using var suppression = RequestContext.SuppressCallChainReentrancy();
        return await WriteRootAsync();
    }

    public async Task<int> WriteRootWithNewScopeAsync()
    {
        using var suppression = RequestContext.SuppressCallChainReentrancy();
        return await this.WithCallChainReentrancyAsync(WriteRootAsync);
    }

    public Task<int> WriteNamedRootAsync(string rootKey) =>
        this.WithCallChainReentrancyAsync(() =>
        {
            GetHistory().NativeCallChainScopes.Length.ShouldBe(2);
            GetHistory().NativeCallChainScopes.Count(scope => scope.ActorId == GrainContext.GrainId).ShouldBe(1);
            return GrainFactory.GetGrain<IReadOnlyCycleRoot>(rootKey).WriteValueAsync();
        });

    private static CallHistory GetHistory() =>
        (CallHistory)RequestContext.Get(Constants.RequestContextKey)!;

    public Task<int> WriteDifferentRootAsync() =>
        GrainFactory.GetGrain<IReadOnlyCycleRoot>($"{this.GetPrimaryKeyString()}-different")
            .WriteThroughPeerNormallyAsync();

    public Task<int> ReadValueAsync() => Task.FromResult(ReadOnlyCycleRoot.Value);
}
