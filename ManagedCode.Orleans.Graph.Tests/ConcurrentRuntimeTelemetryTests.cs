using System.Diagnostics;
using System.Text.Json;
using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests;

public sealed class ConcurrentRuntimeTelemetryTests
{
    private const int ConcurrentGrains = 48;
    private const int ProfileRounds = 30;
    private const int RegressionRounds = 2;
    private const string ProfilePathVariable = "ORLEANS_GRAPH_TELEMETRY_PROFILE_PATH";

    [Test]
    public async Task ConcurrentCallsReportEachRuntimeEdgeAsync()
    {
        var builder = new TestClusterBuilder(1);
        builder.AddSiloBuilderConfigurator<TelemetryObservationSiloConfiguration>();
        builder.AddClientBuilderConfigurator<TestRuntimeGraphClientConfigurations>();
        await using var cluster = builder.Build();
        await cluster.DeployAsync();

        var silo = cluster.Primary.ShouldNotBeNull();
        var observation = cluster.GetSiloServiceProvider(silo.SiloAddress).GetRequiredService<TelemetryObservation>();
        var client = cluster.Client;
        var worker = client.GetGrain<IOrleansGraphTelemetryWorker>(Constants.LiveGraphTelemetryGrainKey);
        var graph = client.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);
        var grains = Enumerable.Range(0, ConcurrentGrains)
            .Select(index => client.GetGrain<IGrainA>($"concurrent-telemetry-{index}"))
            .ToArray();

        await CallAllAsync(grains, 0);
        await worker.FlushAsync();
        await graph.ClearAsync();
        var profilePath = Environment.GetEnvironmentVariable(ProfilePathVariable);
        var rounds = profilePath is null ? RegressionRounds : ProfileRounds;

        using var process = Process.GetCurrentProcess();
        var startedCpu = process.TotalProcessorTime;
        var startedAllocations = GC.GetTotalAllocatedBytes(precise: true);
        var startedCalls = observation.Calls;
        var startedBatches = observation.Batches;
        var elapsedMilliseconds = new double[rounds];
        for (var index = 0; index < rounds; index++)
        {
            var start = Stopwatch.GetTimestamp();
            await CallAllAsync(grains, index);
            elapsedMilliseconds[index] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - startedAllocations;
        var cpuMilliseconds = (process.TotalProcessorTime - startedCpu).TotalMilliseconds;
        var calls = observation.Calls - startedCalls;
        var batches = observation.Batches - startedBatches;

        await worker.FlushAsync();
        var observed = await graph.GetObservedGraphAsync();
        observed.Edges.Count.ShouldBe(2);
        batches.ShouldBeGreaterThan(0);
        (calls + batches).ShouldBeLessThan(2 * rounds * ConcurrentGrains);
        observed.Edges.Single(edge => edge.Source == Constants.ClientCallerId &&
            edge.Target == typeof(IGrainA).FullName && edge.TargetMethod == nameof(IGrainA.MethodB1))
            .Count.ShouldBe(rounds * ConcurrentGrains);
        observed.Edges.Single(edge => edge.Source == typeof(IGrainA).FullName &&
            edge.Target == typeof(IGrainB).FullName && edge.SourceMethod == nameof(IGrainA.MethodB1) &&
            edge.TargetMethod == nameof(IGrainB.MethodB1)).Count.ShouldBe(rounds * ConcurrentGrains);

        if (!string.IsNullOrWhiteSpace(profilePath))
        {
            await File.WriteAllTextAsync(profilePath, JsonSerializer.Serialize(new
            {
                Rounds = rounds,
                ConcurrentGrains,
                Calls = calls,
                Batches = batches,
                CpuMilliseconds = cpuMilliseconds,
                AllocatedBytes = allocatedBytes,
                ElapsedMilliseconds = elapsedMilliseconds,
            }));
        }
    }

    private static async Task CallAllAsync(IGrainA[] grains, int input)
    {
        var results = await Task.WhenAll(grains.Select(grain => grain.MethodB1(input)));
        results.ShouldAllBe(result => result == input + 1);
    }

    public sealed class TelemetryObservationSiloConfiguration : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddOrleansGraph(graph => graph.AllowAll());
            siloBuilder.Services.AddSingleton<TelemetryObservation>();
            siloBuilder.Services.AddSingleton<IOutgoingGrainCallFilter>(services =>
                services.GetRequiredService<TelemetryObservation>());
        }
    }

    public sealed class TelemetryObservation : IOutgoingGrainCallFilter
    {
        private int _calls;
        private int _batches;

        public int Calls => Volatile.Read(ref _calls);
        public int Batches => Volatile.Read(ref _batches);

        public async Task Invoke(IOutgoingGrainCallContext context)
        {
            if (context.InterfaceMethod.DeclaringType == typeof(IOrleansGraphTelemetryWorker))
            {
                if (context.InterfaceMethod.Name == nameof(IOrleansGraphTelemetryWorker.RecordObservedCallAsync))
                {
                    Interlocked.Increment(ref _calls);
                }
                else if (context.InterfaceMethod.Name == nameof(IOrleansGraphTelemetryWorker.RecordObservedCallsAsync))
                {
                    Interlocked.Increment(ref _batches);
                }
            }
            await context.Invoke();
        }
    }
}
