using ManagedCode.Orleans.Graph.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests.Features.GrainServiceTransitions;

[ClassDataSource<TransitionProbeTestCluster>(Shared = SharedType.PerClass)]
[Timeout(TransitionProbeProtocol.TestTimeoutMilliseconds)]
public sealed class NativeGrainServiceTransitionTests(TransitionProbeTestCluster fixture)
{
    [Test]
    public async Task NativeServiceCallbackUsesConcreteSourceAndOnlyConfiguredTargetsAsync(CancellationToken cancellationToken)
    {
        foreach (var silo in fixture.Cluster.Silos)
        {
            var observation = fixture.GetObservation(silo.SiloAddress);
            var outcome = await observation.Probe.WaitAsync(cancellationToken);
            outcome.FirstAllowedResult.ShouldBe(TransitionProbeProtocol.AllowedResult);
            outcome.FinalAllowedResult.ShouldBe(TransitionProbeProtocol.AllowedResult);
            outcome.DeniedBodyResult.ShouldBeNull();
            var denial = outcome.DeniedTransition.ShouldNotBeNull();
            denial.ShouldStartWith(TransitionProbeProtocol.DenialPrefix);
            denial.ShouldContain(typeof(TransitionProbeGrainService).FullName!);
            denial.ShouldContain(typeof(ITransitionProbeGrain).FullName!);
            denial.ShouldBe($"Transition from {typeof(TransitionProbeGrainService).FullName} to {typeof(ITransitionProbeGrain).FullName} is not allowed.");
        }

        var clientGrain = fixture.Cluster.Client.GetGrain<ITransitionProbeGrain>("direct-client");
        var clientDenial = await Should.ThrowAsync<InvalidOperationException>(
            () => clientGrain.AllowedAsync(cancellationToken));
        clientDenial.Message.ShouldStartWith(TransitionProbeProtocol.DenialPrefix);
        clientDenial.Message.ShouldContain(Constants.ClientCallerId);
        clientDenial.Message.ShouldContain(typeof(ITransitionProbeGrain).FullName!);

        var observations = fixture.Cluster.Silos
            .Select(silo => fixture.GetObservation(silo.SiloAddress))
            .ToArray();
        await fixture.DisposeAsync();
        foreach (var observation in observations)
        {
            await observation.Stopped.WaitAsync(cancellationToken);
        }
    }
}
