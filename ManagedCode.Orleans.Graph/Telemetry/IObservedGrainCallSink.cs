using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Telemetry;

internal interface IObservedGrainCallSink
{
    void RecordObservedCalls(IReadOnlyCollection<ObservedGrainCall> edges);
}
