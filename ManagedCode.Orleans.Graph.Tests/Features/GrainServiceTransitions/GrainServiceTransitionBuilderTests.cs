using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using ManagedCode.Orleans.Graph.Tests.Features.GrainServiceTransitions;

namespace ManagedCode.Orleans.Graph.Tests;

public sealed class GrainServiceTransitionBuilderTests
{
    [Test]
    public void AddsConcreteServiceSourceAndOnlyTheRequestedTargets()
    {
        var manager = GrainCallsBuilder.Create()
            .AddGrainServiceTransition<TransitionProbeGrainService, ITransitionProbeGrain>(
                nameof(ITransitionProbeGrain.AllowedAsync), nameof(ITransitionProbeGrain.AlsoAllowedAsync))
            .Build();

        IsAllowed(manager, nameof(ITransitionProbeGrain.AllowedAsync)).ShouldBeTrue();
        IsAllowed(manager, nameof(ITransitionProbeGrain.AlsoAllowedAsync)).ShouldBeTrue();
        IsAllowed(manager, nameof(ITransitionProbeGrain.DeniedAsync)).ShouldBeFalse();
        IsAllowed(manager, nameof(ITransitionProbeGrain.AllowedAsync).ToLowerInvariant()).ShouldBeFalse();
    }

    [Test]
    public void AddingServiceRulesKeepsExistingApplicationTransitions()
    {
        var builder = GrainCallsBuilder.Create();
        builder.AddGrainTransition<IGrainA, IGrainB>()
            .MethodByName(nameof(IGrainA.MethodA1), nameof(IGrainB.MethodB1));
        builder.AddGrainServiceTransition<TransitionProbeGrainService, ITransitionProbeGrain>(
            nameof(ITransitionProbeGrain.AllowedAsync));

        var manager = builder.Build();
        IsAllowed(manager, nameof(ITransitionProbeGrain.AllowedAsync)).ShouldBeTrue();
        IsExistingApplicationTransitionAllowed(manager).ShouldBeTrue();
    }

    [Test]
    public void RejectsInvalidTargetListsBeforeAddingAnyTransition()
    {
        var nullBuilder = GrainCallsBuilder.Create();
        Should.Throw<ArgumentNullException>(() => nullBuilder.AddGrainServiceTransition<TransitionProbeGrainService, ITransitionProbeGrain>(null!));
        AssertInvalid([]);
        AssertInvalid([null!]);
        AssertInvalid([string.Empty]);
        AssertInvalid([" \t"]);

        var builder = GrainCallsBuilder.Create();
        Should.Throw<ArgumentException>(() => builder.AddGrainServiceTransition<TransitionProbeGrainService, ITransitionProbeGrain>(
            nameof(ITransitionProbeGrain.DeniedAsync), "  "));

        var manager = builder.Build();
        IsAllowed(manager, nameof(ITransitionProbeGrain.DeniedAsync)).ShouldBeFalse();
    }

    private static void AssertInvalid(string[] targetMethods)
    {
        var builder = GrainCallsBuilder.Create();
        Should.Throw<ArgumentException>(() => builder.AddGrainServiceTransition<TransitionProbeGrainService, ITransitionProbeGrain>(targetMethods));
    }

    private static bool IsExistingApplicationTransitionAllowed(GrainTransitionManager manager)
    {
        var history = new CallHistory();
        var sourceId = GrainId.Create("graina", "existing-transition");
        var targetId = GrainId.Create("grainb", "existing-transition");
        history.Push(new OutCall(sourceId, targetId, typeof(IGrainA).FullName!, typeof(IGrainB).FullName!,
            nameof(IGrainB.MethodB1), nameof(IGrainA.MethodA1)));
        history.Push(new InCall(sourceId, targetId, typeof(IGrainB).FullName!, nameof(IGrainB.MethodB1)));

        return manager.IsTransitionAllowed(history);
    }

    private static bool IsAllowed(GrainTransitionManager manager, string targetMethod)
    {
        var history = new CallHistory();
        history.Push(new OutCall(
            GrainId.Create("service", "transition-test"),
            GrainId.Create("probe", "transition-test"),
            typeof(TransitionProbeGrainService).FullName!,
            typeof(ITransitionProbeGrain).FullName!,
            targetMethod,
            Constants.AnyMethod));
        history.Push(new InCall(
            GrainId.Create("service", "transition-test"),
            GrainId.Create("probe", "transition-test"),
            typeof(ITransitionProbeGrain).FullName!,
            targetMethod));

        return manager.IsTransitionAllowed(history);
    }
}
