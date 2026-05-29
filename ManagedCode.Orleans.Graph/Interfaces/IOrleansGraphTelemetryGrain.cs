using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Interfaces;

public interface IOrleansGraphTelemetryGrain : IGrainWithStringKey
{
    Task MergeAsync(IReadOnlyCollection<ObservedGrainCallEdge> edges);

    Task<IReadOnlyCollection<ObservedGrainCallEdge>> GetEdgesAsync();

    Task<string> GenerateLiveMermaidDiagramAsync();

    Task ClearAsync();
}
