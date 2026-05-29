using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.AttributeCluster;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.Orleans.Graph.Tests;

[ClassDataSource<TestAttributeClusterApplication>(Shared = SharedType.PerTestSession)]
public class AttributeClusterTests(TestAttributeClusterApplication fixture)
{
    private readonly TestAttributeClusterApplication _fixture = fixture;

    [Test]
    public async Task AttributeGraph_AllowsConfiguredTransitionsAsync()
    {
        var result = await _fixture.Cluster.Client
            .GetGrain<IAttributeClusterGrainA>("1")
            .CallB();

        result.ShouldBe(1);
    }

    [Test]
    public async Task AttributeGraph_DisallowsMissingTransitionAsync()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await _fixture.Cluster.Client
                .GetGrain<IAttributeClusterGrainA>("1")
                .CallC();
        });

        exception.Message.ShouldStartWith("Transition from");
    }

    [Test]
    public async Task AttributeGraph_AllowsReentrancyAsync()
    {
        var result = await _fixture.Cluster.Client
            .GetGrain<IAttributeClusterGrainB>("1")
            .ReentrantCall();

        result.ShouldBe(1);
    }

    [Test]
    public void AttributeGraph_ProducesExpectedPolicyDiagram()
    {
        var manager = _fixture.Cluster.Client.ServiceProvider.GetRequiredService<GrainTransitionManager>();
        var diagram = manager.GeneratePolicyMermaidDiagram();

        diagram.ShouldContain("IAttributeClusterGrainA");
        diagram.ShouldContain("IAttributeClusterGrainB");
    }

    [Test]
    public void AttributeGraph_LiveDiagramHighlightsUsage()
    {
        var manager = _fixture.Cluster.Client.ServiceProvider.GetRequiredService<GrainTransitionManager>();

        var history = new CallHistory();
        history.Push(new OutCall(
            null,
            null,
            typeof(IAttributeClusterGrainA).FullName!,
            typeof(IAttributeClusterGrainB).FullName!,
            nameof(IAttributeClusterGrainA.CallB),
            nameof(IAttributeClusterGrainA.CallB)));
        history.Push(new InCall(null, null, typeof(IAttributeClusterGrainB).FullName!, nameof(IAttributeClusterGrainB.MethodB)));

        var diagram = manager.GenerateLiveMermaidDiagram(history);

        diagram.ShouldContain("==>");
        diagram.ShouldContain("hits: 1");
    }
}
