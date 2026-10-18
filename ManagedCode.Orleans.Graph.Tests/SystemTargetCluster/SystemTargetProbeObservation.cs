namespace ManagedCode.Orleans.Graph.Tests.SystemTargetCluster;

public sealed class SystemTargetProbeObservation
{
    private int _executions;
    private int _callbackExecutions;
    private SystemTargetProbeReply? _initializationReply;
    private SystemTargetInitializationFailure? _initializationFailure;

    public int Executions => Volatile.Read(ref _executions);
    public int CallbackExecutions => Volatile.Read(ref _callbackExecutions);
    public SystemTargetProbeReply? InitializationReply => Volatile.Read(ref _initializationReply);
    public SystemTargetInitializationFailure? InitializationFailure => Volatile.Read(ref _initializationFailure);

    public void RecordExecution() => Interlocked.Increment(ref _executions);

    public void RecordCallbackExecution() => Interlocked.Increment(ref _callbackExecutions);

    public void RecordInitialization(SystemTargetProbeReply reply) => Volatile.Write(ref _initializationReply, reply);

    public void RecordInitializationFailure(Exception exception, int highestCompletedStage) =>
        Volatile.Write(ref _initializationFailure, new SystemTargetInitializationFailure(highestCompletedStage, exception.GetType().FullName!));
}

public sealed record SystemTargetInitializationFailure(int HighestCompletedStage, string ExceptionType);
