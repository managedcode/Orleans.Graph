using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ManagedCode.Orleans.Graph.Attributes;
using Orleans;

namespace ManagedCode.Orleans.Graph;

internal static class AttributeGraphConfigurator
{
    public static void ApplyFromAssemblies(GrainCallsBuilder builder, IEnumerable<Assembly>? assemblies)
    {
        var assemblyList = (assemblies?.Any() == true)
            ? assemblies!
            : AppDomain.CurrentDomain.GetAssemblies().Where(static a => !a.IsDynamic);

        foreach (var type in assemblyList.SelectMany(SafeGetTypes))
        {
            if (!typeof(IGrain).IsAssignableFrom(type))
            {
                continue;
            }

            var sourceName = TypeExtensions.GetTypeName(type);

            ApplyClientAttributes(builder, type);
            ApplyTransitionAttributes(builder, type, sourceName);
            ApplySelfReentrancy(builder, type, sourceName);
        }
    }

    private static void ApplyClientAttributes(GrainCallsBuilder builder, Type type)
    {
        foreach (var attribute in type.GetCustomAttributes<AllowClientCallAttribute>())
        {
            var targetType = attribute.GrainType ?? type;
            if (!typeof(IGrain).IsAssignableFrom(targetType))
            {
                continue;
            }

            builder.AllowClientCall(targetType);
        }
    }

    private static void ApplyTransitionAttributes(GrainCallsBuilder builder, Type sourceType, string sourceName)
    {
        foreach (var attribute in sourceType.GetCustomAttributes<AllowGrainCallAttribute>())
        {
            if (attribute.TargetGrainType is null || !typeof(IGrain).IsAssignableFrom(attribute.TargetGrainType))
            {
                continue;
            }

            var targetName = TypeExtensions.GetTypeName(attribute.TargetGrainType);

            if (attribute.AllowAllMethods || (attribute.SourceMethods.Length == 0 && attribute.TargetMethods.Length == 0))
            {
                builder.AddMethodRule(sourceName, targetName, "*", "*");
            }
            else
            {
                var pairCount = Math.Min(attribute.SourceMethods.Length, attribute.TargetMethods.Length);
                for (var i = 0; i < pairCount; i++)
                {
                    builder.AddMethodRule(sourceName, targetName, attribute.SourceMethods[i], attribute.TargetMethods[i]);
                }
            }

            if (attribute.AllowReentrancy)
            {
                builder.AddReentrancy(sourceName, targetName);
            }
        }
    }

    private static void ApplySelfReentrancy(GrainCallsBuilder builder, Type sourceType, string sourceName)
    {
        if (!sourceType.IsDefined(typeof(AllowSelfReentrancyAttribute), false))
        {
            return;
        }

        builder.AddReentrancy(sourceName, sourceName);
        builder.AddMethodRule(sourceName, sourceName, "*", "*");
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
