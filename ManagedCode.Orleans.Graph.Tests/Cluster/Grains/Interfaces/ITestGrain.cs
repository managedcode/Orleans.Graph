namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IGrainA : IGrainWithStringKey
{
    Task<int> MethodA1(int input);
    
    Task<int> MethodB1(int input);
    Task<int> MethodB2(int input);
    
    Task<int> MethodC1(int input);
}

public interface IGrainB : IGrainWithStringKey
{
    Task<int> MethodB1(int input);
    Task<int> MethodC2(int input);
}
public interface IGrainC : IGrainWithStringKey
{
    Task<int> MethodC1(int input);
    Task<int> MethodA2(int input);
}
public interface IGrainD : IGrainWithStringKey
{
    Task<int> MethodD1(int input);
    Task<int> MethodE2(int input);
}
public interface IGrainE : IGrainWithStringKey
{
    Task<int> MethodE1(int input);
    Task<int> MethodD2(int input);
}