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

Register the client-side outgoing filter when Orleans clients should participate in the same call-history tracking.

```csharp
clientBuilder.AddOrleansGraph();
```

## Observe Mode

Use `AllowAll()` when you want to discover real traffic before enforcing a strict policy. In this mode, unconfigured transitions are allowed, but the filters still send observed calls to stateless telemetry workers. Those workers aggregate calls and periodically flush them into an in-memory telemetry grain used by the live graph.

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

var observedEdges = await telemetry.GetEdgesAsync();
var liveMermaidDiagram = await telemetry.GenerateLiveMermaidDiagramAsync();
```

`AllowAll()` is not required for live telemetry. It only changes enforcement behavior: missing transitions are allowed instead of blocked. Orleans.Graph internal telemetry calls are excluded by default so the graph shows application traffic. Set `TrackOrleansGraphInternalCalls = true` only when debugging the telemetry pipeline itself.

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

Generate configured-policy and live-call Mermaid diagrams. The live graph is also available from the in-memory telemetry grain populated by the filters.

```csharp
var manager = serviceProvider.GetRequiredService<GrainTransitionManager>();

var policyDiagram = manager.GeneratePolicyMermaidDiagram();
var liveDiagram = manager.GenerateLiveMermaidDiagram(callHistory);
```

```csharp
var telemetry = grainFactory.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);
var liveEdges = await telemetry.GetEdgesAsync();
var liveGraph = await telemetry.GenerateLiveMermaidDiagramAsync();
```

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
