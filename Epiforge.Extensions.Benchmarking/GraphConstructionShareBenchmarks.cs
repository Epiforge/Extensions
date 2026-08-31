namespace Epiforge.Extensions.Benchmarking;

using System.Linq.Expressions;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class GraphConstructionShareBenchmarks
{
    static readonly Expression<Func<BenchmarkPerson, int>> selector = person => person.Rank * 2;

    ExpressionObserver observer = null!;
    BenchmarkPerson subject = null!;

    BinaryExpression BuildNormalized() =>
        Expression.Multiply(Expression.Property(Expression.Constant(subject, typeof(BenchmarkPerson)), nameof(BenchmarkPerson.Rank)), Expression.Constant(2));

    [Benchmark]
    public Expression BuildNormalizedOnly() =>
        BuildNormalized();

    [Benchmark]
    public int BuildThenDiagramEveryNode()
    {
        var normalized = BuildNormalized();
        var comparer = Epiforge.Extensions.Expressions.ExpressionEqualityComparer.Default;
        return comparer.GetHashCode(normalized) + comparer.GetHashCode(normalized.Left);
    }

    [Benchmark(Baseline = true)]
    public void GraphObservation() =>
        observer.Observe(selector, subject).Dispose();

    [GlobalSetup]
    public void Setup()
    {
        subject = new BenchmarkPerson("subject", 4);
        observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
    }
}
