using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Interfaces;

public interface IOrleansGraphTelemetryGrain : IGrainWithStringKey
{
    Task MergeObservedCallsAsync(IReadOnlyCollection<ObservedGrainCall> observedCalls);

    Task<ObservedGrainCallGraph> GetObservedGraphAsync();

    Task<string> GenerateLiveMermaidDiagramAsync();

    Task ClearAsync();
}
