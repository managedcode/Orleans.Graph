namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IGrainC : IGrainWithStringKey
{
    Task<int> MethodC1(int input);
    Task<int> MethodA2(int input);
}