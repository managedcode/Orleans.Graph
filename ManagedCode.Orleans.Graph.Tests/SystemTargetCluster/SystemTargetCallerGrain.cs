using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using Orleans.Placement;

namespace ManagedCode.Orleans.Graph.Tests.SystemTargetCluster;

[PreferLocalPlacement]
public sealed class SystemTargetCallerGrain(SystemTargetProbeClient client, ILocalSiloDetails localSilo,
    SystemTargetProbeObservation observation)
    : Grain, ISystemTargetCallerGrain
{
    public Task<SystemTargetProbeReply> CallServiceAsync(CancellationToken cancellationToken) =>
        client.ProbeAsync(localSilo.SiloAddress, cancellationToken);

    public async Task<SystemTargetCallbackReply> CallServiceCallbackAsync(bool refused, CancellationToken cancellationToken)
    {
        var caller = RequestContext.Get(Constants.CurrentCallerContextKey);
        var history = RequestContext.Get(Constants.RequestContextKey);
        var reply = await client.CallbackAsync(localSilo.SiloAddress, refused, cancellationToken);
        return reply with
        {
            ParentCallerRestored = Equals(caller, RequestContext.Get(Constants.CurrentCallerContextKey)),
            ParentHistoryRestored = ReferenceEquals(history, RequestContext.Get(Constants.RequestContextKey))
        };
    }

    public Task<SystemTargetCallbackReply> CallbackTargetAsync() => ObserveCallbackAsync();

    public Task<SystemTargetCallbackReply> RefusedCallbackTargetAsync() => ObserveCallbackAsync();

    private Task<SystemTargetCallbackReply> ObserveCallbackAsync()
    {
        observation.RecordCallbackExecution();
        var history = RequestContext.Get(Constants.RequestContextKey) as CallHistory
            ?? throw new InvalidOperationException("The native callback has no application history.");
        var edge = GrainTransitionManager.GetLatestObservedCall(history)
            ?? throw new InvalidOperationException("The native callback has no observed edge.");
        var sourceId = history.History.Peek().SourceId?.ToString()
            ?? throw new InvalidOperationException("The native callback has no source identity.");
        return Task.FromResult(new SystemTargetCallbackReply(edge.Source, edge.SourceMethod, sourceId,
            false, false, false, false, false));
    }

    public Task<int> CallAllowedApplicationAsync(int input) =>
        GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKey().ToString()).MethodB1(input);

    public Task<int> CallDeniedApplicationAsync(int input) =>
        GrainFactory.GetGrain<IGrainC>(this.GetPrimaryKey().ToString()).MethodC1(input);
}
