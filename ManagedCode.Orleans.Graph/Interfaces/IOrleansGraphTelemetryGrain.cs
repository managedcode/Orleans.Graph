using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Interfaces;

public interface IOrleansGraphTelemetryGrain : IGrainWithStringKey
{
    Task MergeAsync(IReadOnlyCollection<ObservedGrainCall> edges);

    Task<ObservedGrainCallGraph> GetObservedGraphAsync();

    Task<string> GenerateLiveMermaidDiagramAsync();

    Task ClearAsync();
}
