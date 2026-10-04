using System.Collections.Frozen;
using System.Reflection;
using Orleans.Serialization.Invocation;

namespace ManagedCode.Orleans.Graph.Features.AsyncEnumeration;

internal sealed class AsyncEnumerationFactoryCatalog
{
    private readonly FrozenDictionary<MethodInfo, Func<IInvokable, AsyncEnumerationScope, IInvokable>> _factories;

    internal AsyncEnumerationFactoryCatalog(Dictionary<MethodInfo, Func<IInvokable, AsyncEnumerationScope, IInvokable>> factories)
    {
        _factories = factories.ToFrozenDictionary();
    }

    public IInvokable Wrap(IInvokable request, AsyncEnumerationScope scope)
    {
        if (!_factories.TryGetValue(request.GetMethod(), out var factory))
        {
            throw new InvalidOperationException("Asynchronous enumeration method is not registered in the Graph catalog.");
        }

        return factory(request, scope);
    }

    public bool Contains(MethodInfo method) => _factories.ContainsKey(method);
}

internal sealed class AsyncEnumerationFactoryCatalogBuilder
{
    private static readonly MethodInfo _createFactoryMethod = typeof(AsyncEnumerationFactoryCatalogBuilder)
        .GetMethod(nameof(CreateFactory), BindingFlags.NonPublic | BindingFlags.Static)!;
    private readonly Dictionary<MethodInfo, Func<IInvokable, AsyncEnumerationScope, IInvokable>> _factories = [];
    private readonly Dictionary<Type, Func<IInvokable, AsyncEnumerationScope, IInvokable>> _factoriesByItemType = [];

    public void AddGrainInterface(Type grainInterface)
    {
        if (!grainInterface.IsInterface)
        {
            return;
        }

        foreach (var method in grainInterface.GetMethods())
        {
            if (!TryGetItemType(method, out var itemType))
            {
                continue;
            }

            if (!_factoriesByItemType.TryGetValue(itemType, out var factory))
            {
                var createFactory = _createFactoryMethod.MakeGenericMethod(itemType);
                factory = (Func<IInvokable, AsyncEnumerationScope, IInvokable>)createFactory.CreateDelegate(
                    typeof(Func<IInvokable, AsyncEnumerationScope, IInvokable>));
                _factoriesByItemType.Add(itemType, factory);
            }

            _factories.TryAdd(method, factory);
        }
    }

    public AsyncEnumerationFactoryCatalog Build() => new(_factories);

    private static bool TryGetItemType(MethodInfo method, out Type itemType)
    {
        var returnType = method.ReturnType;
        if (method.ContainsGenericParameters || !returnType.IsGenericType ||
            returnType.GetGenericTypeDefinition() != typeof(IAsyncEnumerable<>))
        {
            itemType = null!;
            return false;
        }

        itemType = returnType.GenericTypeArguments[0];
        return !itemType.ContainsGenericParameters;
    }

    private static IInvokable CreateFactory<T>(IInvokable request, AsyncEnumerationScope scope) =>
        new ScopedAsyncEnumerableRequest<T>((IAsyncEnumerableRequest<T>)request, scope);
}
