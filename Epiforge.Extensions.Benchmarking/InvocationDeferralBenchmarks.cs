namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices an invocation of a literal lambda whose parameter is read only in a branch, which the fast path reduced until 6.1.0 and which builds the graph from it, against one whose parameters are read where they are always evaluated
/// </summary>
/// <remarks>
/// Reducing such an invocation moved its argument into the branch, where a fault it raised went unreported whenever the branch was not taken, although the graph and compiled .NET both evaluate every argument first and report it. Declining the reduction is the fix, and its cost is that the observation builds the graph. Where the default arm lands is the finding: on <see cref="BranchedArgumentGraph"/>, exactly, means the fast path declined; on <see cref="EagerArgumentDefault"/> means it did not
/// </remarks>
[MemoryDiagnoser]
public class InvocationDeferralBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPersonWithCachedEventArguments, bool>> branchedArgument = Build((c, x) => c ? x : 0);
    static readonly Expression<Func<BenchmarkPersonWithCachedEventArguments, bool>> eagerArgument = Build((c, x) => (c ? 1 : 0) + x);

    /// <summary>
    /// Builds a predicate invoking the specified literal lambda with a comparison and a product over the element, which is the shape a formula engine emits for a function taking two operands
    /// </summary>
    static Expression<Func<BenchmarkPersonWithCachedEventArguments, bool>> Build(Expression<Func<bool, int, int>> body)
    {
        var person = Expression.Parameter(typeof(BenchmarkPersonWithCachedEventArguments), "person");
        var rank = Expression.Property(person, nameof(BenchmarkPersonWithCachedEventArguments.Rank));
        return Expression.Lambda<Func<BenchmarkPersonWithCachedEventArguments, bool>>(Expression.GreaterThan(Expression.Invoke(body, Expression.GreaterThan(rank, Expression.Constant(0)), Expression.Multiply(rank, Expression.Constant(2))), Expression.Constant(0)), person);
    }

    CollectionObserver aDefault = null!;
    CollectionObserver graph = null!;
    ObservableRangeCollection<BenchmarkPersonWithCachedEventArguments> people = null!;

    [Benchmark]
    public void BranchedArgumentDefault() =>
        ConstructAndDispose(aDefault, branchedArgument);

    [Benchmark]
    public void BranchedArgumentGraph() =>
        ConstructAndDispose(graph, branchedArgument);

    [Benchmark(Baseline = true)]
    public void EagerArgumentDefault() =>
        ConstructAndDispose(aDefault, eagerArgument);

    void ConstructAndDispose(CollectionObserver observer, Expression<Func<BenchmarkPersonWithCachedEventArguments, bool>> predicate)
    {
        var sourceQuery = observer.ObserveReadOnlyList(people);
        var where = sourceQuery.ObserveWhere(predicate);
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        aDefault = new CollectionObserver(new ExpressionObserver());
        graph = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false }));
        people = new(Enumerable.Range(1, elementCount).Select(rank => new BenchmarkPersonWithCachedEventArguments(rank)));
    }
}
