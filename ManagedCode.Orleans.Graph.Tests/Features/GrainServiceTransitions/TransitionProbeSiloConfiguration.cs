using ManagedCode.Orleans.Graph.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.Features.GrainServiceTransitions;

public sealed class TransitionProbeSiloConfiguration : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.Services.AddSingleton<TransitionProbeObservation>();
        siloBuilder.AddGrainService<TransitionProbeGrainService>();
        siloBuilder.AddOrleansGraph(configureGraph =>
        {
            configureGraph.AddGrainServiceTransition<TransitionProbeGrainService, ITransitionProbeGrain>(
                nameof(ITransitionProbeGrain.AllowedAsync),
                nameof(ITransitionProbeGrain.AlsoAllowedAsync));
        });
    }
}
