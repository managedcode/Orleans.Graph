using System.Collections.Concurrent;
using System.Reflection;
using Orleans.Concurrency;
using Orleans.Serialization.Invocation;

namespace ManagedCode.Orleans.Graph.Extensions;

internal static class MayInterleavePredicateCache
{
    private static readonly ConcurrentDictionary<Type, Lazy<Func<object, IInvokable, bool>>> Predicates = new();

    public static bool Allows(object grain, IInvokable request)
    {
        ArgumentNullException.ThrowIfNull(grain);
        ArgumentNullException.ThrowIfNull(request);

        return Predicates.GetOrAdd(
                grain.GetType(),
                static grainType => new Lazy<Func<object, IInvokable, bool>>(
                    () => CreatePredicate(grainType),
                    LazyThreadSafetyMode.ExecutionAndPublication))
            .Value(grain, request);
    }

    private static Func<object, IInvokable, bool> CreatePredicate(Type grainType)
    {
        var callbackMethodName = GetCallbackMethodName(grainType);
        if (callbackMethodName is null)
        {
            return static (_, _) => false;
        }

        var method = grainType.GetMethod(
            callbackMethodName,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy) ?? throw new InvalidOperationException(
                $"Class {grainType.FullName} doesn't declare public method with name {callbackMethodName} specified in MayInterleave attribute");

        var parameters = method.GetParameters();
        if (method.ReturnType != typeof(bool) || parameters.Length != 1 || parameters[0].ParameterType != typeof(IInvokable))
        {
            throw new InvalidOperationException(
                $"Wrong signature of callback method {callbackMethodName} specified in MayInterleave attribute for grain class {grainType.FullName}. Expected: public bool {callbackMethodName}(IInvokable req)");
        }

        if (method.IsStatic)
        {
            var predicate = method.CreateDelegate<Func<IInvokable, bool>>();
            return (_, request) => predicate(request);
        }

        var createInstancePredicate = typeof(MayInterleavePredicateCache)
            .GetMethod(nameof(CreateInstancePredicate), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(grainType);
        return (Func<object, IInvokable, bool>)createInstancePredicate.Invoke(null, [method])!;
    }

    private static Func<object, IInvokable, bool> CreateInstancePredicate<TGrain>(MethodInfo method)
        where TGrain : class
    {
        var predicate = method.CreateDelegate<Func<TGrain, IInvokable, bool>>();
        return (grain, request) => predicate((TGrain)grain, request);
    }

    private static string? GetCallbackMethodName(Type grainType)
    {
        for (var currentType = grainType; currentType is not null; currentType = currentType.BaseType)
        {
            var attribute = CustomAttributeData.GetCustomAttributes(currentType)
                .SingleOrDefault(static item => item.AttributeType == typeof(MayInterleaveAttribute));
            if (attribute is null)
            {
                continue;
            }

            if (attribute.ConstructorArguments.Count != 1 || attribute.ConstructorArguments[0].Value is not string callbackMethodName)
            {
                throw new InvalidOperationException(
                    $"MayInterleave attribute on grain class {grainType.FullName} does not contain a callback method name");
            }

            return callbackMethodName;
        }

        return null;
    }
}
