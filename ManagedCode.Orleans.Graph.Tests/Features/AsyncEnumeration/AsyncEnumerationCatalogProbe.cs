using ManagedCode.Orleans.Graph.Features.AsyncEnumeration;
using Microsoft.Extensions.DependencyInjection;
using Orleans.CodeGeneration;
using Orleans.Serialization.Invocation;

namespace ManagedCode.Orleans.Graph.Tests.Features.AsyncEnumeration;

public sealed partial class AsyncEnumerationSourceGrain(IServiceProvider services)
{
    private static readonly AsyncEnumerationFactoryCatalog _closedSourceCatalog = CreateClosedSourceCatalog();
    private readonly AsyncEnumerationFactoryCatalog _catalog = services.GetRequiredService<AsyncEnumerationFactoryCatalog>();

    public async Task<CatalogDelegationResult> InspectAsync(string streamKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scope = AsyncEnumerationScope.Capture();
        using var original = CreateOriginalRequest(streamKey);
        var wrapped = (IAsyncEnumerableRequest<StreamItem>)_catalog.Wrap(original, scope);
        var metadata = PreservesMetadata(original, wrapped);
        var argument = MutatesArgument(original, wrapped);
        var target = SetRealTarget(original, wrapped);
        var batchSize = SetBatchSize(original, wrapped);
        var options = SetInvokeOptions(original, wrapped);
        var cancellation = CompareCancellationDelegation(streamKey, scope);
        using var unknown = CreateUnknownRequest(streamKey);
        var unknownRejected = RejectUnknownMethod(unknown, scope);
        var openMethod = typeof(IOpenGenericAsyncEnumeration).GetMethod(nameof(IOpenGenericAsyncEnumeration.OpenAsync))!;
        var openGenericUnregistered = !_closedSourceCatalog.Contains(openMethod);
        var invokeDelegated = await CompareInvokeFailuresAsync(original, wrapped, scope);

        return new CatalogDelegationResult(
            wrapped.GetInterfaceName(),
            wrapped.GetMethodName(),
            wrapped.GetArgument(0) as string ?? string.Empty,
            wrapped.MaxBatchSize,
            metadata,
            argument,
            batchSize,
            options,
            cancellation,
            target,
            invokeDelegated,
            unknownRejected,
            openGenericUnregistered);
    }

    private IAsyncEnumerableRequest<StreamItem> CreateOriginalRequest(string streamKey) =>
        (IAsyncEnumerableRequest<StreamItem>)GrainFactory.GetGrain<IAsyncEnumerationSource>(streamKey).AllowedAsync(streamKey);

    private IAsyncEnumerableRequest<StreamItem> CreateUnknownRequest(string streamKey) =>
        (IAsyncEnumerableRequest<StreamItem>)GrainFactory.GetGrain<IUnallowedAsyncEnumerationSource>(streamKey).StreamAsync(streamKey);

    private static bool PreservesMetadata(IAsyncEnumerableRequest<StreamItem> original, IAsyncEnumerableRequest<StreamItem> wrapped) =>
        wrapped.GetArgumentCount() == original.GetArgumentCount() &&
        wrapped.GetMethod() == original.GetMethod() &&
        wrapped.GetMethodName() == original.GetMethodName() &&
        wrapped.GetInterfaceName() == original.GetInterfaceName() &&
        wrapped.GetInterfaceType() == original.GetInterfaceType() &&
        wrapped.GetActivityName() == original.GetActivityName() &&
        wrapped.GetDefaultResponseTimeout() == original.GetDefaultResponseTimeout() &&
        wrapped.GetCancellationToken() == original.GetCancellationToken() &&
        wrapped.IsCancellable == original.IsCancellable &&
        wrapped.Options == original.Options;

    private static bool MutatesArgument(
        IAsyncEnumerableRequest<StreamItem> original,
        IAsyncEnumerableRequest<StreamItem> wrapped)
    {
        var argument = wrapped.GetArgument(0) as string
                       ?? throw new InvalidOperationException("Native stream request argument was not preserved.");
        wrapped.SetArgument(0, "catalog-probe-replaced");
        var delegated = string.Equals(original.GetArgument(0) as string, "catalog-probe-replaced", StringComparison.Ordinal);
        wrapped.SetArgument(0, argument);
        return delegated;
    }

