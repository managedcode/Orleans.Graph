using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Telemetry;
using Microsoft.Extensions.Logging;

namespace ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

internal sealed class ShutdownTelemetryLogObserver : ILoggerProvider, ILogger
{
    private int _failures;
    internal int Failures => Volatile.Read(ref _failures);

    public ILogger CreateLogger(string categoryName) => this;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public void Dispose()
    {
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel) &&
            (formatter(state, exception).Contains(typeof(IOrleansGraphTelemetryGrain).FullName!, StringComparison.Ordinal) ||
             exception?.StackTrace?.Contains(nameof(OrleansGraphTelemetryWorker), StringComparison.Ordinal) == true))
        {
            Interlocked.Increment(ref _failures);
        }
    }
}
