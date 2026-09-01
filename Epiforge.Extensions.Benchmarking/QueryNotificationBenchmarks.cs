namespace Epiforge.Extensions.Benchmarking;

using System.Collections.Specialized;
using System.Linq.Expressions;

[MemoryDiagnoser]
public class QueryNotificationBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, bool>> predicate = person => person.Rank > 0;

    CollectionObserver observer = null!;
    BenchmarkPerson[] people = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;
    IObservableCollectionQuery<BenchmarkPerson> where = null!;

    [GlobalCleanup]
    public void Cleanup()
    {
        where.Dispose();
        sourceQuery.Dispose();
    }

    [Benchmark]
    public void FlipEveryMembershipWithASubscriber()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    [Benchmark(Baseline = true)]
    public void FlipEveryMembershipWithNothingObserving()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    static void Ignore(object? sender, NotifyCollectionChangedEventArgs e)
    {
    }

    [GlobalSetup(Target = nameof(FlipEveryMembershipWithASubscriber))]
    public void SetupObserved()
    {
        SetupSource();
        where.CollectionChanged += Ignore;
    }

    void SetupSource()
    {
        observer = new CollectionObserver();
        source = BenchmarkPerson.CreateCollection(elementCount);
        people = new BenchmarkPerson[elementCount];
        for (var i = 0; i < elementCount; ++i)
            people[i] = source[i];
        sourceQuery = observer.ObserveReadOnlyList(source);
        where = sourceQuery.ObserveWhere(predicate);
    }

    [GlobalSetup(Target = nameof(FlipEveryMembershipWithNothingObserving))]
    public void SetupUnobserved() =>
        SetupSource();
}
