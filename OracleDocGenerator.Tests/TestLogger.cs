using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public class TestLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly LogLevel _minLevel;

    public TestLogger(ITestOutputHelper output, LogLevel minLevel = LogLevel.Trace)
    {
        _output = output;
        _minLevel = minLevel;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        // No-op scope support
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var output = $"[{logLevel}] {message}";

        if (exception != null)
        {
            output += $"\nException: {exception}";
        }

        _output.WriteLine(output);
    }

    // Optional convenience methods (not part of the ILogger interface)
    public void LogInfo(string message) => Log(LogLevel.Information, new EventId(), message, null, (s, e) => s?.ToString() ?? "");
    public void LogDebug(string message) => Log(LogLevel.Debug, new EventId(), message, null, (s, e) => s?.ToString() ?? "");
    public void LogError(string message) => Log(LogLevel.Error, new EventId(), message, null, (s, e) => s?.ToString() ?? "");

    private class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
