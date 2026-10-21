namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices what an observer given a logger which does not write traces pays per notification, against the same observer given none, which is the condition every other instrument in this project measures
/// </summary>
/// <remarks>
/// The logger is what an application configures: a factory whose minimum level is information, over a provider which would write whatever reaches it. Each arm changes all thousand people of a source once; the subjects pass pre-allocated notification arguments, so that nothing the people allocate is in the figure. The notification arms keep every person a member and so measure re-evaluation alone; the membership arms move every person out of the filter and back, which adds what the filter announces
/// </remarks>
[MemoryDiagnoser]
public class TraceLoggingCostBenchmarks
{
    const int elementCount = 1000;

    sealed class DiscardingLoggerProvider :
        ILoggerProvider
    {
        sealed class DiscardingLogger :
            ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull =>
                null;

            public bool IsEnabled(LogLevel logLevel) =>
                true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
            }
        }

        static readonly DiscardingLogger logger = new();

        public ILogger CreateLogger(string categoryName) =>
            logger;

        public void Dispose()
        {
        }
    }

    ILoggerFactory? loggerFactory;
    ObservableRangeCollection<BenchmarkPersonWithCachedEventArguments> people = null!;
    IObservableCollectionQuery<BenchmarkPersonWithCachedEventArguments> sourceQuery = null!;
    IObservableCollectionQuery<BenchmarkPersonWithCachedEventArguments> where = null!;

    [GlobalCleanup]
    public void Cleanup()
    {
        where.Dispose();
        sourceQuery.Dispose();
        loggerFactory?.Dispose();
    }

    [Benchmark]
    public void DirectMembershipChangesWithADisabledLogger() =>
        MoveEveryoneOutAndBack();

    [Benchmark]
    public void DirectMembershipChangesWithoutALogger() =>
        MoveEveryoneOutAndBack();

    [Benchmark]
    public void DirectNotificationsWithADisabledLogger() =>
        NotifyEveryone();

    [Benchmark(Baseline = true)]
    public void DirectNotificationsWithoutALogger() =>
        NotifyEveryone();

    [Benchmark]
    public void GraphNotificationsWithADisabledLogger() =>
        NotifyEveryone();

    [Benchmark]
    public void GraphNotificationsWithoutALogger() =>
        NotifyEveryone();

    void MoveEveryoneOutAndBack()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    void NotifyEveryone()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 2;
    }

    void SetUp(bool useDirectSubscription, bool withLogger)
    {
        var options = new ExpressionObserverOptions { UseDirectSubscription = useDirectSubscription };
        if (withLogger)
        {
            loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Information).AddProvider(new DiscardingLoggerProvider()));
            options.Logger = loggerFactory.CreateLogger<TraceLoggingCostBenchmarks>();
        }
        var observer = new CollectionObserver(new ExpressionObserver(options));
        var list = new List<BenchmarkPersonWithCachedEventArguments>(elementCount);
        for (var i = 0; i < elementCount; ++i)
            list.Add(new BenchmarkPersonWithCachedEventArguments(i * 4));
        people = new ObservableRangeCollection<BenchmarkPersonWithCachedEventArguments>(list);
        sourceQuery = observer.ObserveReadOnlyList(people);
        where = sourceQuery.ObserveWhere(person => person.Rank % 2 == 0);
    }

    [GlobalSetup(Targets = [nameof(DirectMembershipChangesWithADisabledLogger), nameof(DirectNotificationsWithADisabledLogger)])]
    public void SetUpDirectWithADisabledLogger() =>
        SetUp(true, true);

    [GlobalSetup(Targets = [nameof(DirectMembershipChangesWithoutALogger), nameof(DirectNotificationsWithoutALogger)])]
    public void SetUpDirectWithoutALogger() =>
        SetUp(true, false);

    [GlobalSetup(Target = nameof(GraphNotificationsWithADisabledLogger))]
    public void SetUpGraphWithADisabledLogger() =>
        SetUp(false, true);

    [GlobalSetup(Target = nameof(GraphNotificationsWithoutALogger))]
    public void SetUpGraphWithoutALogger() =>
        SetUp(false, false);
}
