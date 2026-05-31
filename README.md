![Orleans Graph](https://raw.githubusercontent.com/managedcode/Orleans.Graph/main/logo.png)

# ManagedCode.Orleans.Graph

[![NuGet](https://badge.fury.io/nu/ManagedCode.Orleans.Graph.svg)](https://www.nuget.org/packages/ManagedCode.Orleans.Graph)
[![CI](https://github.com/managedcode/Orleans.Graph/actions/workflows/ci.yml/badge.svg)](https://github.com/managedcode/Orleans.Graph/actions/workflows/ci.yml)
[![CodeQL](https://github.com/managedcode/Orleans.Graph/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/managedcode/Orleans.Graph/actions/workflows/codeql-analysis.yml)

Grain-to-grain call policy enforcement for Microsoft Orleans applications.

The library lets a silo declare the grain calls it permits, blocks missing transitions before target grain code runs, and keeps enough request context to detect unsafe runtime call cycles.

## Features

- Fluent builder API for source grain, target grain, method, client-call, and reentrancy rules.
- Attribute-based graph configuration for colocating policies with grain contracts.
- Incoming and outgoing Orleans call filters for runtime enforcement.
- Deadlock detection for active grain call chains, with opt-in self-reentrancy.
- Mermaid policy and live-call diagrams.
- Policy edge snapshots through `GetPolicyEdges()` for diagnostics and custom visualizers.
- .NET 10, Orleans 10, central package management, TUnit tests, and CI coverage reporting.

## Requirements

- .NET SDK 10
- Microsoft Orleans 10

## Installation

```sh
dotnet add package ManagedCode.Orleans.Graph
```

## Silo Setup

Register the graph filters on the silo and configure allowed transitions.

```csharp
using ManagedCode.Orleans.Graph;
using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;

siloBuilder.AddOrleansGraph(graph =>
{
    graph.AllowClientCallGrain<IOrderGrain>();

    graph.AddGrainTransition<IOrderGrain, IPaymentGrain>()
        .Method(
            source => source.SubmitAsync(GraphParam.Any<Order>()),
            target => target.ChargeAsync(GraphParam.Any<Payment>()))
        .And();

    graph.AddGrain<IPaymentGrain>()
        .WithReentrancy();
});
```

Register the client-side outgoing filter when Orleans clients should participate in the same call-history tracking. This is only required for client-originated edges such as `ORLEANS_GRAIN_CLIENT -> IOrderGrain`. Grain-to-grain calls that start inside a silo, for example from another grain, timer, reminder, hosted service, or injected `IGrainFactory`, are tracked by the silo filters.

```csharp
clientBuilder.AddOrleansGraph();
```

## Observe Mode

Use `AllowAll()` when you want to discover real traffic before enforcing a strict policy. In this mode, unconfigured transitions are allowed, but the filters still send observed calls to stateless telemetry workers. Those workers aggregate calls and periodically flush them into an in-memory telemetry grain used by the live graph.

`AllowAll()` is useful for documenting expected traffic without blocking unconfigured transitions. It is not a requirement for live telemetry: the live graph is recorded in both enforcing and observe modes.

```csharp
using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;

siloBuilder.AddOrleansGraph(
    configureFilters: filters =>
    {
        filters.LiveGraphFlushPeriod = TimeSpan.FromSeconds(1);
    },
    configureGraph: graph =>
    {
        graph.AllowAll();
    });

clientBuilder.AddOrleansGraph();
```

After your app receives traffic, read the observed graph from the telemetry grain.

```csharp
var telemetry = grainFactory.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);

var observedGraph = await telemetry.GetObservedGraphAsync();
var liveMermaidDiagram = await telemetry.GenerateLiveMermaidDiagramAsync();
```

The live telemetry pipeline records one observed edge from the incoming side, after Orleans has both sides of the call pair. The filters send the edge to stateless telemetry workers, workers aggregate repeated calls in memory, and a timer flushes the aggregated counts into the telemetry grain. The timer does not keep stateless workers alive.

Activation-origin calls are attributed to the source grain activation. `RegisterGrainTimer` callbacks do not expose a source grain interface method, so their source method is recorded as `*`. Reminder callbacks are attributed to the source grain identity and keep `ReceiveReminder` as the source method instead of exposing `Orleans.IRemindable` as the graph vertex.

Stateless worker calls follow the same identity rules: grain-interface methods use the worker interface identity, while activation-origin callbacks use the concrete worker activation identity.

Orleans.Graph internal telemetry calls are excluded by default so the graph shows application traffic. Set `TrackOrleansGraphInternalCalls = true` only when debugging the telemetry pipeline itself.

```csharp
siloBuilder.AddOrleansGraph(
    configureFilters: filters =>
    {
        filters.TrackOrleansGraphInternalCalls = true;
    },
    configureGraph: graph =>
    {
        graph.AllowAll();
    });
```

## Attribute Setup

Attributes are scanned automatically from loaded assemblies. Pass explicit assemblies when startup should avoid scanning the full `AppDomain`.

```csharp
using ManagedCode.Orleans.Graph.Attributes;

[AllowClientCall]
[AllowGrainCall(
    typeof(IPaymentGrain),
    AllowAllMethods = false,
    SourceMethods = [nameof(IOrderGrain.SubmitAsync)],
    TargetMethods = [nameof(IPaymentGrain.ChargeAsync)])]
public interface IOrderGrain : IGrainWithStringKey
{
    Task SubmitAsync(Order order);
}

[AllowSelfReentrancy]
public interface IPaymentGrain : IGrainWithStringKey
{
    Task ChargeAsync(Payment payment);
}
```

## Diagnostics

Generate configured-policy and per-request Mermaid diagrams from the registered `GrainTransitionManager`. For the cluster-wide live graph, use the telemetry grain API because it reads the aggregated runtime graph from all filters and stateless workers.

```csharp
var manager = serviceProvider.GetRequiredService<GrainTransitionManager>();

var policyDiagram = manager.GeneratePolicyMermaidDiagram();
var liveDiagram = manager.GenerateLiveMermaidDiagram(callHistory);
```

```csharp
var telemetry = grainFactory.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);
var liveGraph = await telemetry.GetObservedGraphAsync();
var liveMermaidGraph = await telemetry.GenerateLiveMermaidDiagramAsync();
```

`liveGraph.Vertices` contains grain identities. `liveGraph.Edges` contains observed runtime transitions between those vertices, including `SourceMethod`, `TargetMethod`, hit count, and timestamps. The Mermaid API renders the same graph for visualization.

Runtime vertices are exact graph identities. The client is represented as `ORLEANS_GRAIN_CLIENT`; grain vertices use the concrete Orleans grain interface or implementation identity resolved from Orleans call context. When Orleans exposes no resolvable caller identity, the runtime graph records `UNKNOWN_CALLER` instead of guessing. The runtime graph does not fall back to the base `Grain` class. Source methods use `*` only when Orleans does not expose a grain interface method for the source callback.

Inspect the configured policy without parsing Mermaid.

```csharp
foreach (var edge in manager.GetPolicyEdges())
{
    Console.WriteLine($"{edge.Source} -> {edge.Target}: {edge.Transitions.Count} rule(s)");
}
```

Mermaid arrows:

- `-->` configured transition
- `-.->` reentrant transition
- `==>` active live-call edge

## Development

```sh
dotnet tool restore
dotnet restore Orleans.Graph.slnx
dotnet format Orleans.Graph.slnx
dotnet build Orleans.Graph.slnx --configuration Release --no-restore -p:RunAnalyzers=true
dotnet test --solution Orleans.Graph.slnx --configuration Release --no-build --verbosity normal
```

Coverage uses the local tool manifest:

```sh
dotnet tool run coverlet ManagedCode.Orleans.Graph.Tests/bin/Release/net10.0/ManagedCode.Orleans.Graph.Tests.dll \
  --target "dotnet" \
  --targetargs "test --project ManagedCode.Orleans.Graph.Tests/ManagedCode.Orleans.Graph.Tests.csproj --configuration Release --no-build --no-restore" \
  --format cobertura \
  --output artifacts/coverage/coverage.cobertura.xml \
  --exclude "[ManagedCode.Orleans.Graph.Tests]*" \
  --threshold 80 \
  --threshold-type line \
  --threshold-stat total
```

## License

MIT - see [LICENSE](LICENSE).
