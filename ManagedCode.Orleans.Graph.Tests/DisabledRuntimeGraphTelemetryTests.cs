using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests;

public class DisabledRuntimeGraphTelemetryTests
{
    [Test]
    public async Task DisabledCollectionSendsNoWorkerCallsAndPreservesPolicyAndHealthyContinuationAsync()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<DisabledTelemetrySiloConfiguration>();
        builder.AddClientBuilderConfigurator<TestRuntimeGraphClientConfigurations>();
        await using var cluster = builder.Build();
        await cluster.DeployAsync();
        var grain = cluster.Client.GetGrain<IGrainA>(nameof(DisabledCollectionSendsNoWorkerCallsAndPreservesPolicyAndHealthyContinuationAsync));

        (await grain.MethodB1(41)).ShouldBe(42);
        var denied = await Should.ThrowAsync<InvalidOperationException>(() => grain.MethodC1(41));
        denied.Message.ShouldContain("is not allowed");
        (await grain.MethodB1(7)).ShouldBe(8);

        foreach (var silo in cluster.Silos)
        {
            cluster.GetSiloServiceProvider(silo.SiloAddress)
                .GetRequiredService<TelemetryCallObservation>().Calls.ShouldBe(0);
        }
        var telemetry = cluster.Client.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);
        (await telemetry.GetObservedGraphAsync()).Edges.Count.ShouldBe(0);
    }

    public sealed class DisabledTelemetrySiloConfiguration : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.Services.AddSingleton<TelemetryCallObservation>();
            siloBuilder.Services.AddSingleton<IOutgoingGrainCallFilter>(services => services.GetRequiredService<TelemetryCallObservation>());
            siloBuilder.AddOrleansGraph(
                configureFilters: filters => filters.EnableLiveGraphTelemetry = false,
                configureGraph: graph =>
                {
                    graph.AllowClientCallGrain<IGrainA>();
                    graph.AddGrainTransition<IGrainA, IGrainB>()
                        .MethodByName(nameof(IGrainA.MethodB1), nameof(IGrainB.MethodB1));
                });
        }
    }

    public sealed class TelemetryCallObservation : IOutgoingGrainCallFilter
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);

        public async Task Invoke(IOutgoingGrainCallContext context)
        {
            if (context.InterfaceMethod.DeclaringType == typeof(IOrleansGraphTelemetryWorker))
            {
                Interlocked.Increment(ref _calls);
            }
            await context.Invoke();
        }
    }
}
