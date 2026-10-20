using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ClankerWorld.Tests;

internal sealed class RecordingLoggerProvider<T>(RecordingLogger<T> logger) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => logger;

    public void Dispose() { }
}

internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<string> messages = new();
    private readonly object sync = new();
    private readonly List<(Func<string, bool> Matches, TaskCompletionSource Completion)> waiting = [];

    public Task WaitForAsync(Func<string, bool> matches)
    {
        lock (sync)
        {
            if (messages.Any(matches)) return Task.CompletedTask;
            var completion = new TaskCompletionSource();
            waiting.Add((matches, completion));
            return completion.Task;
        }
    }

    public IReadOnlyCollection<string> Messages => messages.ToArray();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        lock (sync)
        {
            messages.Enqueue(message);
            foreach (var waiter in waiting.Where(waiter => waiter.Matches(message)).ToArray())
            {
                waiter.Completion.TrySetResult();
                waiting.Remove(waiter);
            }
        }
    }
}
