using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using Orleans.Concurrency;
using Orleans.Serialization.Invocation;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

[MayInterleave(nameof(CanInterleave))]
public sealed class MayInterleaveCycleRoot : Grain, IMayInterleaveCycleRoot
{
    public static bool CanInterleave(IInvokable request) =>
        request.GetArgumentCount() == 1 && request.GetArgument(0) is true;

    public Task<int> StartCycleAsync(bool allowInterleave) =>
        GrainFactory.GetGrain<IMayInterleaveCyclePeer>(this.GetPrimaryKeyString()).CallRootAsync();

    public Task<int> CallbackAsync() => Task.FromResult(42);
}
