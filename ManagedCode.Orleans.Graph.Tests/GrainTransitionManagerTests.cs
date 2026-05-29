using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains;
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
    public void IsTransitionAllowed_UsesSourceIncomingMethodForOutgoingGrainCall()
    {
        var graph = GrainCallsBuilder.Create()
            .AllowClientCallGrain<IGrainA>()
            .And()
            .From<IGrainA>()
            .To<IGrainB>()
            .MethodByName(nameof(IGrainA.MethodB2), nameof(IGrainB.MethodC2))
            .And()
            .Build();

        var grainAId = GrainId.Create("graina", "source-method");
        var grainBId = GrainId.Create("grainb", "source-method");

        var callHistory = new CallHistory();
        callHistory.Push(new OutCall(null, grainAId, Constants.ClientCallerId, typeof(IGrainA).FullName!, nameof(IGrainA.MethodB2)));
        callHistory.Push(new InCall(null, grainAId, typeof(IGrainA).FullName!, nameof(IGrainA.MethodB2)));
        callHistory.Push(new OutCall(grainAId, grainBId, typeof(IGrainA).FullName!, typeof(IGrainB).FullName!, nameof(IGrainB.MethodC2), nameof(IGrainA.MethodB2)));
        callHistory.Push(new InCall(grainAId, grainBId, typeof(IGrainB).FullName!, nameof(IGrainB.MethodC2)));

        graph.IsTransitionAllowed(callHistory).ShouldBeTrue();
    }

    [Test]
    public void GetObservedGraph_ReturnsVerticesAndClientAndNestedGrainEdges()
    {
        var grainAId = GrainId.Create("graina", "observed");
        var grainBId = GrainId.Create("grainb", "observed");

        var callHistory = new CallHistory();
        callHistory.Push(new OutCall(null, grainAId, Constants.ClientCallerId, typeof(IGrainA).FullName!, nameof(IGrainA.MethodB1)));
        callHistory.Push(new InCall(null, grainAId, typeof(IGrainA).FullName!, nameof(IGrainA.MethodB1)));
        callHistory.Push(new OutCall(grainAId, grainBId, typeof(IGrainA).FullName!, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1), nameof(IGrainA.MethodB1)));
        callHistory.Push(new InCall(grainAId, grainBId, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        var graph = GrainTransitionManager.GetObservedGraph(callHistory);
        var edges = graph.Edges;

        graph.Vertices.Count.ShouldBe(3);
        graph.Vertices.ShouldContain(vertex => vertex.Id == Constants.ClientCallerId);
        graph.Vertices.ShouldContain(vertex => vertex.Id == typeof(IGrainA).FullName);
        graph.Vertices.ShouldContain(vertex => vertex.Id == typeof(IGrainB).FullName);
        edges.Count.ShouldBe(2);
        edges.ShouldContain(edge =>
            edge.Source == Constants.ClientCallerId &&
            edge.Target == typeof(IGrainA).FullName &&
            edge.SourceMethod == Constants.AnyMethod &&
            edge.TargetMethod == nameof(IGrainA.MethodB1) &&
            edge.Count == 1);
        edges.ShouldContain(edge =>
            edge.Source == typeof(IGrainA).FullName &&
            edge.Target == typeof(IGrainB).FullName &&
            edge.SourceMethod == nameof(IGrainA.MethodB1) &&
            edge.TargetMethod == nameof(IGrainB.MethodB1) &&
            edge.Count == 1);
    }

    [Test]
    public void GetLatestObservedCall_ReturnsCurrentIncomingPair()
    {
        var grainAId = GrainId.Create("graina", "latest");
        var grainBId = GrainId.Create("grainb", "latest");

        var callHistory = new CallHistory();
        callHistory.Push(new OutCall(null, grainAId, Constants.ClientCallerId, typeof(IGrainA).FullName!, nameof(IGrainA.MethodB1)));
        callHistory.Push(new InCall(null, grainAId, typeof(IGrainA).FullName!, nameof(IGrainA.MethodB1)));
        callHistory.Push(new OutCall(grainAId, grainBId, typeof(IGrainA).FullName!, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1), nameof(IGrainA.MethodB1)));
        callHistory.Push(new InCall(grainAId, grainBId, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        var edge = GrainTransitionManager.GetLatestObservedCall(callHistory);

        edge.ShouldNotBeNull();
        edge.Source.ShouldBe(typeof(IGrainA).FullName);
        edge.Target.ShouldBe(typeof(IGrainB).FullName);
        edge.SourceMethod.ShouldBe(nameof(IGrainA.MethodB1));
        edge.TargetMethod.ShouldBe(nameof(IGrainB.MethodB1));
    }

    [Test]
    public void GetObservedGraph_RejectsBaseGrainIdentity()
    {
        var grainAId = GrainId.Create("graina", "concrete-implementation");

        var callHistory = new CallHistory();
        callHistory.Push(new OutCall(
            null,
            grainAId,
            Constants.ClientCallerId,
            typeof(Grain).FullName!,
            nameof(IGrainA.MethodB1)));
        callHistory.Push(new InCall(
            null,
            grainAId,
            typeof(Grain).FullName!,
            nameof(IGrainA.MethodB1)));

        var exception = Should.Throw<InvalidOperationException>(() => GrainTransitionManager.GetObservedGraph(callHistory));

        exception.Message.ShouldContain("base Grain");
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
        edges[0].Transitions.Single().ShouldBe(new GrainTransition(Constants.AnyMethod, Constants.AnyMethod));
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

        var grainAId = GrainId.Create("graina", "live-policy");
        var grainBId = GrainId.Create("grainb", "live-policy");

        var callHistory = new CallHistory();
        callHistory.Push(new OutCall(null, grainAId, Constants.ClientCallerId, typeof(IGrainA).FullName!, nameof(IGrainA.MethodB1)));
        callHistory.Push(new InCall(null, grainAId, typeof(IGrainA).FullName!, nameof(IGrainA.MethodB1)));
        callHistory.Push(new OutCall(grainAId, grainBId, typeof(IGrainA).FullName!, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));
        callHistory.Push(new InCall(grainAId, grainBId, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        var diagram = graph.GenerateLiveMermaidDiagram(callHistory);

        diagram.ShouldContain("==>");
        diagram.ShouldContain(nameof(IGrainB.MethodB1));
        diagram.ShouldNotContain("all");
    }

    [Test]
    public void GenerateLiveMermaidDiagram_RendersObservedCallsWithoutPolicyEdges()
    {
        var graph = GrainCallsBuilder.Create()
            .AllowAll()
            .Build();

        var callHistory = new CallHistory();
        callHistory.Push(new OutCall(null, null, Constants.ClientCallerId, typeof(IGrainA).FullName!, nameof(IGrainA.MethodB1)));
        callHistory.Push(new InCall(null, null, typeof(IGrainA).FullName!, nameof(IGrainA.MethodB1)));

        var diagram = graph.GenerateLiveMermaidDiagram(callHistory);

        diagram.ShouldContain("ORLEANS_GRAIN_CLIENT");
        diagram.ShouldContain("IGrainA");
        diagram.ShouldContain("==>");
        diagram.ShouldContain("hits: 1");
    }

    [Test]
    public void GenerateObservedGraphMermaidDiagram_RendersObservedOnlyMethodLabelsAndUsageCounts()
    {
        var edges = new[]
        {
            ObservedGrainCall.Create(
                typeof(IGrainA).FullName!,
                typeof(IGrainB).FullName!,
                nameof(IGrainA.MethodA1),
                nameof(IGrainB.MethodB1)),
            ObservedGrainCall.Create(
                typeof(IGrainA).FullName!,
                typeof(IGrainB).FullName!,
                nameof(IGrainA.MethodB1),
                nameof(IGrainB.MethodC2))
        };

        var graph = GrainTransitionManager.BuildObservedGraph(edges);
        var diagram = GrainTransitionManager.GenerateObservedGraphMermaidDiagram(graph);

        diagram.ShouldContain("graph LR");
        diagram.ShouldContain("IGrainA");
        diagram.ShouldContain("IGrainB");
        diagram.ShouldContain("==>");
        diagram.ShouldContain($"{nameof(IGrainA.MethodA1)}->{nameof(IGrainB.MethodB1)}");
        diagram.ShouldContain($"{nameof(IGrainA.MethodB1)}->{nameof(IGrainB.MethodC2)}");
        diagram.ShouldContain("hits: 2");
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
