using System;
using ManagedCode.Orleans.Graph.Filters;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;

namespace ManagedCode.Orleans.Graph.Extensions;

public static class OrleansGraphExtensions
{
    public static ISiloBuilder AddOrleansGraph(this ISiloBuilder builder)
    {
        return builder.AddOrleansGraph(_ => { });
    }

    public static ISiloBuilder AddOrleansGraph(this ISiloBuilder builder, Action<GraphCallFilterConfig> config)
    {
        var graphCallFilterConfig = new GraphCallFilterConfig();
        config.Invoke(graphCallFilterConfig);
        builder.Services.AddSingleton(graphCallFilterConfig);

        builder.AddIncomingGrainCallFilter<GraphIncomingGrainCallFilter>();
        builder.AddOutgoingGrainCallFilter<GraphOutgoingGrainCallFilter>();

        return builder;
    }

    public static ISiloBuilder CreateGraph(this ISiloBuilder builder, Action<IGrainCallsBuilder> graphBuilder)
    {
        var grainGraph = new GrainCallsBuilder();
        graphBuilder(grainGraph);
        var manager = grainGraph.Build();
        
        builder.ConfigureServices(services => services.AddSingleton(manager));
        return builder;
    }
}