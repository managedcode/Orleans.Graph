using System.Diagnostics;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.CallHistory")]
[DebuggerDisplay("{ToString()}")]
public class CallHistory
{
    [Id(0)]
    public Guid Id { get; set; } = Guid.NewGuid();

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
        var transitions = string.Join("\n", History.Reverse().Select(call => call.ToString()));
        return $"CallHistory Id: {Id}\nTransitions:{transitions}\n";
    }

}
