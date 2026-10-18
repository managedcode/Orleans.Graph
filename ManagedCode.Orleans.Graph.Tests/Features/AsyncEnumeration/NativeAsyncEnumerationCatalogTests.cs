using ManagedCode.Orleans.Graph.Features.AsyncEnumeration;

namespace ManagedCode.Orleans.Graph.Tests.Features.AsyncEnumeration;

[ClassDataSource<AsyncEnumerationTestCluster>(Shared = SharedType.PerTestSession)]
public sealed class NativeAsyncEnumerationCatalogTests(AsyncEnumerationTestCluster fixture)
{
    private static readonly TimeSpan _testOperationDeadline = TimeSpan.FromSeconds(10);

    [Test]
    public async Task ClosedGenericInheritedMethodUsesNativeCatalogAcrossPullsAsync()
    {
        var streamKey = Guid.NewGuid().ToString("N");
        var source = fixture.Cluster.Client.GetGrain<IClosedInheritedAsyncEnumerationSource>(streamKey);
        var stream = source.InheritedAsync(streamKey);
        var request = (IAsyncEnumerableRequest<StreamItem>)stream;
        request.GetInterfaceType().ShouldBe(typeof(IInheritedAsyncEnumeration<StreamItem>));
        request.GetMethod().DeclaringType.ShouldBe(typeof(IInheritedAsyncEnumeration<StreamItem>));
        request.GetMethod().ContainsGenericParameters.ShouldBeFalse();
        var builder = new AsyncEnumerationFactoryCatalogBuilder();
        builder.AddGrainInterface(typeof(IClosedInheritedAsyncEnumerationSource));
        builder.AddGrainInterface(typeof(IInheritedAsyncEnumeration<>));
        var catalog = builder.Build();
        catalog.Contains(request.GetMethod()).ShouldBeTrue();
        catalog.Contains(typeof(IInheritedAsyncEnumeration<>).GetMethod(nameof(IInheritedAsyncEnumeration<StreamItem>.InheritedAsync))!)
            .ShouldBeFalse();
        request.MaxBatchSize = 1;
        using var deadline = new CancellationTokenSource(_testOperationDeadline);
        await using (var enumerator = stream.GetAsyncEnumerator(deadline.Token))
        {
            for (var sequence = 0; sequence < 3; sequence++)
            {
                (await enumerator.MoveNextAsync()).ShouldBeTrue();
                var item = enumerator.Current;
                item.StreamKey.ShouldBe(streamKey);
                item.Sequence.ShouldBe(sequence);
                item.Caller.ShouldBe(typeof(IInheritedAsyncEnumeration<StreamItem>).FullName);
                item.CallerMethod.ShouldBe(nameof(IInheritedAsyncEnumeration<StreamItem>.InheritedAsync));
                item.SameHistoryBranch.ShouldBeTrue();
                item.HistoryDepth.ShouldBe(2);
                if (sequence > 0)
                {
                    var probe = item.Probe.ShouldNotBeNull();
                    probe.EdgeSource.ShouldBe(typeof(IInheritedAsyncEnumeration<StreamItem>).FullName);
                    probe.EdgeSourceMethod.ShouldBe(nameof(IInheritedAsyncEnumeration<StreamItem>.InheritedAsync));
                    probe.EdgeTarget.ShouldBe(typeof(IAsyncEnumerationLeaf).FullName);
                    probe.EdgeTargetMethod.ShouldBe(nameof(IAsyncEnumerationLeaf.ObserveAsync));
                    probe.NativeSourceId.ShouldBe(source.GetGrainId().ToString());
                }
            }
            (await enumerator.MoveNextAsync()).ShouldBeFalse();
        }
        AsyncEnumerationExecutionCounters.Read(streamKey).ShouldBe(new StreamCounts(1, 1));
        AsyncEnumerationExecutionCounters.LeafAttemptCount(streamKey).ShouldBe(2);
        AsyncEnumerationExecutionCounters.LeafCallCount(streamKey).ShouldBe(2);
        AsyncEnumerationExecutionCounters.Remove(streamKey);
    }

    [Test]
    public async Task ClosedGenericInheritedMethodKeepsMissingLeafEdgeClosedAsync()
    {
        var streamKey = Guid.NewGuid().ToString("N");
        var stream = fixture.Cluster.Client.GetGrain<IClosedInheritedAsyncEnumerationSource>(streamKey).DeniedInheritedAsync(streamKey);
        ((IAsyncEnumerableRequest<StreamItem>)stream).MaxBatchSize = 1;
        using var deadline = new CancellationTokenSource(_testOperationDeadline);
        await using var enumerator = stream.GetAsyncEnumerator(deadline.Token);
        (await enumerator.MoveNextAsync()).ShouldBeTrue();
        enumerator.Current.Caller.ShouldBe(typeof(IInheritedAsyncEnumeration<StreamItem>).FullName);
        enumerator.Current.CallerMethod.ShouldBe(nameof(IInheritedAsyncEnumeration<StreamItem>.DeniedInheritedAsync));
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => enumerator.MoveNextAsync().AsTask());
        failure.Message.ShouldContain(typeof(IInheritedAsyncEnumeration<StreamItem>).FullName!);
        AsyncEnumerationExecutionCounters.Read(streamKey).ShouldBe(new StreamCounts(1, 1));
        AsyncEnumerationExecutionCounters.LeafAttemptCount(streamKey).ShouldBe(1);
        AsyncEnumerationExecutionCounters.LeafCallCount(streamKey).ShouldBe(0);
        AsyncEnumerationExecutionCounters.Remove(streamKey);
    }

    [Test]
    public async Task RegisteredCatalogWrapsNativeRequestAndRejectsUnknownOrOpenMethodsAsync()
    {
        var streamKey = Guid.NewGuid().ToString("N");
        using var deadline = new CancellationTokenSource(_testOperationDeadline);
        var result = await fixture.Cluster.Client.GetGrain<IAsyncEnumerationCatalogProbe>(streamKey)
            .InspectAsync(streamKey, deadline.Token);

        result.InterfaceName.ShouldBe(typeof(IAsyncEnumerationSource).FullName);
        result.MethodName.ShouldBe(nameof(IAsyncEnumerationSource.AllowedAsync));
        result.Argument.ShouldBe(streamKey);
        result.BatchSize.ShouldBe(1);
        result.NativeMetadataDelegated.ShouldBeTrue();
        result.ArgumentMutationDelegated.ShouldBeTrue();
        result.BatchMutationDelegated.ShouldBeTrue();
        result.OptionsMutationDelegated.ShouldBeTrue();
        result.CancellationMutationDelegated.ShouldBeTrue();
        result.TargetMutationDelegated.ShouldBeTrue();
        result.InvokeFailureDelegated.ShouldBeTrue();
        result.UnknownMethodRejected.ShouldBeTrue();
        result.OpenGenericUnregistered.ShouldBeTrue();
    }
}
