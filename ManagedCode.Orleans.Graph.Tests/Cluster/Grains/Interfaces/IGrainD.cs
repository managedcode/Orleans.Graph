namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IGrainD : IGrainWithStringKey
{
    Task<int> MethodD1(int input);
    Task<int> MethodE2(int input);
}