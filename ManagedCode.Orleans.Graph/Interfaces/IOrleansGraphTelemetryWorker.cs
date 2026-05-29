using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Interfaces;

public interface IOrleansGraphTelemetryWorker : IGrainWithStringKey
{
    Task RecordObservedCallAsync(ObservedGrainCall observedCall);

    Task RecordObservedCallsAsync(IReadOnlyCollection<ObservedGrainCall> observedCalls);

    Task FlushAsync();
}
