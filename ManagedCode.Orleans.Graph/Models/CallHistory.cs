using System;
using System.Collections.Generic;
using System.Linq;
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
    
    public override string ToString()
    {
        var transitions = string.Join("\n", History.Select(call => call.ToString()));
        return $"CallHistory Id: {Id}\nTransitions:\n{transitions}";
    }

}