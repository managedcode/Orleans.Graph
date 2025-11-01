using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using Xunit;

namespace ManagedCode.Orleans.Graph.Tests;

public class GrainTransitionManagerTests
{

    [Fact]
    public void IsTransitionAllowed_SingleValidTransition_ReturnsTrue()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .AllMethods()
            .And()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        Assert.True(graph.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void IsTransitionAllowed_InvalidTransition_ReturnsFalse()
    {
        var graph = GrainCallsBuilder.Create()
            .AddGrain<IGrainA>()
            .And()
            .AddGrain<IGrainB>()
            .And()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        Assert.False(graph.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void IsTransitionAllowed_EmptyCallHistory_ReturnsFalse()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .AllMethods()
            .And()
            .Build();

        var callHistory = new CallHistory();

        Assert.False(graph.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void IsTransitionAllowed_ReentrancyAllowed_ReturnsTrue()
    {
        var graph = GrainCallsBuilder.Create()
            .AddGrain<IGrainA>()
            .And()
            .AddGrain<IGrainB>()
            .And()
            .From<IGrainA>()
            .To<IGrainB>()
            .WithReentrancy()
            .And()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        Assert.True(graph.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void IsTransitionAllowed_PreservesMultipleMethodRules()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .Method(a => a.MethodA1(GraphParam.Any<int>()), b => b.MethodB1(GraphParam.Any<int>()))
            .MethodByName(nameof(IGrainA.MethodB1), nameof(IGrainB.MethodC2))
            .And()
            .Build();

        var firstAllowed = new CallHistory();
        firstAllowed.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        firstAllowed.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));
        Assert.True(graph.IsTransitionAllowed(firstAllowed));

        var secondAllowed = new CallHistory();
        secondAllowed.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodB1)));
        secondAllowed.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodC2)));
        Assert.True(graph.IsTransitionAllowed(secondAllowed));

        var disallowed = new CallHistory();
        disallowed.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodC1)));
        disallowed.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodC2)));
        Assert.False(graph.IsTransitionAllowed(disallowed));
    }

    [Fact]
    public void IsTransitionAllowed_AllowAllByDefault_AllowsMissingTransitions()
    {
        var graph = GrainCallsBuilder.Create()
            .AllowAll()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        Assert.True(graph.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void IsTransitionAllowed_DisallowAllAfterAllowAll_DeniesMissingTransitions()
    {
        var graph = GrainCallsBuilder.Create()
            .AllowAll()
            .DisallowAll()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        Assert.False(graph.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void IsTransitionAllowed_MethodRuleAllowed_ReturnsTrue()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .Method(a => a.MethodA1(GraphParam.Any<int>()), b => b.MethodB1(GraphParam.Any<int>()))
            .And()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        Assert.True(graph.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void IsTransitionAllowed_SelfLoopTransition_ReturnsTrue()
    {
        var graph = GrainCallsBuilder.Create()
            .AddGrain<IGrainA>()
            .WithReentrancy()
            .And()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));

        Assert.True(graph.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void IsTransitionAllowed_DisallowedTransition_ReturnsFalse()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .AllMethods()
            .And()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainC).FullName!, nameof(IGrainC.MethodC1)));

        Assert.False(graph.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void IsTransitionAllowed_MultipleValidTransitions_ReturnsTrue()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .AllMethods()
            .And()
            .From<IGrainB>()
            .To<IGrainC>()
            .AllMethods()
            .And()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainC).FullName!, nameof(IGrainC.MethodC1)));

        Assert.True(graph.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void IsTransitionAllowed_InvalidMethodRule_ReturnsFalse()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .Method(a => a.MethodA1(GraphParam.Any<int>()), b => b.MethodB1(GraphParam.Any<int>()))
            .And()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, "MethodC2"));

        Assert.False(graph.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void IsTransitionAllowed_DetectsComplexLoop_ThrowsExceptionOnBuild()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var graph = GrainCallsBuilder.Create()
                .From<IGrainA>()
                .To<IGrainB>()
                .AllMethods()
                .And()
                .From<IGrainB>()
                .To<IGrainC>()
                .AllMethods()
                .And()
                .From<IGrainC>()
                .To<IGrainD>()
                .AllMethods()
                .And()
                .From<IGrainD>()
                .To<IGrainA>()
                .AllMethods()
                .And()
                .Build();
        });

        Assert.Equal("Adding transition from ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces.IGrainD to ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces.IGrainA creates a cycle.", exception.Message);
    }

    [Fact]
    public void IsTransitionAllowed_DetectsSimpleLoop_ReturnsFalse()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .AllMethods()
            .And()
            .From<IGrainB>()
            .To<IGrainC>()
            .AllMethods()
            .And()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainC).FullName!, nameof(IGrainC.MethodC1)));
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainC).FullName!, nameof(IGrainC.MethodC1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));

        Assert.False(graph.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void IsTransitionAllowed_NoLoop_ReturnsTrue()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .AllMethods()
            .And()
            .From<IGrainB>()
            .To<IGrainC>()
            .AllMethods()
            .And()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainC).FullName!, nameof(IGrainC.MethodC1)));

        Assert.True(graph.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void DetectDeadlocks_IgnoresReentrantTransitions()
    {
        var graph = GrainCallsBuilder.Create()
            .AddGrain<IGrainA>()
            .WithReentrancy()
            .And()
            .Build();

        var grainId = GrainId.Create("test", nameof(IGrainA));

        var callHistory = new CallHistory();
        callHistory.Push(new OutCall(grainId, grainId, typeof(IGrainA).FullName!, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new InCall(grainId, grainId, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));

        Assert.False(graph.DetectDeadlocks(callHistory));
    }

    [Fact]
    public void GeneratePolicyMermaidDiagram_ProducesEdges()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .AllMethods()
            .And()
            .Build();

        var diagram = graph.GeneratePolicyMermaidDiagram();

        Assert.Contains("graph LR", diagram);
        Assert.Contains("[\"IGrainA\"]", diagram);
        Assert.Contains("[\"IGrainB\"]", diagram);
        Assert.Contains("all", diagram);
    }

    [Fact]
    public void GenerateLiveMermaidDiagram_HighlightsActiveEdges()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .AllMethods()
            .And()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new OutCall(null, null, typeof(IGrainA).FullName!, typeof(IGrainB).FullName!, nameof(IGrainA.MethodB1)));
        callHistory.Push(new InCall(null, null, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        var diagram = graph.GenerateLiveMermaidDiagram(callHistory);

        Assert.Contains("==>", diagram);
    }

    [Fact]
    public void GeneratePolicyMermaidDiagram_MarksReentrantEdges()
    {
        var graph = GrainCallsBuilder.Create()
            .AddGrain<IGrainA>()
            .WithReentrancy()
            .And()
            .Build();

        var diagram = graph.GeneratePolicyMermaidDiagram();

        Assert.Contains("-.->", diagram);
    }

    [Fact]
    public void GeneratePolicyMermaidDiagram_RendersMultipleMethodLabels()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .MethodByName(nameof(IGrainA.MethodA1), nameof(IGrainB.MethodB1))
            .MethodByName(nameof(IGrainA.MethodB1), nameof(IGrainB.MethodC2))
            .And()
            .Build();

        var diagram = graph.GeneratePolicyMermaidDiagram();

        Assert.Contains("<br/>", diagram);
    }

    [Fact]
    public void GenerateLiveMermaidDiagram_AppendsUsageCounts()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .AllMethods()
            .And()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new OutCall(null, null, typeof(IGrainA).FullName!, typeof(IGrainB).FullName!, nameof(IGrainA.MethodB1)));
        callHistory.Push(new InCall(null, null, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));
        callHistory.Push(new OutCall(null, null, typeof(IGrainA).FullName!, typeof(IGrainB).FullName!, nameof(IGrainA.MethodB1)));
        callHistory.Push(new InCall(null, null, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        var diagram = graph.GenerateLiveMermaidDiagram(callHistory);

        Assert.Contains("hits: 2", diagram);
    }
}
