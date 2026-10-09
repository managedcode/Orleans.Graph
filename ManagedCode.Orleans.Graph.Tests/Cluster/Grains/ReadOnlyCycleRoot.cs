using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Serialization;

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

    public Task<int> WriteThroughSelfAsync() =>
        GrainFactory.GetGrain<IReadOnlyCycleRoot>(this.GetPrimaryKeyString()).WriteValueAsync();

    public Task<int> InterleavingWriteThroughPeerAsync() =>
        GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).WriteRootAsync();

    public Task<int> WriteThroughPeerNormallyAsync() =>
        GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).WriteRootAsync();

    public Task<int> WriteThroughPeerWithCallChainReentrancyAsync() =>
        this.WithCallChainReentrancyAsync(() =>
            GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).WriteRootAsync());

    public Task<int> WriteThroughPeerWithInheritedScopeAsync() =>
        this.WithCallChainReentrancyAsync(() =>
            GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).WriteThroughRootAsync());

    public Task<int> WriteThroughScopedPeerAgainAsync() =>
        this.WithCallChainReentrancyAsync(() =>
            GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).WriteThroughRootWithOwnScopeAsync());

    public Task<int> WriteThroughPeerWithSuppressedScopeAsync() =>
        this.WithCallChainReentrancyAsync(() =>
            GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).WriteRootWithSuppressedScopeAsync());

    public Task<int> WriteThroughPeerWithNewScopeAsync() =>
        this.WithCallChainReentrancyAsync(() =>
            GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).WriteRootWithNewScopeAsync());

    public async Task<int> WriteAfterScopeAsync()
    {
        await WriteThroughPeerWithCallChainReentrancyAsync();
        return await WriteThroughPeerNormallyAsync();
    }

    public async Task<int> WriteAfterFailedScopeAsync()
    {
        try
        {
            await this.WithCallChainReentrancyAsync<int>(async () =>
            {
                await WriteThroughPeerNormallyAsync();
                throw new InvalidOperationException("Operation failed after its callback.");
            });
        }
        catch (InvalidOperationException exception) when (exception.Message == "Operation failed after its callback.")
        {
            return await WriteThroughPeerNormallyAsync();
        }

        throw new InvalidOperationException("The scoped operation must fail.");
    }

    public Task<int> WriteThroughNestedSelfScopesAsync() =>
        this.WithCallChainReentrancyAsync(async () =>
        {
            await this.WithCallChainReentrancyAsync(async () =>
            {
                GetHistory().NativeCallChainScopes.Length.ShouldBe(1);
                return await WriteThroughPeerNormallyAsync();
            });
            GetHistory().NativeCallChainScopes.Length.ShouldBe(1);
            return await WriteThroughPeerNormallyAsync();
        });

    public Task<int> WriteThroughParallelScopedPeersAsync() =>
        this.WithCallChainReentrancyAsync(async () =>
        {
            var values = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
                GrainFactory.GetGrain<IReadOnlyCyclePeer>($"{this.GetPrimaryKeyString()}-{index}")
                    .WriteNamedRootAsync(this.GetPrimaryKeyString())));
            values.ShouldAllBe(value => value == Value);
            GetHistory().NativeCallChainScopes.Length.ShouldBe(1);
            return Value;
        });

    public Task<int> WriteThroughSerializedScopeAsync() =>
        this.WithCallChainReentrancyAsync(async () =>
        {
            var serializer = GrainContext.ActivationServices.GetRequiredService<Serializer>();
            var original = GetHistory();
            var restored = serializer.Deserialize<CallHistory>(serializer.SerializeToArray(original));
            restored.ShouldNotBeNull();
            restored.NativeCallChainScopes.Length.ShouldBe(1);
            restored.NativeCallChainScopes[0].ActorId.ShouldBe(GrainContext.GrainId);
            restored.NativeCallChainScopes[0].ReentrancyId.ShouldBe(RequestContext.ReentrancyId);
            RequestContext.Set(Constants.RequestContextKey, restored);
            return await WriteThroughPeerNormallyAsync();
        });

    public Task<int> WriteThroughDifferentScopedRootAsync() =>
        this.WithCallChainReentrancyAsync(() =>
            GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).WriteDifferentRootAsync());

    public Task WriteWithoutResultInScopeAsync() =>
        this.WithCallChainReentrancyAsync(async () =>
        {
            (await WriteThroughPeerNormallyAsync()).ShouldBe(Value);
        });

    public Task<int> WriteWithDetachedScopeAsync() =>
        this.WithCallChainReentrancyAsync(async () =>
        {
            var detached = GetHistory().Fork(detach: true);
            detached.NativeCallChainScopes.ShouldBeEmpty();
            RequestContext.Set(Constants.RequestContextKey, detached);
            // The root remains native-reentrant, but detached work cannot claim
            // permission from its old graph scope.
            return await WriteThroughPeerNormallyAsync();
        });

    private static CallHistory GetHistory() =>
        (CallHistory)RequestContext.Get(Constants.RequestContextKey)!;

    public Task<int> WriteThroughInterleavingPeerAsync() =>
        GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).WriteInterleavingRootAsync();

    public Task<int> InterleavingWriteThroughPeerAgainAsync() =>
        GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).WriteThroughRootAsync();

    public Task<int> WriteValueAsync() => Task.FromResult(Value);

    public Task<int> ReadValueAsync() => Task.FromResult(Value);

    public Task<int> ReadInterleavingValueAsync() => Task.FromResult(Value);

    public Task<int> ReadInterleavingPeerAsync() =>
        GrainFactory.GetGrain<IReadOnlyCyclePeer>(this.GetPrimaryKeyString()).ReadValueAsync();
}
