namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class ObservableExpressionBenchmarks
{
    const int poolSize = 64;

    int cursor;
    List<IObservableExpression<BenchmarkPerson, int>> existing = null!;
    ExpressionObserver observer = null!;
    BenchmarkPerson[] pool = null!;

    [Params(0, 1000, 10000)]
    public int ExistingExpressions { get; set; }

    [GlobalCleanup]
    public void Cleanup()
    {
        for (int i = 0, ii = existing.Count; i < ii; ++i)
            existing[i].Dispose();
    }

    [Benchmark]
    public void CreateAndDispose()
    {
        var argument = pool[cursor];
        cursor = cursor + 1 == poolSize ? 0 : cursor + 1;
        var expression = observer.Observe(person => person.Rank * 2, argument);
        expression.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        observer = new ExpressionObserver();
        existing = new List<IObservableExpression<BenchmarkPerson, int>>(ExistingExpressions);
        for (var i = 0; i < ExistingExpressions; ++i)
            existing.Add(observer.Observe(person => person.Rank * 2, new BenchmarkPerson("Existing", i)));
        pool = new BenchmarkPerson[poolSize];
        for (var i = 0; i < poolSize; ++i)
            pool[i] = new BenchmarkPerson("Pooled", i);
    }
}
