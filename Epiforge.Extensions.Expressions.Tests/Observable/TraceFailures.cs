namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// A logger which throws while an observation writes to it, which a consumer met when a logger formatted a list another thread was changing
/// </summary>
[TestClass]
public class TraceFailures
{
    sealed class ThrowingLogger :
        Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) =>
            true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("the logger failed");
    }

    /// <summary>
    /// What an observation evaluates to does not depend on whether its trace could be written
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ALoggerWhichThrowsDoesNotFaultTheObservation(bool useDirectSubscription)
    {
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, new ExpressionObserverOptions { Logger = new ThrowingLogger() });
        var person = new TestPerson("Ben");
        using var observation = observer.Observe(p => p.Name!.Length, person);
        Assert.IsNull(observation.Evaluation.Fault, "the logger's failure became the observation's fault");
        Assert.AreEqual(3, observation.Evaluation.Result);
        person.Name = "Bridget";
        Assert.IsNull(observation.Evaluation.Fault, "the logger's failure became the observation's fault after a change");
        Assert.AreEqual(7, observation.Evaluation.Result);
    }
}
