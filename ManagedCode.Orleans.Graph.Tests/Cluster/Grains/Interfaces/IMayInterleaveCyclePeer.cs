namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IMayInterleaveCyclePeer : IGrainWithStringKey
{
    Task<int> CallRootAsync();
}
