using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Telemetry;

internal interface IObservedGrainCallSink
{
    void RecordObservedCall(ObservedGrainCall observedCall);

    void RecordObservedCalls(IReadOnlyCollection<ObservedGrainCall> observedCalls);
}
