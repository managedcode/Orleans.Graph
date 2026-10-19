using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ManagedCode.Orleans.Graph.Tests;

public class RuntimeGraphTelemetryLifecycleTests
{
    [Test]
    public async Task TelemetryGrain_EmptyGraphAccess_AllowsIdleActivationCollectionAsync()
    {
        await using var fixture = new TestRuntimeGraphClusterApplication();
        var telemetry = fixture.Cluster.Client.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);

        var graph = await telemetry.GetObservedGraphAsync();
        graph.Edges.Count.ShouldBe(0);

        await AssertActivationCollectedAsync(fixture.Cluster.Client, telemetry);
    }

    [Test]
    public async Task TelemetryWorker_DeactivationFlushesBufferedObservedCallsAsync()
    {
        await using var fixture = new TestRuntimeGraphSlowFlushClusterApplication();
        var worker = fixture.Cluster.Client.GetGrain<IOrleansGraphTelemetryWorker>(Constants.LiveGraphTelemetryGrainKey);

        await worker.RecordObservedCallAsync(ObservedGrainCall.Create(
            "source",
            "target",
            "SourceMethod",
            "TargetMethod"));

        await fixture.Cluster.Client
            .GetGrain<IManagementGrain>(0)
            .ForceActivationCollection(TimeSpan.Zero);

        var graph = await WaitForObservedGraphAsync(
            fixture.Cluster.Client,
            graph => graph.Edges.Any(IsExpectedObservedEdge));

        graph.Edges.ShouldContain(edge => IsExpectedObservedEdge(edge));
    }

    [Test]
    [Arguments((short)1)]
    [Arguments((short)2)]
    public async Task TelemetryWorker_LastSiloShutdownDoesNotSendToUnavailableTelemetryGrainAsync(short initialSilos)
    {
        await using var fixture = new TestRuntimeGraphSlowFlushClusterApplication(initialSilos);
        using var logs = new ShutdownTelemetryLogObserver();
        foreach (var silo in fixture.Cluster.Silos)
        {
            fixture.Cluster.GetSiloServiceProvider(silo.SiloAddress)
                .GetRequiredService<ILoggerFactory>().AddProvider(logs);
        }
        var worker = fixture.Cluster.Client.GetGrain<IOrleansGraphTelemetryWorker>(Constants.LiveGraphTelemetryGrainKey);
        await worker.RecordObservedCallAsync(ObservedGrainCall.Create(
            "source", "target", "SourceMethod", "TargetMethod"));

        await fixture.Cluster.StopAllSilosAsync();

        logs.Failures.ShouldBe(0);
    }

    private static async Task AssertActivationCollectedAsync(IGrainFactory grainFactory, IAddressable grainReference)
    {
        var management = grainFactory.GetGrain<IManagementGrain>(0);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await management.ForceActivationCollection(TimeSpan.Zero);
            await Task.Delay(100);

            var currentAddress = await management.GetActivationAddress(grainReference);
            if (currentAddress is null)
            {
                return;
            }
        }

        var finalAddress = await management.GetActivationAddress(grainReference);
        finalAddress.ShouldBeNull();
    }

    private static async Task<ObservedGrainCallGraph> WaitForObservedGraphAsync(
        IGrainFactory grainFactory,
        Func<ObservedGrainCallGraph, bool> predicate)
    {
        var telemetry = grainFactory.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var graph = await telemetry.GetObservedGraphAsync();
            if (predicate(graph))
            {
                return graph;
            }

            await Task.Delay(100);
        }

        return await telemetry.GetObservedGraphAsync();
    }

    private static bool IsExpectedObservedEdge(ObservedGrainCall edge)
    {
        return string.Equals(edge.Source, "source", StringComparison.Ordinal) &&
               string.Equals(edge.Target, "target", StringComparison.Ordinal) &&
               string.Equals(edge.SourceMethod, "SourceMethod", StringComparison.Ordinal) &&
               string.Equals(edge.TargetMethod, "TargetMethod", StringComparison.Ordinal);
    }
}
