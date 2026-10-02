using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.SystemTargetCluster;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.Orleans.Graph.Tests;

[ClassDataSource<SystemTargetTestCluster>(Shared = SharedType.PerClass)]
[NotInParallel(SystemTargetTestProtocol.SharedClusterKey)]
[Timeout(SystemTargetTestProtocol.TestTimeoutMilliseconds)]
public class SystemTargetTrackingTests(SystemTargetTestCluster fixture)
{
    [Test]
    public void EarlyInitializationRpcRunsBeforeActivationWithoutGraphHistory(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // KeyLoad AC-ROUTE-003/006: the real system-target transport must not depend on ordinary telemetry grains.
        foreach (var silo in fixture.Cluster.Silos)
        {
            var observation = fixture.Cluster.GetSiloServiceProvider(silo.SiloAddress)
                .GetRequiredService<SystemTargetProbeObservation>();
            observation.InitializationFailure.ShouldBeNull();
            var reply = observation.InitializationReply.ShouldNotBeNull();
            reply.ExecutedBeforeGrainServicesCompleted.ShouldBeTrue();
            reply.HighestCompletedStage.ShouldBeGreaterThanOrEqualTo(ServiceLifecycleStage.RuntimeInitialize);
            reply.HighestCompletedStage.ShouldBeLessThan(ServiceLifecycleStage.RuntimeGrainServices);
            reply.ApplicationHistoryDepth.ShouldBe(0);
            reply.ContainsSystemTargetHistory.ShouldBeFalse();
        }
    }

    [Test]
    public async Task NativeServiceCallsFromSiloCodeAreNotTrackedAsync(CancellationToken cancellationToken)
    {
        using var deadline = CreateDeadline(cancellationToken);
        var client = fixture.PrimaryServices.GetRequiredService<SystemTargetProbeClient>();
        foreach (var silo in fixture.Cluster.Silos)
        {
            var observation = fixture.Cluster.GetSiloServiceProvider(silo.SiloAddress)
                .GetRequiredService<SystemTargetProbeObservation>();
            var before = observation.Executions;
            var reply = await client.ProbeAsync(silo.SiloAddress, deadline.Token);
            reply.ExecutedBeforeGrainServicesCompleted.ShouldBeFalse();
            reply.ApplicationHistoryDepth.ShouldBe(0);
            reply.ContainsSystemTargetHistory.ShouldBeFalse();
            observation.Executions.ShouldBe(before + 1);
        }
    }

    [Test]
    public async Task NativeServiceCallsFromOrdinaryGrainsPreserveApplicationHistoryAsync(CancellationToken cancellationToken)
    {
        using var deadline = CreateDeadline(cancellationToken);
        var reply = await fixture.CreateCaller().CallServiceAsync(deadline.Token).WaitAsync(deadline.Token);
        reply.ExecutedBeforeGrainServicesCompleted.ShouldBeFalse();
        reply.ApplicationHistoryDepth.ShouldBeGreaterThan(0);
        reply.ContainsSystemTargetHistory.ShouldBeFalse();
    }

    [Test]
    public async Task ApplicationTransitionsStillAllowConfiguredAndDenyMissingEdgesAsync(CancellationToken cancellationToken)
    {
        using var deadline = CreateDeadline(cancellationToken);
        var caller = fixture.CreateCaller();
        (await caller.CallAllowedApplicationAsync(SystemTargetTestProtocol.ApplicationInput).WaitAsync(deadline.Token))
            .ShouldBe(SystemTargetTestProtocol.ApplicationResult);
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            caller.CallDeniedApplicationAsync(SystemTargetTestProtocol.ApplicationInput).WaitAsync(deadline.Token));
        exception.Message.ShouldStartWith(SystemTargetTestProtocol.TransitionDeniedPrefix);
    }

    [Test]
    public async Task ExplicitTrackingChecksIncomingSystemTargetCallsBeforeExecutionAsync(CancellationToken cancellationToken)
    {
        var client = fixture.PrimaryServices.GetRequiredService<SystemTargetProbeClient>();
        await AssertExplicitTrackingDeniedAsync(token => client.ProbeAsync(fixture.Cluster.Primary!.SiloAddress, token), cancellationToken);
    }

    [Test]
    public async Task ExplicitTrackingChecksOutgoingSystemTargetCallsBeforeExecutionAsync(CancellationToken cancellationToken)
    {
        using var deadline = CreateDeadline(cancellationToken);
        var caller = fixture.CreateCaller();
        // Prime the ordinary activation and telemetry worker before intentionally tracking runtime calls.
        await caller.CallServiceAsync(deadline.Token).WaitAsync(deadline.Token);
        await AssertExplicitTrackingDeniedAsync(token => caller.CallServiceAsync(token).WaitAsync(token), cancellationToken);
    }

    private async Task AssertExplicitTrackingDeniedAsync(Func<CancellationToken, Task<SystemTargetProbeReply>> invoke, CancellationToken cancellationToken)
    {
        var configuration = fixture.PrimaryServices.GetRequiredService<GraphCallFilterConfig>();
        var observation = fixture.PrimaryServices.GetRequiredService<SystemTargetProbeObservation>();
        var before = observation.Executions;
        using var deadline = CreateDeadline(cancellationToken);
        configuration.TrackOrleansCalls = true;
        try
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(() => invoke(deadline.Token));
            exception.Message.ShouldStartWith(SystemTargetTestProtocol.TransitionDeniedPrefix);
            exception.Message.ShouldContain(typeof(ISystemTargetProbe).FullName!);
            observation.Executions.ShouldBe(before);
        }
        finally
        {
            configuration.TrackOrleansCalls = false;
        }
    }

    private static CancellationTokenSource CreateDeadline(CancellationToken cancellationToken)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(SystemTargetTestProtocol.RpcTimeout);
        return deadline;
    }
}
