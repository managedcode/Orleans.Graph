using ManagedCode.Orleans.Graph.Extensions;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

public class TestRuntimeGraphInternalSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddOrleansGraph(
            configureFilters: filters =>
            {
                filters.LiveGraphFlushPeriod = TimeSpan.FromMilliseconds(50);
                filters.TrackOrleansGraphInternalCalls = true;
            },
            configureGraph: graph => graph.AllowAll());
    }
}
