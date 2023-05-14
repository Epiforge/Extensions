
namespace Epiforge.Extensions.Components.Tests;

public static class MockLoggerExtensions
{
    public static IServiceCollection AddMockLogging<T>(this IServiceCollection services, out Mock<ILogger<T>> mockLogger)
    {
        Mock<ILogger<T>> enclosedMockLogger = null!;
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            var mockLoggerProvider = new MockLoggerProvider<T>();
            enclosedMockLogger = mockLoggerProvider.MockLogger;
            builder.AddProvider(mockLoggerProvider);
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        mockLogger = enclosedMockLogger;
        return services;
    }

    public static void VerifyLog<T>(this Mock<ILogger<T>> mockLogger, LogLevel logLevel, string message) =>
        VerifyLog(mockLogger, logLevel, message, Times.Once());

    public static void VerifyLog<T>(this Mock<ILogger<T>> mockLogger, LogLevel logLevel, string message, Times times) =>
        mockLogger.Verify(x => x.Log(logLevel, It.IsAny<EventId>(), It.Is<It.IsAnyType>((o, t) => o.ToString() == message), null, (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), times);

    public static void VerifyLogDebug<T>(this Mock<ILogger<T>> mockLogger, string message) =>
        VerifyLog(mockLogger, LogLevel.Debug, message);

    public static void VerifyLogDebug<T>(this Mock<ILogger<T>> mockLogger, string message, Times times) =>
        VerifyLog(mockLogger, LogLevel.Debug, message, times);

    public static void VerifyLogError<T>(this Mock<ILogger<T>> mockLogger, string message) =>
        VerifyLog(mockLogger, LogLevel.Error, message);

    public static void VerifyLogError<T>(this Mock<ILogger<T>> mockLogger, string message, Times times) =>
        VerifyLog(mockLogger, LogLevel.Error, message, times);

    public static void VerifyLogInformation<T>(this Mock<ILogger<T>> mockLogger, string message) =>
        VerifyLog(mockLogger, LogLevel.Information, message);

    public static void VerifyLogInformation<T>(this Mock<ILogger<T>> mockLogger, string message, Times times) =>
        VerifyLog(mockLogger, LogLevel.Information, message, times);

    public static void VerifyLogWarning<T>(this Mock<ILogger<T>> mockLogger, string message) =>
        VerifyLog(mockLogger, LogLevel.Warning, message);

    public static void VerifyLogWarning<T>(this Mock<ILogger<T>> mockLogger, string message, Times times) =>
        VerifyLog(mockLogger, LogLevel.Warning, message, times);
}
