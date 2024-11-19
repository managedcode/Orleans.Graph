![img|300x200](https://raw.githubusercontent.com/managedcode/Orleans.Graph/main/logo.png)

# Orleans Graph

[![NuGet version](https://badge.fury.io/nu/ManagedCode.Orleans.Graph.svg)](https://www.nuget.org/packages/ManagedCode.Orleans.Graph)
[![.NET](https://github.com/managedcode/Orleans.Graph/actions/workflows/dotnet.yml/badge.svg)](https://github.com/managedcode/Orleans.Graph/actions/workflows/dotnet.yml)
[![CodeQL](https://github.com/managedcode/Orleans.Graph/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/managedcode/Orleans.Graph/actions/workflows/codeql-analysis.yml)

A library for managing and validating grain call transitions in Microsoft Orleans applications. It allows you to define allowed communication patterns between grains and enforce them at runtime.

## Features

- Define allowed grain-to-grain communication patterns
- Validate grain call transitions at runtime
- Detect and prevent circular dependencies
- Support for method-level granularity
- Reentrancy control
- Client-to-grain call validation

## Installation

```sh
dotnet add package ManagedCode.Orleans.Graph
```

## Usage

### Configure Silo

```csharp
builder.AddOrleansGraph()
       .CreateGraph(graph =>
{
    graph.AddGrainTransition<GrainA, GrainB>()
         .Method(a => a.MethodA1(), b => b.MethodB1())
         .And()
         .AllowClientCallGrain<GrainA>();
});
```

### Configure Client

```csharp
builder.AddOrleansGraph()
       .CreateGraph(graph =>
{
    graph.AddGrainTransition<GrainA, GrainB>()
         .AllMethods()
         .And()
         .AllowClientCallGrain<GrainA>();
});
```

## Example
Here are more examples of how to configure the graph in your Orleans application:

### Example 1: Simple Grain-to-Grain Transition

```csharp
builder.AddOrleansGraph()
       .CreateGraph(graph =>
{
    graph.AddGrainTransition<GrainA, GrainB>()
         .AllMethods();
});
```

### Example 2: Method-Level Transition

```csharp
builder.AddOrleansGraph()
       .CreateGraph(graph =>
{
    graph.AddGrainTransition<GrainA, GrainB>()
         .Method(a => a.MethodA1(), b => b.MethodB1());
});
```

### Example 3: Allowing Reentrancy

```csharp
builder.AddOrleansGraph()
       .CreateGraph(graph =>
{
    graph.AddGrainTransition<GrainA, GrainB>()
         .WithReentrancy()
         .AllMethods();
});
```

### Example 4: Multiple Transitions

```csharp
builder.AddOrleansGraph()
       .CreateGraph(graph =>
{
    graph.AddGrainTransition<GrainA, GrainB>()
         .AllMethods()
         .And()
         .AddGrainTransition<GrainB, GrainC>()
         .AllMethods();
});
```

### Example 5: Client-to-Grain Call Validation

```csharp
builder.AddOrleansGraph()
       .CreateGraph(graph =>
{
    graph.AllowClientCallGrain<GrainA>()
         .And()
         .AddGrainTransition<GrainA, GrainB>()
         .AllMethods();
});
```

### Example 6: Detecting and Preventing Circular Dependencies

```csharp
builder.AddOrleansGraph()
       .CreateGraph(graph =>
{
    graph.AddGrainTransition<GrainA, GrainB>()
         .AllMethods()
         .And()
         .AddGrainTransition<GrainB, GrainC>()
         .AllMethods()
         .And()
         .AddGrainTransition<GrainC, GrainA>()
         .AllMethods();
});
```

### Example 7: Simple Self-Call

```csharp
builder.AddOrleansGraph()
       .CreateGraph(graph =>
{
    graph.AddGrain<IGrainA>()
         .WithReentrancy();
});
```

### Example 8: Self-Call with All Methods

```csharp
builder.AddOrleansGraph()
       .CreateGraph(graph =>
{
    graph.AddGrain<IGrainA>()
         .AllMethods()
         .WithReentrancy();
});
```

### Example 9: Self-Call with Specific Method and Parameters

```csharp
builder.AddOrleansGraph()
       .CreateGraph(graph =>
{
    graph.AddGrain<IGrainA>()
         .Method(a => a.MethodA1(GraphParam.Any<int>()))
         .WithReentrancy();
});
```

### Example 10: Self-Call with Client Call Validation

```csharp
builder.AddOrleansGraph()
       .CreateGraph(graph =>
{
    graph.AddGrain<IGrainA>()
         .AllowClientCallGrain()
         .WithReentrancy();
});
```

These examples demonstrate various ways to configure a grain to call itself, including method-level granularity, reentrancy, and client call validation.
## License

This project is licensed under the MIT License.



## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Support

If you have any questions or run into issues, please open an issue on the [GitHub repository](https://github.com/managedcode/Orleans.Graph).

---
Created and maintained by [ManagedCode](https://github.com/managedcode)