    private bool SetRealTarget(
        IAsyncEnumerableRequest<StreamItem> original,
        IAsyncEnumerableRequest<StreamItem> wrapped)
    {
        var target = (ITargetHolder)GrainContext;
        var actualTarget = target.GetTarget();
        var targetWasUnbound = original.GetTarget() is null;
        wrapped.SetTarget(target);
        return targetWasUnbound && actualTarget is not null &&
               ReferenceEquals(original.GetTarget(), actualTarget) &&
               ReferenceEquals(wrapped.GetTarget(), actualTarget);
    }

    private static bool SetBatchSize(
        IAsyncEnumerableRequest<StreamItem> original,
        IAsyncEnumerableRequest<StreamItem> wrapped)
    {
        wrapped.MaxBatchSize = 1;
        return original.MaxBatchSize == 1;
    }

    private static bool SetInvokeOptions(
        IAsyncEnumerableRequest<StreamItem> original,
        IAsyncEnumerableRequest<StreamItem> wrapped)
    {
        var originalRequest = (IRequest)original;
        var wrappedRequest = (IRequest)wrapped;
        var previousOptions = originalRequest.Options;
        var readOnlyWasClear = (previousOptions & InvokeMethodOptions.ReadOnly) == 0;
        wrapped.AddInvokeMethodOptions(InvokeMethodOptions.ReadOnly);
        var expectedOptions = previousOptions | InvokeMethodOptions.ReadOnly;
        return readOnlyWasClear && originalRequest.Options == expectedOptions &&
               wrappedRequest.Options == expectedOptions;
    }

    private bool CompareCancellationDelegation(string streamKey, AsyncEnumerationScope scope)
    {
        using var wrappedCancellation = new CancellationTokenSource();
        using var controlCancellation = new CancellationTokenSource();
        using var wrappedRequest = CreateCancellableRequest(streamKey, wrappedCancellation.Token);
        using var controlRequest = CreateCancellableRequest(streamKey, controlCancellation.Token);
        var target = (ITargetHolder)GrainContext;
        wrappedRequest.SetTarget(target);
        controlRequest.SetTarget(target);
        var wrapped = (IAsyncEnumerableRequest<StreamItem>)_catalog.Wrap(wrappedRequest, scope);
        var initiallyActive = !wrapped.GetCancellationToken().IsCancellationRequested &&
                              !controlRequest.GetCancellationToken().IsCancellationRequested;
        var wrappedCancelled = wrapped.TryCancel();
        var controlCancelled = controlRequest.TryCancel();
        return initiallyActive && wrappedCancelled && controlCancelled &&
               wrapped.GetCancellationToken().IsCancellationRequested &&
               controlRequest.GetCancellationToken().IsCancellationRequested;
    }

    private IAsyncEnumerableRequest<StreamItem> CreateCancellableRequest(string streamKey, CancellationToken token) =>
        (IAsyncEnumerableRequest<StreamItem>)GrainFactory
            .GetGrain<IAsyncEnumerationSource>(streamKey)
            .CancellableAsync(streamKey, token);

    private static async Task<bool> CompareInvokeFailuresAsync(
        IAsyncEnumerableRequest<StreamItem> original,
        IAsyncEnumerableRequest<StreamItem> wrapped,
        AsyncEnumerationScope scope)
    {
        var originalFailure = await CaptureInvokeFailureAsync((IRequest)original, scope);
        var wrappedFailure = await CaptureInvokeFailureAsync((IRequest)wrapped, null);
        return originalFailure.GetType() == wrappedFailure.GetType() &&
               string.Equals(originalFailure.Message, wrappedFailure.Message, StringComparison.Ordinal);
    }

    private static async Task<NotImplementedException> CaptureInvokeFailureAsync(IRequest request, AsyncEnumerationScope? scope)
    {
        try
        {
            if (scope is null)
            {
                await request.Invoke();
            }
            else
            {
                using var requestScope = scope.Enter();
                await request.Invoke();
            }
        }
        catch (NotImplementedException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Native stream request Invoke unexpectedly returned instead of its Orleans-defined failure.");
    }

    private static bool RejectUnknownMethod(IInvokable unknown, AsyncEnumerationScope scope)
    {
        try
        {
            _closedSourceCatalog.Wrap(unknown, scope);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("not registered", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static AsyncEnumerationFactoryCatalog CreateClosedSourceCatalog()
    {
        var builder = new AsyncEnumerationFactoryCatalogBuilder();
        builder.AddGrainInterface(typeof(IAsyncEnumerationSource));
        builder.AddGrainInterface(typeof(IOpenGenericAsyncEnumeration));
        return builder.Build();
    }
}
