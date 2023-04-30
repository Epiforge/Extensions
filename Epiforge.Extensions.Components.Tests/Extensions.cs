namespace Epiforge.Extensions.Components.Tests;

static class Extensions
{
    public static void VerifyLog<T>(this Mock<ILogger<T>> mockLogger, LogLevel logLevel, string message) =>
        VerifyLog(mockLogger, logLevel, message, Times.Once());

    public static void VerifyLog<T>(this Mock<ILogger<T>> mockLogger, LogLevel logLevel, string message, Times times) =>
        mockLogger.Verify(x => x.Log(logLevel, It.IsAny<EventId>(), It.Is<It.IsAnyType>((o, t) => o.ToString() == message), null, (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), times);

    public static void VerifyLogDebug<T>(this Mock<ILogger<T>> mockLogger, string message) =>
        VerifyLog(mockLogger, LogLevel.Debug, message);

    public static void VerifyLogDebug<T>(this Mock<ILogger<T>> mockLogger, string message, Times times) =>
        VerifyLog(mockLogger, LogLevel.Debug, message, times);
}
