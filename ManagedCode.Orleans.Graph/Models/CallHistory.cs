using System;
using System.Collections.Generic;
using Orleans;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.CallHistory")]
public class CallHistory
{
    [Id(0)] public Guid Id = Guid.NewGuid();

    [Id(1)]
    public Stack<Call> History { get; } = new();


    public void Push(Call call)
    {
        History.Push(call);
    }

    public bool IsEmpty()
    {
        return History.Count == 0;
    }
}