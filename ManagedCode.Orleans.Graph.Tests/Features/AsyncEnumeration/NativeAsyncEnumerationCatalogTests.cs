namespace ManagedCode.Orleans.Graph.Tests.Features.AsyncEnumeration;

[ClassDataSource<AsyncEnumerationTestCluster>(Shared = SharedType.PerTestSession)]
public sealed class NativeAsyncEnumerationCatalogTests(AsyncEnumerationTestCluster fixture)
{
    private static readonly TimeSpan _testOperationDeadline = TimeSpan.FromSeconds(10);

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
