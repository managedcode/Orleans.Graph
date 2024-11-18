using System;
using System.Collections;
using System.Collections.Generic;
using Orleans;

namespace ManagedCode.Orleans.Graph.Models;


[Immutable]
[GenerateSerializer]
[Alias("MC.CallHistory")]
public class CallHistory
{
    [Id(0)]
    public Guid Id = Guid.NewGuid();

    [Id(1)]
    public Stack<Call> History { get; private set; } = new();
    
    
    public void Push(Call call) => History.Push(call);
    
    public bool IsEmpty() => History.Count == 0;
}