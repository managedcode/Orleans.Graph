using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Attributes;
using Xunit;

namespace ManagedCode.Orleans.Graph.Tests;

public class AttributeGraphConfiguratorTests
{
    [Fact]
    public void AttributeConfigurator_BuildsExpectedGraph()
    {
        var builder = new GrainCallsBuilder();
        AttributeGraphConfigurator.ApplyFromAssemblies(builder, new[] { typeof(IAttributeGrainA).Assembly });

        var manager = builder.Build();

        var clientHistory = new CallHistory();
        clientHistory.Push(new InCall(null, null, typeof(IAttributeGrainA).FullName!, nameof(IAttributeGrainA.MethodA1)));
        clientHistory.Push(new OutCall(null, null, Constants.ClientCallerId, typeof(IAttributeGrainA).FullName!, nameof(IAttributeGrainA.MethodA1)));

        Assert.True(manager.IsTransitionAllowed(clientHistory));

        var reentrantHistory = new CallHistory();
        reentrantHistory.Push(new OutCall(null, null, typeof(IAttributeGrainB).FullName!, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));
        reentrantHistory.Push(new InCall(null, null, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));

        Assert.True(manager.IsTransitionAllowed(reentrantHistory));
    }

    [Fact]
    public void AttributeConfigurator_ScansAllAssemblies_WhenAssembliesNotSpecified()
    {
        var builder = new GrainCallsBuilder();
        AttributeGraphConfigurator.ApplyFromAssemblies(builder, null);

        var manager = builder.Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IAttributeGrainA).FullName!, nameof(IAttributeGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));

        Assert.True(manager.IsTransitionAllowed(callHistory));
    }

    [Fact]
    public void AttributeConfigurator_RespectsMethodPairs()
    {
        var builder = new GrainCallsBuilder();
        AttributeGraphConfigurator.ApplyFromAssemblies(builder, new[] { typeof(IAttributeGrainC).Assembly });

        var manager = builder.Build();

        var allowed = new CallHistory();
        allowed.Push(new Call(null, null, Direction.Out, typeof(IAttributeGrainC).FullName!, nameof(IAttributeGrainC.MethodSpecial)));
        allowed.Push(new Call(null, null, Direction.In, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));

        Assert.True(manager.IsTransitionAllowed(allowed));

        var rejected = new CallHistory();
        rejected.Push(new Call(null, null, Direction.Out, typeof(IAttributeGrainC).FullName!, nameof(IAttributeGrainC.MethodOther)));
        rejected.Push(new Call(null, null, Direction.In, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));

        Assert.False(manager.IsTransitionAllowed(rejected));
    }

    [Fact]
    public void AttributeConfigurator_ClientAttributeWithTargetType_AllowsClientCalls()
    {
        var builder = new GrainCallsBuilder();
        AttributeGraphConfigurator.ApplyFromAssemblies(builder, new[] { typeof(IAttributeGateway).Assembly });

        var manager = builder.Build();

        var history = new CallHistory();
        history.Push(new InCall(null, null, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));
        history.Push(new OutCall(null, null, Constants.ClientCallerId, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));

        Assert.True(manager.IsTransitionAllowed(history));
    }
}
