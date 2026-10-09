using System.Diagnostics;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.CallHistory")]
[DebuggerDisplay("{ToString()}")]
public class CallHistory
{
    [Id(0)]
    public Guid Id { get; set; }

    [Id(1)]
    public Stack<Call> History { get; } = new();

    [Id(2)]
    public NativeCallChainScope[] NativeCallChainScopes { get; set; } = [];

    public void Push(Call call)
    {
        History.Push(call);
    }

    public CallHistory Fork(bool detach = false)
    {
        var branch = new CallHistory { Id = Id };
        if (!detach)
        {
            branch.NativeCallChainScopes = [.. NativeCallChainScopes];
            foreach (var call in History.Reverse())
            {
                branch.Push(call);
            }
        }

        return branch;
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
