using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.AttributeCluster;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ManagedCode.Orleans.Graph.Tests;

[Collection(nameof(TestAttributeClusterApplication))]
public class AttributeClusterTests(TestAttributeClusterApplication fixture)
{
    private readonly TestAttributeClusterApplication _fixture = fixture;

    [Fact]
    public async Task AttributeGraph_AllowsConfiguredTransitions()
    {
        var result = await _fixture.Cluster.Client
            .GetGrain<IAttributeClusterGrainA>("1")
            .CallB();

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task AttributeGraph_DisallowsMissingTransition()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _fixture.Cluster.Client
                .GetGrain<IAttributeClusterGrainA>("1")
                .CallC();
        });

        Assert.StartsWith("Transition from", exception.Message);
    }

    [Fact]
    public async Task AttributeGraph_AllowsReentrancy()
    {
        var result = await _fixture.Cluster.Client
            .GetGrain<IAttributeClusterGrainB>("1")
            .ReentrantCall();

        Assert.Equal(1, result);
    }

    [Fact]
    public void AttributeGraph_ProducesExpectedPolicyDiagram()
    {
        var manager = _fixture.Cluster.Client.ServiceProvider.GetRequiredService<GrainTransitionManager>();
        var diagram = manager.GeneratePolicyMermaidDiagram();

        Assert.Contains("IAttributeClusterGrainA", diagram);
        Assert.Contains("IAttributeClusterGrainB", diagram);
    }

    [Fact]
    public void AttributeGraph_LiveDiagramHighlightsUsage()
    {
        var manager = _fixture.Cluster.Client.ServiceProvider.GetRequiredService<GrainTransitionManager>();

        var history = new CallHistory();
        history.Push(new OutCall(null, null, typeof(IAttributeClusterGrainA).FullName!, typeof(IAttributeClusterGrainB).FullName!, nameof(IAttributeClusterGrainA.CallB)));
        history.Push(new InCall(null, null, typeof(IAttributeClusterGrainB).FullName!, nameof(IAttributeClusterGrainB.MethodB)));

        var diagram = manager.GenerateLiveMermaidDiagram(history);

        Assert.Contains("==>", diagram);
        Assert.Contains("hits: 1", diagram);
    }
}
