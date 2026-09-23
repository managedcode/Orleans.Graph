using Orleans.Concurrency;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

public interface IGrainB : IGrainWithStringKey
{
    Task<int> MethodB1(int input);
    Task<int> MethodC2(int input);

    Task<int> GetCallHistoryDepthAsync();

    [OneWay]
    Task RecordOneWayHistoryDepthAsync();

    Task<int?> GetOneWayHistoryDepthAsync();
}
