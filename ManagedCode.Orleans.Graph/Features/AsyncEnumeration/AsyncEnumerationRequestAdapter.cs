using Orleans.Serialization.Invocation;

namespace ManagedCode.Orleans.Graph.Features.AsyncEnumeration;

internal static class AsyncEnumerationRequestAdapter
{
    private static readonly Type _extensionType = typeof(IAsyncEnumerableGrainExtension);
    private const string StartMethodName = nameof(IAsyncEnumerableGrainExtension.StartEnumeration);

    public static bool TryGetOriginal(IInvokable request, out IInvokable original)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.GetInterfaceType() != _extensionType ||
            !string.Equals(request.GetMethod().Name, StartMethodName, StringComparison.Ordinal))
        {
            original = null!;
            return false;
        }

        if (request.GetArgumentCount() < 2 || request.GetArgument(1) is not IInvokable originalRequest)
        {
            throw new InvalidOperationException("Native asynchronous enumeration request argument is invalid.");
        }

        original = originalRequest;
        return true;
    }

    public static IInvokable GetPolicyRequest(IInvokable request) =>
        TryGetOriginal(request, out var original) ? original : request;
}
