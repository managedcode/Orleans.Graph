using ManagedCode.Orleans.Graph.Models;
using Xunit;

namespace ManagedCode.Orleans.Graph.Tests;

public class DirectedGraphTests
{
    [Fact]
    public void AddTransition_PreservesMultipleRules()
    {
        var graph = new DirectedGraph(true);
        graph.AddTransition("source", "target", new GrainTransition("Method1", "MethodA"));
        graph.AddTransition("source", "target", new GrainTransition("Method2", "MethodB"));

        Assert.True(graph.IsTransitionAllowed("source", "target", "Method1", "MethodA"));
        Assert.True(graph.IsTransitionAllowed("source", "target", "Method2", "MethodB"));
    }

    [Fact]
    public void HasReentrantTransition_ReturnsTrueForSelfLoop()
    {
        var graph = new DirectedGraph(true);
        graph.AddTransition("source", "source", new GrainTransition("*", "*", IsReentrant: true));

        Assert.True(graph.HasReentrantTransition("source", "source"));
    }
}
