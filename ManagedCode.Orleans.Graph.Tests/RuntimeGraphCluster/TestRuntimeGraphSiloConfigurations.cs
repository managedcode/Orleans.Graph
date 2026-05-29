using ManagedCode.Orleans.Graph.Extensions;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

public class TestRuntimeGraphSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddOrleansGraph(
            configureFilters: filters =>
            {
                filters.LiveGraphFlushPeriod = TimeSpan.FromMilliseconds(50);
            },
            configureGraph: graph => graph.AllowAll());
    }
}
