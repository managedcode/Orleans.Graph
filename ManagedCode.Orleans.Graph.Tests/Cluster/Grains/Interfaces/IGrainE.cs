namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IGrainE : IGrainWithStringKey
{
    Task<int> MethodE1(int input);
    Task<int> MethodD2(int input);
}