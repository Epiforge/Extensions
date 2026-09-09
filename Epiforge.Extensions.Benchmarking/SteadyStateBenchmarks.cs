namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices what a notification costs each mechanism once the observations exist, which every other instrument on this shape leaves out by constructing and discarding without ever changing a value
/// </summary>
/// <remarks>
/// Eligibility decides which mechanism runs after construction as well as what construction costs, and the two mechanisms re-evaluate differently: the fast path invokes one compiled delegate for the whole body whenever any subscribed source announces, where the graph re-evaluates a node only when that node's own operands have moved. Where a subexpression cannot change, the graph never repeats it and the fast path repeats it every time unless the analyzer held it. Each shape here has a construct-only arm and a steady arm which does the same construction and then drives ten rounds of notifications over every element; the difference between them is what those notifications cost, and no arm needs an iteration setup to produce it
/// </remarks>
[MemoryDiagnoser]
public class SteadyStateBenchmarks
{
    const int elementCount = 1000;
    const int rounds = 10;

    static readonly PropertyInfo rankProperty = typeof(BenchmarkPersonWithPartner).GetProperty(nameof(BenchmarkPersonWithPartner.Rank))!;

    static readonly BenchmarkValueSource values = new();
    static readonly BenchmarkWeight weight = new();

    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> heldFormula = BuildHeldFormula();
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> rankComparison = person => person.Rank > 0;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> unheldCall = person => person.Rank + weight.Scale() > 0;

    /// <summary>
    /// The shape a formula engine emits, whose call the analyzer holds because nothing it reads can change, summed with a member read so that a notification reaches the observation at all
    /// </summary>
    /// <remarks>
    /// The member read is load bearing, and its absence was a defect in the first form of this instrument. The query these arms open does not subscribe to the person it reads, a form of it which did having leaked a handler per element per invocation, so with nothing else in the expression a change to that person announced to nothing and the steady arms measured a floor rather than a re-evaluation. Reading the person alongside the call drives the notification without putting a subscription back inside the query
    /// </remarks>
    static Expression<Func<BenchmarkPersonWithPartner, bool>> BuildHeldFormula()
    {
        var person = Expression.Parameter(typeof(BenchmarkPersonWithPartner), "person");
        Expression<Func<BenchmarkPersonWithPartner, int>> read = element => (int)values.Open(element).Value!;
        return Expression.Lambda<Func<BenchmarkPersonWithPartner, bool>>(Expression.GreaterThan(Expression.Add(Expression.MakeMemberAccess(person, rankProperty), Expression.Invoke(read, person)), Expression.Constant(0)), person);
    }

    CollectionObserver aDefault = null!;
    CollectionObserver graph = null!;
    ObservableRangeCollection<BenchmarkPersonWithPartner> people = null!;

    [Benchmark]
    public void HeldFormulaDefaultConstruct() =>
        Run(aDefault, heldFormula, 0);

    [Benchmark]
    public void HeldFormulaDefaultSteady() =>
        Run(aDefault, heldFormula, rounds);

    [Benchmark]
    public void HeldFormulaGraphConstruct() =>
        Run(graph, heldFormula, 0);

    [Benchmark]
    public void HeldFormulaGraphSteady() =>
        Run(graph, heldFormula, rounds);

    [Benchmark(Baseline = true)]
    public void RankComparisonDefaultConstruct() =>
        Run(aDefault, rankComparison, 0);

    [Benchmark]
    public void RankComparisonDefaultSteady() =>
        Run(aDefault, rankComparison, rounds);

    [Benchmark]
    public void RankComparisonGraphConstruct() =>
        Run(graph, rankComparison, 0);

    [Benchmark]
    public void RankComparisonGraphSteady() =>
        Run(graph, rankComparison, rounds);

    [Benchmark]
    public void UnheldCallDefaultConstruct() =>
        Run(aDefault, unheldCall, 0);

    [Benchmark]
    public void UnheldCallDefaultSteady() =>
        Run(aDefault, unheldCall, rounds);

    [Benchmark]
    public void UnheldCallGraphConstruct() =>
        Run(graph, unheldCall, 0);

    [Benchmark]
    public void UnheldCallGraphSteady() =>
        Run(graph, unheldCall, rounds);

    void Run(CollectionObserver observer, Expression<Func<BenchmarkPersonWithPartner, bool>> predicate, int notifying)
    {
        var sourceQuery = observer.ObserveReadOnlyList(people);
        var where = sourceQuery.ObserveWhere(predicate);
        for (var round = 0; round < notifying; ++round)
            for (int i = 0, ii = people.Count; i < ii; ++i)
                people[i].Rank = round + 1;
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        aDefault = new CollectionObserver(new ExpressionObserver());
        graph = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false }));
        people = BenchmarkPersonWithPartner.CreateCollection(elementCount);
    }
}
