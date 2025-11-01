![Orleans Graph](https://raw.githubusercontent.com/managedcode/Orleans.Graph/main/logo.png)

# Orleans Graph

[![NuGet](https://badge.fury.io/nu/ManagedCode.Orleans.Graph.svg)](https://www.nuget.org/packages/ManagedCode.Orleans.Graph)
[![CI](https://github.com/managedcode/Orleans.Graph/actions/workflows/ci.yml/badge.svg)](https://github.com/managedcode/Orleans.Graph/actions/workflows/ci.yml)
[![CodeQL](https://github.com/managedcode/Orleans.Graph/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/managedcode/Orleans.Graph/actions/workflows/codeql-analysis.yml)

**Declare the grain-to-grain traffic you allow, block everything else, and see the graph in real time.**

## What You Get
- Fluent builder *and* attributes for defining permitted grain hops
- Incoming/outgoing filters that stop unauthorised calls before your code runs
- Deadlock detection with opt-in reentrancy
- Mermaid diagrams for both configured policy and live call history

## Quick Start
```csharp
// Silo
builder.AddOrleansGraph(
    configureGraph: graph =>
    {
        graph.AddGrainTransition<IOrderGrain, IPaymentGrain>()
             .Method(o => o.SubmitAsync(default!), p => p.ChargeAsync(default!))
             .WithReentrancy()
             .And()
             .AllowClientCallGrain<IOrderGrain>();
    });

// Client – attributes + defaults are enough
clientBuilder.AddOrleansGraph();
```

```csharp
[AllowClientCall]
[AllowGrainCall(typeof(IPaymentGrain), AllowAllMethods = true, AllowReentrancy = true)]
public interface IOrderGrain : IGrainWithStringKey
{
    Task SubmitAsync(Order dto);
}

[AllowSelfReentrancy]
[AllowGrainCall(typeof(IOrderGrain), SourceMethods = new[] { nameof(IPaymentGrain.ChargeAsync) }, TargetMethods = new[] { nameof(IOrderGrain.SubmitAsync) })]
public interface IPaymentGrain : IGrainWithStringKey
{
    Task ChargeAsync(Payment payment);
}

public record Payment;
```

## Visualise the Graph (Mermaid)
```csharp
var manager = services.GetRequiredService<GrainTransitionManager>();
var policy = manager.GeneratePolicyMermaidDiagram();

var history = new CallHistory();
history.Push(new OutCall(null, null, typeof(IOrderGrain).FullName!, typeof(IPaymentGrain).FullName!, "SubmitAsync"));
history.Push(new InCall(null, null, typeof(IPaymentGrain).FullName!, "ChargeAsync"));
var live = manager.GenerateLiveMermaidDiagram(history);
```
`-->` means allowed, `-.->` reentrant, `==>` active edge with `hits: N`.

## Runtime Guarantees
- Outgoing filter records every hop; incoming filter enforces the graph
- Deadlocks are detected unless the edge is marked reentrant
- Client → grain rules reuse the same builder/attribute API

## Tests & Coverage
```bash
dotnet format Orleans.Graph.slnx
dotnet test Orleans.Graph.slnx --configuration Release -p:CollectCoverage=true -p:CoverletOutput=coverage/
```
Automated suites cover fluent policies, attribute discovery (AppDomain scan), attribute-only TestCluster, and graph internals (~86 % line / 81 % branch).

## License
MIT – see [LICENSE](LICENSE).
