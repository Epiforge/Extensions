namespace Epiforge.Extensions.Components;

static class TraceLogging
{
    /// <summary>
    /// Gets <paramref name="logger"/> when it will write traces, or null otherwise, so that a trace written as <c>logger.WhenTracing()?.LogTrace(...)</c> asks the logger at the moment of writing and, when tracing is off, neither allocates an array for its arguments nor boxes them
    /// </summary>
    public static ILogger? WhenTracing(this ILogger? logger) =>
        logger is not null && logger.IsEnabled(LogLevel.Trace) ? logger : null;
}
