namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Decomposes what an ordered key change costs into the stages which make it up, by standing up each stage of the arrangement the ordered query builds internally and stopping there
/// </summary>
/// <remarks>
/// An ordered key change costs this library about 1,124 ns of fixed overhead at a thousand elements, which is the whole of its deficit against DynamicData below about 1,470 elements. Where that time goes has so far been <b>estimated from reading the code and never measured</b>, and the two candidate remedies point at opposite ends of the chain: removing the intermediate selection which computes the keys, or making the reposition's search and comparisons cheaper. This instrument decides which, so that the work goes where the time is
/// </remarks>
/// <remarks>
/// The arms are cumulative, and each difference prices one stage:
/// <list type="bullet">
/// <item><description><c>KeyAlone</c> less <c>Unobserved</c> — observing a key per element and maintaining a selection over it, with no boxing and no pairing</description></item>
/// <item><description><c>KeyBoxed</c> less <c>KeyAlone</c> — what boxing the key into an <see cref="IComparable" /> costs, which is what a generic overload would remove</description></item>
/// <item><description><c>KeyPaired</c> less <c>KeyBoxed</c> — what pairing the key with its element into a <see cref="Tuple{T1, T2}" /> costs, which is the shape the ordered query actually builds</description></item>
/// <item><description><c>Ordered</c> less <c>KeyPaired</c> — what the comparer and the reposition cost on top of the selection they read</description></item>
/// </list>
/// </remarks>
/// <remarks>
/// <c>KeyPaired</c> writes the wrapped selector as a lambda where the ordered query builds the equivalent expression through <c>LambdaInvocationRewriter</c>. The two are the same shape; if the arms below ever disagree with the ordered query's own measured figure by more than the last stage accounts for, that difference is the first thing to suspect
/// </remarks>
[MemoryDiagnoser]
public class OrderByKeyChangeStageBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, IComparable>> boxedSelector = person => person.Rank;
    static readonly Expression<Func<BenchmarkPerson, int>> plainSelector = person => person.Rank;
    static readonly Expression<Func<BenchmarkPerson, Tuple<BenchmarkPerson, IComparable>>> pairedSelector = person => new Tuple<BenchmarkPerson, IComparable>(person, person.Rank);

    IObservableCollectionQuery<int> keyAlone = null!;
    IObservableCollectionQuery<IComparable> keyBoxed = null!;
    IObservableCollectionQuery<Tuple<BenchmarkPerson, IComparable>> keyPaired = null!;
    CollectionObserver observer = null!;
    IObservableCollectionQuery<BenchmarkPerson> orderBy = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;

    void ChangeEveryRank()
    {
        for (var i = 0; i < elementCount; ++i)
            source[i].Rank ^= 2;
    }

    /// <summary>
    /// What the changes cost before anything observes them, which is the floor every arm below stands on
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ChangeEveryRankUnobserved() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankObservingTheKeyAlone() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankObservingTheKeyBoxed() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankObservingTheKeyPaired() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankOrdered() =>
        ChangeEveryRank();

    [GlobalCleanup(Target = nameof(ChangeEveryRankObservingTheKeyAlone))]
    public void CleanupKeyAlone()
    {
        keyAlone.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryRankObservingTheKeyBoxed))]
    public void CleanupKeyBoxed()
    {
        keyBoxed.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryRankObservingTheKeyPaired))]
    public void CleanupKeyPaired()
    {
        keyPaired.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryRankOrdered))]
    public void CleanupOrdered()
    {
        orderBy.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankObservingTheKeyAlone))]
    public void SetupKeyAlone()
    {
        SetupSourceQuery();
        keyAlone = sourceQuery.ObserveSelect(plainSelector);
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankObservingTheKeyBoxed))]
    public void SetupKeyBoxed()
    {
        SetupSourceQuery();
        keyBoxed = sourceQuery.ObserveSelect(boxedSelector);
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankObservingTheKeyPaired))]
    public void SetupKeyPaired()
    {
        SetupSourceQuery();
        keyPaired = sourceQuery.ObserveSelect(pairedSelector);
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankOrdered))]
    public void SetupOrdered()
    {
        SetupSourceQuery();
        orderBy = sourceQuery.ObserveOrderBy(boxedSelector);
    }

    void SetupSourceQuery()
    {
        source = BenchmarkPerson.CreateCollection(elementCount);
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyList(source);
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankUnobserved))]
    public void SetupUnobserved() =>
        source = BenchmarkPerson.CreateCollection(elementCount);
}
