namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices a try whose catch blocks give a fallback in place of a fault, in the shape a formula engine emits for a function doing that, against the same body observed without one
/// </summary>
/// <remarks>
/// The construction arms build and dispose of a filtered view of a thousand elements, once over elements whose body has a value and once over elements whose body faults and whose fallback is therefore evaluated. The toggle arms hold a thousand observations and move each element's value to another and back, so that every observation re-evaluates twice: the body-only controls between two values, or between a value and a fault, and the try arms between a value and the fallback. The difference between a try arm and its control of the same mechanism is what the try costs; the faulting control prices the fault the try arm also pays for, so that what is left is the handler
/// </remarks>
[MemoryDiagnoser]
public class TryExpressionBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPersonWithCachedEventArguments, bool>> bodyOnly = person => 100 / person.Rank > 0;
    static readonly Expression<Func<BenchmarkPersonWithCachedEventArguments, bool>> fallingBack = BuildFallingBack();

    static Expression<Func<BenchmarkPersonWithCachedEventArguments, bool>> BuildFallingBack()
    {
        var person = Expression.Parameter(typeof(BenchmarkPersonWithCachedEventArguments), "person");
        var body = Expression.Divide(Expression.Constant(100), Expression.Property(person, nameof(BenchmarkPersonWithCachedEventArguments.Rank)));
        var handled = Expression.TryCatch
        (
            body,
            Expression.Catch(typeof(OperationCanceledException), Expression.Rethrow(typeof(int))),
            Expression.Catch(typeof(ObjectDisposedException), Expression.Rethrow(typeof(int))),
            Expression.Catch(typeof(Exception), Expression.Constant(-1))
        );
        return Expression.Lambda<Func<BenchmarkPersonWithCachedEventArguments, bool>>(Expression.GreaterThan(handled, Expression.Constant(0)), person);
    }

    CollectionObserver direct = null!;
    CollectionObserver graph = null!;
    ExpressionObserver observer = null!;
    List<IObservableExpression<BenchmarkPersonWithCachedEventArguments, bool>> observations = null!;
    ObservableRangeCollection<BenchmarkPersonWithCachedEventArguments> people = null!;
    int toggledRank;

    [Benchmark]
    public void BodyDirect() =>
        ConstructAndDispose(direct, bodyOnly);

    [Benchmark(Baseline = true)]
    public void BodyGraph() =>
        ConstructAndDispose(graph, bodyOnly);

    void ConstructAndDispose(CollectionObserver collectionObserver, Expression<Func<BenchmarkPersonWithCachedEventArguments, bool>> predicate)
    {
        var sourceQuery = collectionObserver.ObserveReadOnlyList(people);
        var where = sourceQuery.ObserveWhere(predicate);
        where.Dispose();
        sourceQuery.Dispose();
    }

    [Benchmark]
    public void FallingBackFaultedDirect() =>
        ConstructAndDispose(direct, fallingBack);

    [Benchmark]
    public void FallingBackFaultedGraph() =>
        ConstructAndDispose(graph, fallingBack);

    [Benchmark]
    public void FallingBackValuedDirect() =>
        ConstructAndDispose(direct, fallingBack);

    [Benchmark]
    public void FallingBackValuedGraph() =>
        ConstructAndDispose(graph, fallingBack);

    void Observe(bool useDirectSubscription, Expression<Func<BenchmarkPersonWithCachedEventArguments, bool>> lambda, int toggledRank)
    {
        people = new(Enumerable.Range(1, elementCount).Select(_ => new BenchmarkPersonWithCachedEventArguments(50)));
        observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = useDirectSubscription });
        observations = [.. people.Select(person => observer.Observe(lambda, person))];
        this.toggledRank = toggledRank;
    }

    [GlobalSetup(Targets = [nameof(FallingBackFaultedDirect), nameof(FallingBackFaultedGraph)])]
    public void SetupFaulted()
    {
        direct = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        graph = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false }));
        people = new(Enumerable.Range(1, elementCount).Select(_ => new BenchmarkPersonWithCachedEventArguments(0)));
    }

    [GlobalSetup(Target = nameof(ToggleBodyDirect))]
    public void SetupToggleBodyDirect() =>
        Observe(true, bodyOnly, 20);

    [GlobalSetup(Target = nameof(ToggleBodyFaultingDirect))]
    public void SetupToggleBodyFaultingDirect() =>
        Observe(true, bodyOnly, 0);

    [GlobalSetup(Target = nameof(ToggleBodyFaultingGraph))]
    public void SetupToggleBodyFaultingGraph() =>
        Observe(false, bodyOnly, 0);

    [GlobalSetup(Target = nameof(ToggleBodyGraph))]
    public void SetupToggleBodyGraph() =>
        Observe(false, bodyOnly, 20);

    [GlobalSetup(Target = nameof(ToggleFallingBackDirect))]
    public void SetupToggleFallingBackDirect() =>
        Observe(true, fallingBack, 0);

    [GlobalSetup(Target = nameof(ToggleFallingBackGraph))]
    public void SetupToggleFallingBackGraph() =>
        Observe(false, fallingBack, 0);

    [GlobalSetup(Targets = [nameof(BodyDirect), nameof(BodyGraph), nameof(FallingBackValuedDirect), nameof(FallingBackValuedGraph)])]
    public void SetupValued()
    {
        direct = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        graph = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false }));
        people = new(Enumerable.Range(1, elementCount).Select(rank => new BenchmarkPersonWithCachedEventArguments(rank)));
    }

    void Toggle()
    {
        for (int i = 0, ii = people.Count; i < ii; ++i)
        {
            var person = people[i];
            person.Rank = toggledRank;
            person.Rank = 50;
        }
    }

    [Benchmark]
    public void ToggleBodyDirect() =>
        Toggle();

    [Benchmark]
    public void ToggleBodyFaultingDirect() =>
        Toggle();

    [Benchmark]
    public void ToggleBodyFaultingGraph() =>
        Toggle();

    [Benchmark]
    public void ToggleBodyGraph() =>
        Toggle();

    [Benchmark]
    public void ToggleFallingBackDirect() =>
        Toggle();

    [Benchmark]
    public void ToggleFallingBackGraph() =>
        Toggle();
}
