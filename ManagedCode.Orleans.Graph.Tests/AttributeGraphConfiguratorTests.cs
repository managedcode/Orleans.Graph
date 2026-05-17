using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Attributes;

namespace ManagedCode.Orleans.Graph.Tests;

public class AttributeGraphConfiguratorTests
{
    [Test]
    public void AttributeConfigurator_BuildsExpectedGraph()
    {
        var builder = new GrainCallsBuilder();
        AttributeGraphConfigurator.ApplyFromAssemblies(builder, new[] { typeof(IAttributeGrainA).Assembly });

        var manager = builder.Build();

        var clientHistory = new CallHistory();
        clientHistory.Push(new InCall(null, null, typeof(IAttributeGrainA).FullName!, nameof(IAttributeGrainA.MethodA1)));
        clientHistory.Push(new OutCall(null, null, Constants.ClientCallerId, typeof(IAttributeGrainA).FullName!, nameof(IAttributeGrainA.MethodA1)));

        manager.IsTransitionAllowed(clientHistory).ShouldBeTrue();

        var reentrantHistory = new CallHistory();
        reentrantHistory.Push(new OutCall(null, null, typeof(IAttributeGrainB).FullName!, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));
        reentrantHistory.Push(new InCall(null, null, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));

        manager.IsTransitionAllowed(reentrantHistory).ShouldBeTrue();
    }

    [Test]
    public void AttributeConfigurator_ScansAllAssemblies_WhenAssembliesNotSpecified()
    {
        var builder = new GrainCallsBuilder();
        AttributeGraphConfigurator.ApplyFromAssemblies(builder, null);

        var manager = builder.Build();

        var callHistory = new CallHistory();
        callHistory.Push(new Call(null, null, Direction.Out, typeof(IAttributeGrainA).FullName!, nameof(IAttributeGrainA.MethodA1)));
        callHistory.Push(new Call(null, null, Direction.In, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));

        manager.IsTransitionAllowed(callHistory).ShouldBeTrue();
    }

    [Test]
    public void AttributeConfigurator_RespectsMethodPairs()
    {
        var builder = new GrainCallsBuilder();
        AttributeGraphConfigurator.ApplyFromAssemblies(builder, new[] { typeof(IAttributeGrainC).Assembly });

        var manager = builder.Build();

        var allowed = new CallHistory();
        allowed.Push(new Call(null, null, Direction.Out, typeof(IAttributeGrainC).FullName!, nameof(IAttributeGrainC.MethodSpecial)));
        allowed.Push(new Call(null, null, Direction.In, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));

        manager.IsTransitionAllowed(allowed).ShouldBeTrue();

        var rejected = new CallHistory();
        rejected.Push(new Call(null, null, Direction.Out, typeof(IAttributeGrainC).FullName!, nameof(IAttributeGrainC.MethodOther)));
        rejected.Push(new Call(null, null, Direction.In, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));

        manager.IsTransitionAllowed(rejected).ShouldBeFalse();
    }

    [Test]
    public void AttributeConfigurator_ClientAttributeWithTargetType_AllowsClientCalls()
    {
        var builder = new GrainCallsBuilder();
        AttributeGraphConfigurator.ApplyFromAssemblies(builder, new[] { typeof(IAttributeGateway).Assembly });

        var manager = builder.Build();

        var history = new CallHistory();
        history.Push(new InCall(null, null, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));
        history.Push(new OutCall(null, null, Constants.ClientCallerId, typeof(IAttributeGrainB).FullName!, nameof(IAttributeGrainB.MethodB1)));

        manager.IsTransitionAllowed(history).ShouldBeTrue();
    }
}
