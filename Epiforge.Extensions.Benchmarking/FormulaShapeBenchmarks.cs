namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices the shape a formula engine emits when it reaches its data through a method whose return value the observer disposes of, against the same shape reaching through one it does not
/// </summary>
/// <remarks>
/// The analyzer refused a method call whose return value is disposed of until 9 September, because the fast path held no value of its own and so could neither hold what such a call produced nor dispose of it. It now holds one in a slot resolved on first read, so the disposable arms are served rather than refused and the plain arms are no longer a ceiling but the control the served shape is measured against. Every operand of the call in these expressions is a constant once the parameter has been replaced, which is what makes holding it once sufficient and is the whole reason the slot is correct rather than merely faster
/// </remarks>
[MemoryDiagnoser]
public class FormulaShapeBenchmarks
{
    const int elementCount = 1000;

    static readonly BenchmarkValueSource values = new();

    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> disposableFormula = BuildFormula(false);
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> plainFormula = BuildFormula(true);
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> rankComparison = person => person.Rank > 0;

    /// <summary>
    /// Builds the shape the formula engine of an application emits: an invocation of a literal lambda whose body calls through a captured object and reads a notifying value from what it returned
    /// </summary>
    static Expression<Func<BenchmarkPersonWithPartner, bool>> BuildFormula(bool plain)
    {
        var person = Expression.Parameter(typeof(BenchmarkPersonWithPartner), "person");
        Expression<Func<BenchmarkPersonWithPartner, int>> read = plain
            ? element => (int)values.OpenPlain(element).Value!
            : element => (int)values.Open(element).Value!;
        return Expression.Lambda<Func<BenchmarkPersonWithPartner, bool>>(Expression.GreaterThan(Expression.Invoke(read, person), Expression.Constant(0)), person);
    }

    CollectionObserver aDefault = null!;
    CollectionObserver direct = null!;
    CollectionObserver graph = null!;
    ObservableRangeCollection<BenchmarkPersonWithPartner> people = null!;

    /// <summary>
    /// The disposable shape observed with direct subscription left at its default
    /// </summary>
    /// <remarks>
    /// Where this arm lands is what the instrument reports. Landing on the graph arm below it would mean the analyzer refused the shape; landing on <see cref="PlainFormulaDirect" /> means it is served, and the distance above that arm is what the slot itself costs. The tests asserting that no observable expression was cached are what prove service by construction rather than by position
    /// </remarks>
    [Benchmark]
    public void DisposableFormulaDefault() =>
        ConstructAndDispose(aDefault, disposableFormula);

    [Benchmark]
    public void DisposableFormulaGraph() =>
        ConstructAndDispose(graph, disposableFormula);

    [Benchmark]
    public void PlainFormulaDirect() =>
        ConstructAndDispose(direct, plainFormula);

    [Benchmark]
    public void PlainFormulaGraph() =>
        ConstructAndDispose(graph, plainFormula);

    [Benchmark]
    public void RankComparisonDirect() =>
        ConstructAndDispose(direct, rankComparison);

    [Benchmark(Baseline = true)]
    public void RankComparisonGraph() =>
        ConstructAndDispose(graph, rankComparison);

    void ConstructAndDispose(CollectionObserver observer, Expression<Func<BenchmarkPersonWithPartner, bool>> predicate)
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
        direct = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        graph = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false }));
        people = BenchmarkPersonWithPartner.CreateCollection(elementCount);
    }
}
