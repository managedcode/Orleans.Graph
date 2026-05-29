using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Interfaces;

public interface IOrleansGraphTelemetryWorker : IGrainWithStringKey
{
    Task RecordAsync(IReadOnlyCollection<ObservedGrainCall> edges);

    Task FlushAsync();
}
