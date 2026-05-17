using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests;

public class GrainTransitionManagerTests
{

    [Test]
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

        graph.IsTransitionAllowed(callHistory).ShouldBeTrue();
    }

    [Test]
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

        graph.IsTransitionAllowed(callHistory).ShouldBeFalse();
    }

    [Test]
    public void IsTransitionAllowed_EmptyCallHistory_ReturnsFalse()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .AllMethods()
            .And()
            .Build();

        var callHistory = new CallHistory();

        graph.IsTransitionAllowed(callHistory).ShouldBeFalse();
    }

    [Test]
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

        graph.IsTransitionAllowed(callHistory).ShouldBeTrue();
    }

    [Test]
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
        graph.IsTransitionAllowed(firstAllowed).ShouldBeTrue();

        var secondAllowed = new CallHistory();
        secondAllowed.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodB1)));
        secondAllowed.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodC2)));
        graph.IsTransitionAllowed(secondAllowed).ShouldBeTrue();

        var disallowed = new CallHistory();
        disallowed.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodC1)));
        disallowed.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodC2)));
        graph.IsTransitionAllowed(disallowed).ShouldBeFalse();
    }

    [Test]
    public void IsTransitionAllowed_AllowAllByDefault_AllowsMissingTransitions()
    {
        var graph = GrainCallsBuilder.Create()
            .AllowAll()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        graph.IsTransitionAllowed(callHistory).ShouldBeTrue();
    }

    [Test]
    public void IsTransitionAllowed_DisallowAllAfterAllowAll_DeniesMissingTransitions()
    {
        var graph = GrainCallsBuilder.Create()
            .AllowAll()
            .DisallowAll()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IGrainA).FullName!, nameof(IGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        graph.IsTransitionAllowed(callHistory).ShouldBeFalse();
    }

    [Test]
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

        graph.IsTransitionAllowed(callHistory).ShouldBeTrue();
    }

    [Test]
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

        graph.IsTransitionAllowed(callHistory).ShouldBeTrue();
    }

    [Test]
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

        graph.IsTransitionAllowed(callHistory).ShouldBeFalse();
    }

    [Test]
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

        graph.IsTransitionAllowed(callHistory).ShouldBeTrue();
    }

    [Test]
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

        graph.IsTransitionAllowed(callHistory).ShouldBeFalse();
    }

    [Test]
    public void IsTransitionAllowed_DetectsComplexLoop_ThrowsExceptionOnBuild()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
        {
            GrainCallsBuilder.Create()
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

        exception.Message.ShouldBe("Adding transition from ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces.IGrainD to ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces.IGrainA creates a cycle.");
    }

    [Test]
    public void Build_AllowsCycleWhenOneEdgeIsReentrant()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .WithReentrancy()
            .And()
            .From<IGrainB>()
            .To<IGrainA>()
            .AllMethods()
            .And()
            .Build();

        graph.GeneratePolicyMermaidDiagram().ShouldContain("-.->");
    }

    [Test]
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

        graph.IsTransitionAllowed(callHistory).ShouldBeFalse();
    }

    [Test]
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

        graph.IsTransitionAllowed(callHistory).ShouldBeTrue();
    }

    [Test]
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

        graph.DetectDeadlocks(callHistory).ShouldBeFalse();
    }

    [Test]
    public void GeneratePolicyMermaidDiagram_ProducesEdges()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainA>()
            .To<IGrainB>()
            .AllMethods()
            .And()
            .Build();

        var diagram = graph.GeneratePolicyMermaidDiagram();

        diagram.ShouldContain("graph LR");
        diagram.ShouldContain("[\"IGrainA\"]");
        diagram.ShouldContain("[\"IGrainB\"]");
        diagram.ShouldContain("all");
    }

    [Test]
    public void GetPolicyEdges_ReturnsOrderedSnapshot()
    {
        var graph = GrainCallsBuilder.Create()
            .From<IGrainB>()
            .To<IGrainC>()
            .MethodByName(nameof(IGrainB.MethodB1), nameof(IGrainC.MethodC1))
            .And()
            .From<IGrainA>()
            .To<IGrainB>()
            .AllMethods()
            .And()
            .Build();

        var edges = graph.GetPolicyEdges().ToArray();

        edges.Length.ShouldBe(2);
        edges[0].Source.ShouldBe(typeof(IGrainA).FullName);
        edges[0].Target.ShouldBe(typeof(IGrainB).FullName);
        edges[0].Transitions.Single().ShouldBe(new GrainTransition("*", "*"));
        edges[1].Source.ShouldBe(typeof(IGrainB).FullName);
        edges[1].Target.ShouldBe(typeof(IGrainC).FullName);
        edges[1].Transitions.Single().ShouldBe(new GrainTransition(nameof(IGrainB.MethodB1), nameof(IGrainC.MethodC1)));
    }

    [Test]
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

        diagram.ShouldContain("==>");
    }

    [Test]
    public void GeneratePolicyMermaidDiagram_MarksReentrantEdges()
    {
        var graph = GrainCallsBuilder.Create()
            .AddGrain<IGrainA>()
            .WithReentrancy()
            .And()
            .Build();

        var diagram = graph.GeneratePolicyMermaidDiagram();

        diagram.ShouldContain("-.->");
    }

    [Test]
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

        diagram.ShouldContain("<br/>");
    }

    [Test]
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

        diagram.ShouldContain("hits: 2");
    }
}
