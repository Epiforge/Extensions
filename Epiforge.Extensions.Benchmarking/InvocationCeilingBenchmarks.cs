namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices what an invocation costs a constructed observation, against the same expression written without one
/// </summary>
/// <remarks>
/// The analyzer has no arm for an invocation and refuses the whole subtree, so an invocation-shaped expression is served by the graph whatever the options say. These arms measure the ceiling of admitting it: what the shape costs today, what removing the invocation node alone is worth on the graph, and what the same expression costs once the fast path can take it. The nested pair exists because an application whose expressions carry many invocations needs a per-invocation unit cost to multiply, not one figure
/// </remarks>
[MemoryDiagnoser]
public class InvocationCeilingBenchmarks
{
    const int elementCount = 1000;

    static readonly PropertyInfo rankProperty = typeof(BenchmarkPersonWithPartner).GetProperty(nameof(BenchmarkPersonWithPartner.Rank))!;

    static readonly Expression<Func<int, int>> absolute = value => Math.Abs(value);
    static readonly Expression<Func<int, int>> negated = value => -value;

    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> inlined = person => Math.Abs(person.Rank) > 0;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> nestedInlined = person => Math.Abs(-person.Rank) > 0;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> rankComparison = person => person.Rank > 0;

    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> invocation = BuildInvocation(false);
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> nestedInvocation = BuildInvocation(true);

    /// <summary>
    /// Builds the shape the formula engine of an application emits: an invocation of a literal lambda over a member read, optionally nested one deep
    /// </summary>
    static Expression<Func<BenchmarkPersonWithPartner, bool>> BuildInvocation(bool nested)
    {
        var person = Expression.Parameter(typeof(BenchmarkPersonWithPartner), "person");
        Expression value = Expression.MakeMemberAccess(person, rankProperty);
        if (nested)
            value = Expression.Invoke(negated, value);
        value = Expression.Invoke(absolute, value);
        return Expression.Lambda<Func<BenchmarkPersonWithPartner, bool>>(Expression.GreaterThan(value, Expression.Constant(0)), person);
    }

    CollectionObserver aDefault = null!;
    CollectionObserver direct = null!;
    CollectionObserver graph = null!;
    ObservableRangeCollection<BenchmarkPersonWithPartner> source = null!;

    [Benchmark]
    public void InlinedDirect() =>
        ConstructAndDispose(direct, inlined);

    [Benchmark]
    public void InlinedGraph() =>
        ConstructAndDispose(graph, inlined);

    /// <summary>
    /// The same expression as the arm below it, observed with direct subscription left at its default
    /// </summary>
    /// <remarks>
    /// Landing on the graph arm to the printed digit is the proof that the analyzer refused the shape rather than served it, which is what makes every other figure here a ceiling rather than a saving already taken
    /// </remarks>
    [Benchmark]
    public void InvocationDefault() =>
        ConstructAndDispose(aDefault, invocation);

    [Benchmark]
    public void InvocationGraph() =>
        ConstructAndDispose(graph, invocation);

    [Benchmark]
    public void NestedInlinedDirect() =>
        ConstructAndDispose(direct, nestedInlined);

    [Benchmark]
    public void NestedInvocationGraph() =>
        ConstructAndDispose(graph, nestedInvocation);

    [Benchmark]
    public void RankComparisonDirect() =>
        ConstructAndDispose(direct, rankComparison);

    [Benchmark(Baseline = true)]
    public void RankComparisonGraph() =>
        ConstructAndDispose(graph, rankComparison);

    void ConstructAndDispose(CollectionObserver observer, Expression<Func<BenchmarkPersonWithPartner, bool>> predicate)
    {
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(predicate);
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        aDefault = new CollectionObserver(new ExpressionObserver());
        direct = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        graph = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false }));
        source = BenchmarkPersonWithPartner.CreateCollection(elementCount);
    }
}
