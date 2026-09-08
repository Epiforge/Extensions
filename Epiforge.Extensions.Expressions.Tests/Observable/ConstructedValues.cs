namespace Epiforge.Extensions.Expressions.Tests.Observable;

public struct InitializablePoint
{
    public int X { get; set; }
}

public sealed class Nicknamed
{
    public List<string> Nicknames { get; } = [];
}

[TestClass]
public class ConstructedValues
{
    sealed class RecordingLogger :
        Microsoft.Extensions.Logging.ILogger
    {
        internal readonly List<(Microsoft.Extensions.Logging.LogLevel Level, Microsoft.Extensions.Logging.EventId EventId, string Message)> Records = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) =>
            true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Records.Add((logLevel, eventId, formatter(state, exception)));
    }

    static Expression BodyOfRecorded<TResult>(Expression<Func<Recorded, TResult>> expression) =>
        expression.Body;

    static Epiforge.Extensions.Expressions.Observable.DirectSubscriptionAnalyzer Analyzer() =>
        new();

    [TestMethod]
    public void AConstructedPairIsEligible()
    {
        var analysis = Analyzer().Analyze(BodyOfRecorded<Tuple<Recorded, IComparable>>(recorded => new Tuple<Recorded, IComparable>(recorded, recorded.Rank)));
        Assert.IsTrue(analysis.IsEligible, analysis.ToString());
    }

    [TestMethod]
    public void AConstructedValueWhichRequiresDisposalIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOfRecorded<SyncDisposableTestPerson>(recorded => new SyncDisposableTestPerson(recorded.Tag!)));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.ValueRequiresDisposal, analysis.Ineligibility);
    }

    [TestMethod]
    public void AConstructorOverSomethingIneligibleIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOfRecorded<Tuple<int, int>>(recorded => new Tuple<int, int>(recorded.Rank, new List<int> { 3 }.Count)));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UnsupportedExpressionKind, analysis.Ineligibility);
    }

    [TestMethod]
    public void AConstructedPairFollowsItsArguments()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(recorded => new Tuple<Recorded, IComparable>(recorded, recorded.Rank), subject))
        {
            Assert.AreEqual(1, log.Attachments().Count, $"fast: [{string.Join(", ", log.Attachments())}]");
            Assert.AreEqual(1, (int)expr.Evaluation.Result!.Item2);
            subject.Rank = 7;
            Assert.AreEqual(7, (int)expr.Evaluation.Result!.Item2);
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void AMemberInitOfAValueTypeIsIneligible()
    {
        var analysis = Analyzer().Analyze(Expression.MemberInit(Expression.New(typeof(InitializablePoint)), Expression.Bind(typeof(InitializablePoint).GetProperty(nameof(InitializablePoint.X))!, Expression.Constant(1))));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UnsupportedExpressionKind, analysis.Ineligibility);
    }

    [TestMethod]
    public void AMemberInitWithAListBindingIsIneligible()
    {
        var analysis = Analyzer().Analyze(Expression.MemberInit(Expression.New(typeof(Nicknamed)), Expression.ListBind(typeof(Nicknamed).GetProperty(nameof(Nicknamed.Nicknames))!, Expression.ElementInit(typeof(List<string>).GetMethod(nameof(List<string>.Add), [typeof(string)])!, Expression.Constant("Em")))));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UnsupportedExpressionKind, analysis.Ineligibility);
    }

    [TestMethod]
    public void AnInitializedObjectFollowsItsAssignments()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Tag = "one" };
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(recorded => new TestPerson { Name = recorded.Tag }, subject))
        {
            Assert.AreEqual(1, log.Attachments().Count, $"fast: [{string.Join(", ", log.Attachments())}]");
            Assert.AreEqual("one", expr.Evaluation.Result!.Name);
            subject.Tag = "two";
            Assert.AreEqual("two", expr.Evaluation.Result!.Name);
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void AnExpressionWhichIsNotEligibleIsReportedOnceForDebugging()
    {
        var logger = new RecordingLogger();
        var log = new SubscriptionLog();
        var observer = new ExpressionObserver(new ExpressionObserverOptions { Logger = logger });
        Expression<Func<Recorded, string?>> notEligible = recorded => new SyncDisposableTestPerson(recorded.Tag!).Name;
        using (observer.Observe(notEligible, new Recorded(log) { Tag = "one" }))
        using (observer.Observe(notEligible, new Recorded(log) { Tag = "two" }))
        {
        }
        var reports = logger.Records.Where(record => record.EventId == EventIds.Epiforge_Extensions_Expressions_ExpressionNotEligibleForDirectSubscription).ToList();
        Assert.AreEqual(1, reports.Count, $"two observations of one expression should report its ineligibility once; records: [{string.Join("; ", logger.Records.Select(record => record.Message))}]");
        Assert.AreEqual(Microsoft.Extensions.Logging.LogLevel.Debug, reports[0].Level);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void BothMechanismsFollowAConstructedPairAlike()
    {
        var graphLog = new SubscriptionLog();
        var graphSubject = new Recorded(graphLog) { Rank = 1 };
        var graphObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        var fastLog = new SubscriptionLog();
        var fastSubject = new Recorded(fastLog) { Rank = 1 };
        var fastObserver = new ExpressionObserver();
        using (var graphExpression = graphObserver.Observe(recorded => new Tuple<Recorded, IComparable>(recorded, recorded.Rank), graphSubject))
        using (var fastExpression = fastObserver.Observe(recorded => new Tuple<Recorded, IComparable>(recorded, recorded.Rank), fastSubject))
        {
            Assert.AreEqual(0, fastObserver.CachedObservableExpressions, "the fast path declined the shape this test exists to cover");
            CollectionAssert.AreEqual(graphLog.Attachments().Distinct().ToArray(), fastLog.Attachments().Distinct().ToArray(), $"graph: [{string.Join(", ", graphLog.Attachments())}]; fast: [{string.Join(", ", fastLog.Attachments())}]");
            Assert.AreEqual(1, (int)graphExpression.Evaluation.Result!.Item2);
            Assert.AreEqual(1, (int)fastExpression.Evaluation.Result!.Item2);
            graphSubject.Rank = 7;
            fastSubject.Rank = 7;
            Assert.AreEqual(7, (int)graphExpression.Evaluation.Result!.Item2);
            Assert.AreEqual(7, (int)fastExpression.Evaluation.Result!.Item2);
            CollectionAssert.AreEqual(graphLog.Attachments().Distinct().ToArray(), fastLog.Attachments().Distinct().ToArray(), $"after the change; graph: [{string.Join(", ", graphLog.Attachments())}]; fast: [{string.Join(", ", fastLog.Attachments())}]");
        }
        Assert.AreEqual(0, graphLog.Outstanding, "the graph did not detach everything it attached");
        Assert.AreEqual(0, fastLog.Outstanding, "the fast path did not detach everything it attached");
        Assert.AreEqual(0, graphObserver.CachedObservableExpressions);
        Assert.AreEqual(0, fastObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void AConstructedPairAnnouncesEachChangeOfItsKey()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(recorded => new Tuple<Recorded, IComparable>(recorded, recorded.Rank), subject))
        {
            var announcements = 0;
            void propertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(IObservableExpression<object?>.Evaluation))
                    ++announcements;
            }

            expr.PropertyChanged += propertyChanged;
            subject.Rank = 2;
            subject.Rank = 3;
            Assert.AreEqual(2, announcements, "a query which orders by an observed key depends on every change of that key being announced");
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }
}
