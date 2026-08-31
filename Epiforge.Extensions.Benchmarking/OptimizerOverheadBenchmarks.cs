namespace Epiforge.Extensions.Benchmarking;

using System.Linq.Expressions;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class OptimizerOverheadBenchmarks
{
    static readonly Expression<Func<BenchmarkPerson, bool>> predicate = person => person.Rank % 2 == 0;
    static readonly Expression<Func<BenchmarkPerson, int>> selector = person => person.Rank * 2;

    IObservableCollectionQuery<BenchmarkPerson> optimizedSource = null!;
    IObservableCollectionQuery<BenchmarkPerson> plainSource = null!;
    IObservableCollectionQuery<BenchmarkPerson> optimizedWhere = null!;
    IObservableCollectionQuery<BenchmarkPerson> plainWhere = null!;
    ExpressionObserver optimizedGraphObserver = null!;
    ExpressionObserver plainGraphObserver = null!;
    BenchmarkPerson subject = null!;

    static Expression<Func<BenchmarkPerson, int>> BuildSelector()
    {
        var person = Expression.Parameter(typeof(BenchmarkPerson), "person");
        return Expression.Lambda<Func<BenchmarkPerson, int>>(Expression.Multiply(Expression.Property(person, nameof(BenchmarkPerson.Rank)), Expression.Constant(2)), person);
    }

    [Benchmark]
    public void FreshLambdaObservationWithOptimizer() =>
        optimizedGraphObserver.Observe(BuildSelector(), subject).Dispose();

    [Benchmark]
    public void FreshLambdaObservationWithoutOptimizer() =>
        plainGraphObserver.Observe(BuildSelector(), subject).Dispose();

    [Benchmark]
    public void GraphObservationWithOptimizer() =>
        optimizedGraphObserver.Observe(selector, subject).Dispose();

    [Benchmark(Baseline = true)]
    public void GraphObservationWithoutOptimizer() =>
        plainGraphObserver.Observe(selector, subject).Dispose();

    [Benchmark]
    public void WhereCacheHitWithOptimizer() =>
        optimizedSource.ObserveWhere(predicate).Dispose();

    [Benchmark]
    public void WhereCacheHitWithoutOptimizer() =>
        plainSource.ObserveWhere(predicate).Dispose();

    [GlobalCleanup]
    public void Cleanup()
    {
        optimizedWhere.Dispose();
        plainWhere.Dispose();
        optimizedSource.Dispose();
        plainSource.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        var source = BenchmarkPerson.CreateCollection(100);
        subject = new BenchmarkPerson("subject", 4);
        plainGraphObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        optimizedGraphObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false, Optimizer = ExpressionOptimizer.tryVisit });
        plainSource = new CollectionObserver(new ExpressionObserver()).ObserveReadOnlyList(source);
        optimizedSource = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { Optimizer = ExpressionOptimizer.tryVisit })).ObserveReadOnlyList(source);
        plainWhere = plainSource.ObserveWhere(predicate);
        optimizedWhere = optimizedSource.ObserveWhere(predicate);
    }
}
