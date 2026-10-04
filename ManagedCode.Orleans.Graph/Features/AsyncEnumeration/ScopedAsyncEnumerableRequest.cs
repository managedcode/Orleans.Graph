using System.Reflection;
using Orleans.CodeGeneration;
using Orleans.Serialization.Invocation;

namespace ManagedCode.Orleans.Graph.Features.AsyncEnumeration;

internal sealed class ScopedAsyncEnumerableRequest<T>(IAsyncEnumerableRequest<T> inner, AsyncEnumerationScope scope)
    : IAsyncEnumerableRequest<T>
{
    public int MaxBatchSize
    {
        get => inner.MaxBatchSize;
        set => inner.MaxBatchSize = value;
    }

    public InvokeMethodOptions Options => ((IRequest)inner).Options;

    public void AddInvokeMethodOptions(InvokeMethodOptions options) => ((IRequest)inner).AddInvokeMethodOptions(options);

    public int GetArgumentCount() => inner.GetArgumentCount();

    public object? GetArgument(int index) => inner.GetArgument(index);

    public void SetArgument(int index, object value) => inner.SetArgument(index, value);

    public object? GetTarget() => inner.GetTarget();

    public void SetTarget(ITargetHolder holder) => inner.SetTarget(holder);

    public string GetMethodName() => inner.GetMethodName();

    public string GetInterfaceName() => inner.GetInterfaceName();

    public string GetActivityName() => inner.GetActivityName();

    public MethodInfo GetMethod() => inner.GetMethod();

    public Type GetInterfaceType() => inner.GetInterfaceType();

    public TimeSpan? GetDefaultResponseTimeout() => inner.GetDefaultResponseTimeout();

    public CancellationToken GetCancellationToken() => inner.GetCancellationToken();

    public bool TryCancel() => inner.TryCancel();

    public bool IsCancellable => inner.IsCancellable;

    public async ValueTask<Response> Invoke()
    {
        using var requestScope = scope.Enter();
        return await inner.Invoke();
    }

    public IAsyncEnumerable<T> InvokeImplementation()
    {
        using var requestScope = scope.Enter();
        return new ScopedAsyncEnumerable<T>(inner.InvokeImplementation(), scope);
    }

    public void Dispose()
    {
        using var requestScope = scope.Enter();
        inner.Dispose();
    }
}

internal sealed class ScopedAsyncEnumerable<T>(IAsyncEnumerable<T> inner, AsyncEnumerationScope scope) : IAsyncEnumerable<T>
{
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        using var requestScope = scope.Enter();
        return new ScopedAsyncEnumerator<T>(inner.GetAsyncEnumerator(cancellationToken), scope);
    }
}

internal sealed class ScopedAsyncEnumerator<T>(IAsyncEnumerator<T> inner, AsyncEnumerationScope scope) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;

    public async ValueTask<bool> MoveNextAsync()
    {
        using var requestScope = scope.Enter();
        return await inner.MoveNextAsync();
    }

    public async ValueTask DisposeAsync()
    {
        using var requestScope = scope.Enter();
        await inner.DisposeAsync();
    }
}
