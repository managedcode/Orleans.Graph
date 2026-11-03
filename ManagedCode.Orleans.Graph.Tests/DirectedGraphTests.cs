using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Tests;

public class DirectedGraphTests
{
    [Test]
    public void AddVertex_KeepsIsolatedVertexWhenOtherEdgesAreAdded()
    {
        var graph = new DirectedGraph();
        graph.AddVertex("isolated");
        graph.AddVertex("isolated");
        graph.AddTransition("source", "target", new GrainTransition("From", "To"));

        graph.HasVertex("isolated").ShouldBeTrue();
        graph.GetNeighbors("isolated").ShouldBeEmpty();
        graph.GetVertices().Count().ShouldBe(3);
        graph.GetAllEdges().Count().ShouldBe(1);
        graph.HasCycle().ShouldBeFalse();
    }

    [Test]
    public void AddTransition_PreservesMultipleRules()
    {
        var graph = new DirectedGraph(true);
        graph.AddTransition("source", "target", new GrainTransition("Method1", "MethodA"));
        graph.AddTransition("source", "target", new GrainTransition("Method2", "MethodB"));

        graph.IsTransitionAllowed("source", "target", "Method1", "MethodA").ShouldBeTrue();
        graph.IsTransitionAllowed("source", "target", "Method2", "MethodB").ShouldBeTrue();
    }

    [Test]
    public void HasReentrantTransition_ReturnsTrueForSelfLoop()
    {
        var graph = new DirectedGraph(true);
        graph.AddTransition("source", "source", new GrainTransition(Constants.AnyMethod, Constants.AnyMethod, IsReentrant: true));

        graph.HasReentrantTransition("source", "source").ShouldBeTrue();
    }

    [Test]
    public void AddTransition_AllowsCycleThroughReentrantEdge()
    {
        var graph = new DirectedGraph(true);
        graph.AddTransition("source", "target", new GrainTransition(Constants.AnyMethod, Constants.AnyMethod, IsReentrant: true));
        graph.AddTransition("target", "source", new GrainTransition(Constants.AnyMethod, Constants.AnyMethod));

        graph.HasCycle().ShouldBeFalse();
    }

    [Test]
    public void AddTransition_RejectsCycleInDeepPolicyAndRollsBack()
    {
        const int Depth = 100_000;
        const string First = "root";
        var graph = new DirectedGraph(allowSelfLoops: true);
        var blocking = new GrainTransition(Constants.AnyMethod, Constants.AnyMethod);
        var reentrant = blocking with { IsReentrant = true };
        var previous = First;
        for (var index = 0; index < Depth; index++)
        {
            var next = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            graph.AddTransition(previous, next, reentrant);
            previous = next;
        }

        // Populate the deep policy without running a full cycle check for each edge.
        foreach (var (_, _, transitions) in graph.GetAllEdges())
        {
            transitions.Add(blocking);
        }

        graph.AddTransition(First, First, blocking);
        graph.HasCycle().ShouldBeFalse();

        Should.Throw<InvalidOperationException>(() => graph.AddTransition(previous, First, blocking));
        graph.HasEdge(previous, First).ShouldBeFalse();
        graph.HasCycle().ShouldBeFalse();
    }
}
