namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Writes to a logger and swallows whatever it throws, since a logger which fails, for instance while formatting a value another thread is changing, would otherwise become the fault of the evaluation it was tracing
/// </summary>
sealed class GuardedLogger(ILogger logger) :
    ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        try
        {
            return logger.BeginScope(state);
        }
        catch
        {
            return null;
        }
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        try
        {
            return logger.IsEnabled(logLevel);
        }
        catch
        {
            return false;
        }
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            logger.Log(logLevel, eventId, state, exception, formatter);
        }
        catch
        {
        }
    }
}
