using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Tests;

public class DirectedGraphTests
{
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
}
