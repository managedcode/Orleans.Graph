using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

public sealed class MayInterleaveCyclePeer : Grain, IMayInterleaveCyclePeer
{
    public Task<int> CallRootAsync() =>
        GrainFactory.GetGrain<IMayInterleaveCycleRoot>(this.GetPrimaryKeyString()).CallbackAsync();
}
