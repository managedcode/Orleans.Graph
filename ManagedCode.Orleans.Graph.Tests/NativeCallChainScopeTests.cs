using ManagedCode.Orleans.Graph.Tests.Cluster.Grains;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

namespace ManagedCode.Orleans.Graph.Tests;

[ClassDataSource<TestRuntimeGraphClusterApplication>(Shared = SharedType.PerTestSession)]
public class NativeCallChainScopeTests(TestRuntimeGraphClusterApplication fixture)
{
    private IReadOnlyCycleRoot GetRoot() =>
        fixture.Cluster.Client.GetGrain<IReadOnlyCycleRoot>(Guid.NewGuid().ToString("N"));

    [Test]
    public async Task InheritedScopeDoesNotPermitAnUnregisteredPeerCallbackAsync()
    {
        var failure = await Should.ThrowAsync<InvalidOperationException>(GetRoot().WriteThroughPeerWithInheritedScopeAsync);
        failure.Message.ShouldStartWith("Deadlock detected.");
    }

    [Test]
    public async Task ExplicitChildScopePermitsItsOwnNormalCallbackAsync() =>
        (await GetRoot().WriteThroughScopedPeerAgainAsync()).ShouldBe(ReadOnlyCycleRoot.Value);

    [Test]
    public async Task SuppressionDoesNotReuseParentPermissionAsync()
    {
        var failure = await Should.ThrowAsync<InvalidOperationException>(GetRoot().WriteThroughPeerWithSuppressedScopeAsync);
        failure.Message.ShouldStartWith("Deadlock detected.");
    }

    [Test]
    public async Task NewChildGuidDoesNotReuseParentPermissionAsync()
    {
        var failure = await Should.ThrowAsync<InvalidOperationException>(GetRoot().WriteThroughPeerWithNewScopeAsync);
        failure.Message.ShouldStartWith("Deadlock detected.");
    }

    [Test]
    public async Task CompletedScopeDoesNotPermitALaterUnscopedCallbackAsync()
    {
        var failure = await Should.ThrowAsync<InvalidOperationException>(GetRoot().WriteAfterScopeAsync);
        failure.Message.ShouldStartWith("Deadlock detected.");
    }

    [Test]
    public async Task FailedScopeDoesNotPermitALaterUnscopedCallbackAsync()
    {
        var failure = await Should.ThrowAsync<InvalidOperationException>(GetRoot().WriteAfterFailedScopeAsync);
        failure.Message.ShouldStartWith("Deadlock detected.");
    }

    [Test]
    public async Task NestedScopeDeduplicatesAndRestoresOuterPermissionAsync() =>
        (await GetRoot().WriteThroughNestedSelfScopesAsync()).ShouldBe(ReadOnlyCycleRoot.Value);

    [Test]
    public async Task ParallelRpcBranchesKeepTheirParentScopeIsolatedAsync() =>
        (await GetRoot().WriteThroughParallelScopedPeersAsync()).ShouldBe(ReadOnlyCycleRoot.Value);

    [Test]
    public async Task NativeCodecRoundTripPreservesExactCallbackPermissionAsync() =>
        (await GetRoot().WriteThroughSerializedScopeAsync()).ShouldBe(ReadOnlyCycleRoot.Value);

    [Test]
    public async Task ScopeCannotAuthorizeAnotherActorWithTheSameInterfaceAsync()
    {
        var failure = await Should.ThrowAsync<InvalidOperationException>(GetRoot().WriteThroughDifferentScopedRootAsync);
        failure.Message.ShouldStartWith("Deadlock detected.");
    }

    [Test]
    public async Task AwaitedOperationWithoutAResultUsesTheSameNativeScopeAsync() =>
        await GetRoot().WriteWithoutResultInScopeAsync();

    [Test]
    public async Task DetachedWorkCannotReuseItsParentScopeAsync()
    {
        var failure = await Should.ThrowAsync<InvalidOperationException>(GetRoot().WriteWithDetachedScopeAsync);
        failure.Message.ShouldStartWith("Deadlock detected.");
    }
}
