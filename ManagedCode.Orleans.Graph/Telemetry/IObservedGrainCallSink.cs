using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Telemetry;

internal interface IObservedGrainCallSink
{
    void RecordObservedEdges(IReadOnlyCollection<ObservedGrainCallEdge> edges);
}
