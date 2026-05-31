using ManagedCode.Orleans.Graph.Extensions;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

public class TestRuntimeGraphSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.UseInMemoryReminderService();
        siloBuilder.Configure<ReminderOptions>(options =>
        {
            options.MinimumReminderPeriod = TimeSpan.FromMilliseconds(100);
            options.RefreshReminderListPeriod = TimeSpan.FromMilliseconds(100);
            options.InitializationTimeout = TimeSpan.FromSeconds(5);
        });

        siloBuilder.AddOrleansGraph(
            configureFilters: filters =>
            {
                filters.LiveGraphFlushPeriod = TimeSpan.FromMilliseconds(50);
            },
            configureGraph: graph => graph.AllowAll());
    }
}
