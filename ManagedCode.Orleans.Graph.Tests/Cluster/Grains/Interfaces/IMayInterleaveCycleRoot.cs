namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IMayInterleaveCycleRoot : IGrainWithStringKey
{
    Task<int> StartCycleAsync(bool allowInterleave);

    Task<int> CallbackAsync();
}
