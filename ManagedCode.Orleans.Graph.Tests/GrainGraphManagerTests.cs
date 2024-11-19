using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using Xunit;

namespace ManagedCode.Orleans.Graph.Tests;

public class GrainGraphManagerTests
{

[Fact]
public void IsTransitionAllowed_SingleValidTransition_ReturnsTrue()
{
    var graph = GrainCallsBuilder.Create()
        .From<IGrainA>()
        .To<IGrainB>()
        .AllMethods()
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, typeof(IGrainA).FullName, nameof(IGrainA.MethodA1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainB).FullName, nameof(IGrainB.MethodB1)));

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
    callHistory.Push(new Call(Direction.Out, typeof(IGrainA).FullName, nameof(IGrainA.MethodA1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainB).FullName, nameof(IGrainB.MethodB1)));

    Assert.False(graph.IsTransitionAllowed(callHistory));
}

[Fact]
public void IsTransitionAllowed_EmptyCallHistory_ReturnsFalse()
{
    var graph = GrainCallsBuilder.Create()
        .From<IGrainA>()
        .To<IGrainB>()
        .AllMethods()
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
    callHistory.Push(new Call(Direction.Out, typeof(IGrainA).FullName, nameof(IGrainA.MethodA1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainB).FullName, nameof(IGrainB.MethodB1)));

    Assert.True(graph.IsTransitionAllowed(callHistory));
}

[Fact]
public void IsTransitionAllowed_MethodRuleAllowed_ReturnsTrue()
{
    var graph = GrainCallsBuilder.Create()
        .From<IGrainA>()
        .To<IGrainB>()
        .Method(a => a.MethodA1(0), b => b.MethodB1(0))
        .And()
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, typeof(IGrainA).FullName, nameof(IGrainA.MethodA1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainB).FullName, nameof(IGrainB.MethodB1)));

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
    callHistory.Push(new Call(Direction.Out, typeof(IGrainA).FullName, nameof(IGrainA.MethodA1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainA).FullName, nameof(IGrainA.MethodA1)));

    Assert.True(graph.IsTransitionAllowed(callHistory));
}

[Fact]
public void IsTransitionAllowed_DisallowedTransition_ReturnsFalse()
{
    var graph = GrainCallsBuilder.Create()
        .From<IGrainA>()
        .To<IGrainB>()
        .AllMethods()
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, typeof(IGrainA).FullName, nameof(IGrainA.MethodA1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainC).FullName, nameof(IGrainC.MethodC1)));

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
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, typeof(IGrainA).FullName, nameof(IGrainA.MethodA1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainB).FullName, nameof(IGrainB.MethodB1)));
    callHistory.Push(new Call(Direction.Out, typeof(IGrainB).FullName, nameof(IGrainB.MethodB1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainC).FullName, nameof(IGrainC.MethodC1)));

    Assert.True(graph.IsTransitionAllowed(callHistory));
}

[Fact]
public void IsTransitionAllowed_InvalidMethodRule_ReturnsFalse()
{
    var graph = GrainCallsBuilder.Create()
        .From<IGrainA>()
        .To<IGrainB>()
        .Method(a => a.MethodA1(0), b => b.MethodB1(0))
        .And()
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, typeof(IGrainA).FullName, nameof(IGrainA.MethodA1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainB).FullName, "MethodC2"));

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
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, typeof(IGrainA).FullName, nameof(IGrainA.MethodA1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainB).FullName, nameof(IGrainB.MethodB1)));
    callHistory.Push(new Call(Direction.Out, typeof(IGrainB).FullName, nameof(IGrainB.MethodB1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainC).FullName, nameof(IGrainC.MethodC1)));
    callHistory.Push(new Call(Direction.Out, typeof(IGrainC).FullName, nameof(IGrainC.MethodC1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainA).FullName, nameof(IGrainA.MethodA1)));

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
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, typeof(IGrainA).FullName, nameof(IGrainA.MethodA1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainB).FullName, nameof(IGrainB.MethodB1)));
    callHistory.Push(new Call(Direction.Out, typeof(IGrainB).FullName, nameof(IGrainB.MethodB1)));
    callHistory.Push(new Call(Direction.In, typeof(IGrainC).FullName, nameof(IGrainC.MethodC1)));

    Assert.True(graph.IsTransitionAllowed(callHistory));
}
}