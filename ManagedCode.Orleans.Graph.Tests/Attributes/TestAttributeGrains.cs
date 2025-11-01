using ManagedCode.Orleans.Graph.Attributes;

namespace ManagedCode.Orleans.Graph.Tests.Attributes;

[AllowClientCall]
[AllowGrainCall(typeof(IAttributeGrainB), AllowAllMethods = true, AllowReentrancy = true)]
public interface IAttributeGrainA : IGrainWithStringKey
{
    Task<int> MethodA1(int input);
    Task<int> MethodB1(int input);
}

[AllowClientCall]
[AllowSelfReentrancy]
public interface IAttributeGrainB : IGrainWithStringKey
{
    Task<int> MethodB1(int input);
}

[AllowGrainCall(typeof(IAttributeGrainB), AllowAllMethods = false, SourceMethods = new[] { nameof(IAttributeGrainC.MethodSpecial) }, TargetMethods = new[] { nameof(IAttributeGrainB.MethodB1) })]
public interface IAttributeGrainC : IGrainWithStringKey
{
    Task<int> MethodSpecial(int input);
    Task<int> MethodOther(int input);
}

[AllowClientCall(typeof(IAttributeGrainB))]
public interface IAttributeGateway : IGrainWithStringKey
{
}
