namespace Epiforge.Extensions.Tests;

public abstract class MockLogger :
    ILogger
{
    public virtual IDisposable? BeginScope<TState>(TState state)
        where TState : notnull =>
        null;

    public void DidNotReceiveLog(LogLevel logLevel, string message) =>
        this.DidNotReceive().Log(logLevel, message);

    public void DidNotReceiveLogCritical(string message) =>
        DidNotReceiveLog(LogLevel.Critical, message);

    public void DidNotReceiveLogDebug(string message) =>
        DidNotReceiveLog(LogLevel.Debug, message);

    public void DidNotReceiveLogError(string message) =>
        DidNotReceiveLog(LogLevel.Error, message);

    public void DidNotReceiveLogInformation(string message) =>
        DidNotReceiveLog(LogLevel.Information, message);

    public void DidNotReceiveLogTrace(string message) =>
        DidNotReceiveLog(LogLevel.Trace, message);

    public void DidNotReceiveLogWarning(string message) =>
        DidNotReceiveLog(LogLevel.Warning, message);

    public virtual bool IsEnabled(LogLevel logLevel) =>
        true;

    public virtual void Log(LogLevel logLevel, string message)
    {
    }

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Log(logLevel, formatter(state, exception));

    public void ReceivedLog(LogLevel logLevel, string message) =>
        this.Received().Log(logLevel, message);

    public void ReceivedLogCritical(string message) =>
        ReceivedLog(LogLevel.Critical, message);

    public void ReceivedLogDebug(string message) =>
        ReceivedLog(LogLevel.Debug, message);

    public void ReceivedLogError(string message) =>
        ReceivedLog(LogLevel.Error, message);

    public void ReceivedLogInformation(string message) =>
        ReceivedLog(LogLevel.Information, message);

    public void ReceivedLogTrace(string message) =>
        ReceivedLog(LogLevel.Trace, message);

    public void ReceivedLogWarning(string message) =>
        ReceivedLog(LogLevel.Warning, message);
}

public abstract class MockLogger<T> :
    MockLogger,
    ILogger<T>
{
}