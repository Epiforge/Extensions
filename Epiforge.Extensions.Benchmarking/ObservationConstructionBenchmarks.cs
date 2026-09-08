namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObservationConstructionBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> constantPredicate = person => true;
    static readonly Expression<Func<BenchmarkPersonWithPartner, BenchmarkPersonWithPartner>> parameterOnly = person => person;
    static readonly Expression<Func<BenchmarkPersonWithPartner, int>> rankRead = person => person.Rank;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> rankComparison = person => person.Rank > 0;

    ExpressionObserver direct = null!;
    ExpressionObserver graph = null!;
    IObservableExpression<BenchmarkPersonWithPartner, bool>[] held = null!;
    IObservableExpression<BenchmarkPersonWithPartner, int>[] heldRanks = null!;
    BenchmarkPersonWithPartner[] people = null!;

    [Benchmark]
    public void ConstantDirect() =>
        ConstructAndDispose(direct, constantPredicate);

    [Benchmark]
    public void ConstantDirectHeld() =>
        ConstructHoldAndDispose(direct, constantPredicate);

    [Benchmark]
    public void ConstantGraph() =>
        ConstructAndDispose(graph, constantPredicate);

    [Benchmark]
    public void ConstantGraphHeld() =>
        ConstructHoldAndDispose(graph, constantPredicate);

    void ConstructAndDispose<TResult>(ExpressionObserver observer, Expression<Func<BenchmarkPersonWithPartner, TResult>> expression)
    {
        for (var i = 0; i < elementCount; ++i)
            observer.Observe(expression, people[i]).Dispose();
    }

    /// <summary>
    /// Constructs every observation before disposing any of them, which is how a query holds them, so that what a thousand living observations cost over a thousand made one at a time can be read as the difference
    /// </summary>
    void ConstructHoldAndDispose(ExpressionObserver observer, Expression<Func<BenchmarkPersonWithPartner, bool>> predicate)
    {
        for (var i = 0; i < elementCount; ++i)
            held[i] = observer.Observe(predicate, people[i]);
        for (var i = 0; i < elementCount; ++i)
            held[i].Dispose();
    }

    void ConstructHoldAndDispose<TResult>(ExpressionObserver observer, Expression<Func<BenchmarkPersonWithPartner, TResult>> expression, IObservableExpression<BenchmarkPersonWithPartner, TResult>[] into)
    {
        for (var i = 0; i < elementCount; ++i)
            into[i] = observer.Observe(expression, people[i]);
        for (var i = 0; i < elementCount; ++i)
            into[i].Dispose();
    }

    /// <summary>
    /// An expression which is nothing but the argument, so that what an observation costs before it reads anything can be told from what reading costs
    /// </summary>
    [Benchmark]
    public void ParameterOnlyDirect() =>
        ConstructAndDispose(direct, parameterOnly);

    [Benchmark]
    public void ParameterOnlyGraph() =>
        ConstructAndDispose(graph, parameterOnly);

    [Benchmark]
    public void RankComparisonDirect() =>
        ConstructAndDispose(direct, rankComparison);

    [Benchmark]
    public void RankComparisonDirectHeld() =>
        ConstructHoldAndDispose(direct, rankComparison);

    /// <summary>
    /// A member read whose result is an <see cref="int" />, which is boxed where the boolean of every other arm is not, so that the result box can be told from the rest
    /// </summary>
    [Benchmark]
    public void RankReadDirect() =>
        ConstructAndDispose(direct, rankRead);

    [Benchmark]
    public void RankReadDirectHeld() =>
        ConstructHoldAndDispose(direct, rankRead, heldRanks);

    [Benchmark]
    public void RankReadGraphHeld() =>
        ConstructHoldAndDispose(graph, rankRead, heldRanks);

    [Benchmark(Baseline = true)]
    public void RankComparisonGraph() =>
        ConstructAndDispose(graph, rankComparison);

    [Benchmark]
    public void RankComparisonGraphHeld() =>
        ConstructHoldAndDispose(graph, rankComparison);

    [GlobalSetup]
    public void Setup()
    {
        direct = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true });
        graph = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        held = new IObservableExpression<BenchmarkPersonWithPartner, bool>[elementCount];
        heldRanks = new IObservableExpression<BenchmarkPersonWithPartner, int>[elementCount];
        var collection = BenchmarkPersonWithPartner.CreateCollection(elementCount);
        people = new BenchmarkPersonWithPartner[elementCount];
        for (var i = 0; i < elementCount; ++i)
            people[i] = collection[i];
    }
}
