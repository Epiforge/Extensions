namespace Epiforge.Extensions.Components.Tests;

public class MockLoggerProvider<T> :
    ILoggerProvider
{
    public MockLoggerProvider()
    {
        MockLogger = new();
        Logger = MockLogger.Object;
    }

    public ILogger Logger { get; }
    public Mock<ILogger<T>> MockLogger { get; }

    public ILogger CreateLogger(string categoryName) =>
        Logger;

    public void Dispose()
    {
    }
}
