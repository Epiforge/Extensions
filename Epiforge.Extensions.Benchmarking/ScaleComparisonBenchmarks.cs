namespace Epiforge.Extensions.Benchmarking;

using DynamicData;
using DynamicData.Binding;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

/// <summary>
/// Asks how a live filtered view of a collection scales in each library, by measuring the same construction and the same thousandfold-to-hundred-thousandfold worth of property changes at a thousand, ten thousand and a hundred thousand elements
/// </summary>
/// <remarks>
/// This instrument uses <see cref="ParamsAttribute" /> where the convention here is not to, because the element count is the question rather than a dimension of it. Everything else follows the convention: one control, arms which decide something, and no arm which repeats what another already says.
/// </remarks>
/// <remarks>
/// Only DynamicData's cache is measured. Its list path costs about 210 times what its cache does for the same property changes at a thousand elements, so at a hundred thousand it would be measuring nothing but that, at a cost in wall-clock which would swamp the run. What the list path costs is recorded in `2026-09-08-measured-against-dynamic-data.md` and is a caveat about an idiom rather than a scaling question.
/// </remarks>
/// <remarks>
/// Construction at a hundred thousand elements allocates hundreds of megabytes per operation in at least one of the arms. That is not a defect of the instrument; it is the answer to the question, and it is why the question is worth asking of a library whose per-element construction floor is known.
/// </remarks>
[MemoryDiagnoser]
public class ScaleComparisonBenchmarks
{
    static readonly Expression<Func<BenchmarkPerson, bool>> predicate = person => person.Rank > 0;

    SourceCache<BenchmarkPerson, string> cache = null!;
    ReadOnlyObservableCollection<BenchmarkPerson> cacheResults = null!;
    IDisposable cacheSubscription = null!;
    CollectionObserver observer = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;
    IObservableCollectionQuery<BenchmarkPerson> where = null!;

    [Params(1_000, 10_000, 100_000)]
    public int ElementCount { get; set; }

    void ChangeEveryRank()
    {
        for (var i = 0; i < ElementCount; ++i)
            source[i].Rank ^= 2;
    }

    /// <summary>
    /// What changing every element's rank costs before anything observes it, which is the floor the two arms below stand on and the only thing here which grows for a reason neither library controls
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ChangeEveryRankUnobserved() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankWithDynamicDataCache() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankWithExpressions() =>
        ChangeEveryRank();

    [Benchmark]
    public void ConstructAndDisposeWithDynamicDataCache()
    {
        var subscription = cache
            .Connect()
            .AutoRefresh(person => person.Rank)
            .Filter(person => person.Rank > 0)
            .Bind(out var bound)
            .Subscribe();
        subscription.Dispose();
    }

    [Benchmark]
    public void ConstructAndDisposeWithExpressions()
    {
        var query = observer.ObserveReadOnlyList(source);
        var filtered = query.ObserveWhere(predicate);
        filtered.Dispose();
        query.Dispose();
    }

    [GlobalCleanup(Targets = [nameof(ChangeEveryRankWithDynamicDataCache), nameof(ConstructAndDisposeWithDynamicDataCache)])]
    public void CleanupCache()
    {
        cacheSubscription?.Dispose();
        cache.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryRankWithExpressions))]
    public void CleanupStandingExpressions()
    {
        where.Dispose();
        sourceQuery.Dispose();
    }

    void SetupCache()
    {
        SetupSource();
        cache = new SourceCache<BenchmarkPerson, string>(person => person.Name);
        cache.AddOrUpdate(source);
    }

    [GlobalSetup(Target = nameof(ConstructAndDisposeWithDynamicDataCache))]
    public void SetupConstructCache() =>
        SetupCache();

    [GlobalSetup(Target = nameof(ConstructAndDisposeWithExpressions))]
    public void SetupConstructExpressions()
    {
        SetupSource();
        observer = new CollectionObserver();
    }

    void SetupSource() =>
        source = BenchmarkPerson.CreateCollection(ElementCount);

    [GlobalSetup(Target = nameof(ChangeEveryRankWithDynamicDataCache))]
    public void SetupStandingCache()
    {
        SetupCache();
        cacheSubscription = cache
            .Connect()
            .AutoRefresh(person => person.Rank)
            .Filter(person => person.Rank > 0)
            .Bind(out cacheResults)
            .Subscribe();
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankWithExpressions))]
    public void SetupStandingExpressions()
    {
        SetupSource();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyList(source);
        where = sourceQuery.ObserveWhere(predicate);
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankUnobserved))]
    public void SetupUnobserved() =>
        SetupSource();
}
