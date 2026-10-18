using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ManagedCode.Orleans.Graph.Tests.SystemTargetCluster;

public sealed class SystemTargetProbeGrainService(GrainId id, Silo silo, ILoggerFactory loggerFactory,
    ILocalSiloDetails localSilo, ISiloLifecycle lifecycle, SystemTargetProbeObservation observation) : GrainService(id, silo, loggerFactory), ISystemTargetProbe
{
    private IGrainFactory? _grainFactory;

    public override async Task Init(IServiceProvider serviceProvider)
    {
        await base.Init(serviceProvider);
        _grainFactory = serviceProvider.GetRequiredService<IGrainFactory>();
        using var deadline = new CancellationTokenSource(SystemTargetTestProtocol.RpcTimeout, TimeProvider.System);
        try
        {
            var client = serviceProvider.GetRequiredService<SystemTargetProbeClient>();
            observation.RecordInitialization(await client.ProbeAsync(localSilo.SiloAddress, deadline.Token));
        }
        catch (Exception exception)
        {
            // Keep startup healthy so the assertion reports the genuine pre-Active RPC failure and cleanup can run.
            observation.RecordInitializationFailure(exception, lifecycle.HighestCompletedStage);
        }
    }

    public async Task<SystemTargetCallbackReply> CallbackAsync(bool refused, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var caller = RequestContext.Get(Constants.CurrentCallerContextKey);
        var history = RequestContext.Get(Constants.RequestContextKey);
        var target = (_grainFactory ?? throw new InvalidOperationException("The probe service is not initialized."))
            .GetGrain<ISystemTargetCallerGrain>(Guid.Empty);
        var reply = refused ? await target.RefusedCallbackTargetAsync() : await target.CallbackTargetAsync();
        return reply with
        {
            SourceMatchesService = string.Equals(reply.NativeSourceId, ((IGrainContext)this).GrainId.ToString(), StringComparison.Ordinal),
            ServiceCallerRestored = Equals(caller, RequestContext.Get(Constants.CurrentCallerContextKey)),
            ServiceHistoryRestored = ReferenceEquals(history, RequestContext.Get(Constants.RequestContextKey))
        };
    }

    public Task<SystemTargetProbeReply> ProbeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        observation.RecordExecution();
        var history = RequestContext.Get(Constants.RequestContextKey) as CallHistory;
        var completedStage = lifecycle.HighestCompletedStage;
        var beforeCompletion = completedStage >= ServiceLifecycleStage.RuntimeInitialize
            && completedStage < ServiceLifecycleStage.RuntimeGrainServices;
        return Task.FromResult(new SystemTargetProbeReply(beforeCompletion,
            history?.History.Count ?? 0,
            history?.History.Any(call => call.Interface == typeof(ISystemTargetProbe).FullName) ?? false, completedStage));
    }
}
