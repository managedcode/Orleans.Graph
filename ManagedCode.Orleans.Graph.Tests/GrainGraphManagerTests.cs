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
        .AddGrain<IGrainA>()
        .AddGrain<IGrainB>()
        .From<IGrainA>()
        .To<IGrainB>()
        .AllMethods()
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, "IGrainA", "MethodA1"));
    callHistory.Push(new Call(Direction.In, "IGrainB", "MethodB1"));

    Assert.True(graph.IsTransitionAllowed(callHistory));
}

[Fact]
public void IsTransitionAllowed_InvalidTransition_ReturnsFalse()
{
    var graph = GrainCallsBuilder.Create()
        .AddGrain<IGrainA>()
        .AddGrain<IGrainB>()
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, "IGrainA", "MethodA1"));
    callHistory.Push(new Call(Direction.In, "IGrainB", "MethodB1"));

    Assert.False(graph.IsTransitionAllowed(callHistory));
}

[Fact]
public void IsTransitionAllowed_EmptyCallHistory_ReturnsFalse()
{
    var graph = GrainCallsBuilder.Create()
        .AddGrain<IGrainA>()
        .AddGrain<IGrainB>()
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
        .AddGrain<IGrainB>()
        .From<IGrainA>()
        .To<IGrainB>()
        .WithReentrancy()
        .And()
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, "IGrainA", "MethodA1"));
    callHistory.Push(new Call(Direction.In, "IGrainB", "MethodB1"));

    Assert.True(graph.IsTransitionAllowed(callHistory));
}

[Fact]
public void IsTransitionAllowed_MethodRuleAllowed_ReturnsTrue()
{
    var graph = GrainCallsBuilder.Create()
        .AddGrain<IGrainA>()
        .AddGrain<IGrainB>()
        .From<IGrainA>()
        .To<IGrainB>()
        .Method<IGrainA, IGrainB>(a => a.MethodA1(0), b => b.MethodB1(0))
        .And()
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, "IGrainA", "MethodA1"));
    callHistory.Push(new Call(Direction.In, "IGrainB", "MethodB1"));

    Assert.True(graph.IsTransitionAllowed(callHistory));
}

[Fact]
public void IsTransitionAllowed_SelfLoopTransition_ReturnsTrue()
{
    var graph = GrainCallsBuilder.Create()
        .AddGrain<IGrainA>()
        .From<IGrainA>()
        .To<IGrainA>()
        .AllMethods()
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, "IGrainA", "MethodA1"));
    callHistory.Push(new Call(Direction.In, "IGrainA", "MethodA1"));

    Assert.True(graph.IsTransitionAllowed(callHistory));
}

[Fact]
public void IsTransitionAllowed_DisallowedTransition_ReturnsFalse()
{
    var graph = GrainCallsBuilder.Create()
        .AddGrain<IGrainA>()
        .AddGrain<IGrainB>()
        .From<IGrainA>()
        .To<IGrainB>()
        .AllMethods()
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, "IGrainA", "MethodA1"));
    callHistory.Push(new Call(Direction.In, "IGrainC", "MethodC1"));

    Assert.False(graph.IsTransitionAllowed(callHistory));
}

[Fact]
public void IsTransitionAllowed_MultipleValidTransitions_ReturnsTrue()
{
    var graph = GrainCallsBuilder.Create()
        .AddGrain<IGrainA>()
        .AddGrain<IGrainB>()
        .AddGrain<IGrainC>()
        .From<IGrainA>()
        .To<IGrainB>()
        .AllMethods()
        .And()
        .From<IGrainB>()
        .To<IGrainC>()
        .AllMethods()
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, "IGrainA", "MethodA1"));
    callHistory.Push(new Call(Direction.In, "IGrainB", "MethodB1"));
    callHistory.Push(new Call(Direction.Out, "IGrainB", "MethodB1"));
    callHistory.Push(new Call(Direction.In, "IGrainC", "MethodC1"));

    Assert.True(graph.IsTransitionAllowed(callHistory));
}

[Fact]
public void IsTransitionAllowed_InvalidMethodRule_ReturnsFalse()
{
    var graph = GrainCallsBuilder.Create()
        .AddGrain<IGrainA>()
        .AddGrain<IGrainB>()
        .From<IGrainA>()
        .To<IGrainB>()
        .Method<IGrainA, IGrainB>(a => a.MethodA1(0), b => b.MethodB1(0))
        .And()
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, "IGrainA", "MethodA1"));
    callHistory.Push(new Call(Direction.In, "IGrainB", "MethodC2"));

    Assert.False(graph.IsTransitionAllowed(callHistory));
}

[Fact]
public void IsTransitionAllowed_GroupTransition_ReturnsTrue()
{
    var graph = GrainCallsBuilder.Create()
        .Group("Group1")
        .AddGrain<IGrainA>()
        .AddGrain<IGrainB>()
        .AllowCallsWithin()
        .And()
        .Build();

    var callHistory = new CallHistory();
    callHistory.Push(new Call(Direction.Out, "IGrainA", "MethodA1"));
    callHistory.Push(new Call(Direction.In, "IGrainB", "MethodB1"));

    Assert.True(graph.IsTransitionAllowed(callHistory));
}

}