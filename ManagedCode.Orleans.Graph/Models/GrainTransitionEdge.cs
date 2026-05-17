namespace ManagedCode.Orleans.Graph.Models;

public sealed record GrainTransitionEdge(string Source, string Target, IReadOnlyCollection<GrainTransition> Transitions);
