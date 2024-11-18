namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IGrainA : IGrainWithStringKey
{
    Task<int> MethodA1(int input);
    
    Task<int> MethodB1(int input);
    Task<int> MethodB2(int input);
    
    Task<int> MethodC1(int input);
}