namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices keeping a maximum of text current under a culture comparer, for a change which evicts the maximum and for one which leaves it alone, against the ordering and first element which does the same job
/// </summary>
/// <remarks>
/// GS5 measured the maximum rescanning every element whenever the element holding it changes back, and reverted to an ordering. This reproduces that at the two sizes it measured, 256 and 4,096, and adds the change which does not touch the maximum, which it did not measure. Each arm is a pair of changes which restores what it found: the eviction arms raise the smallest element above every other and then restore it, and the untouched arms lower the second smallest further and then restore it. Every mechanism and size has a collection of its own with its query standing, and the change nobody observes is the floor to subtract
/// </remarks>
[MemoryDiagnoser]
public class ComparisonEvictionBenchmarks
{
    /// <summary>
    /// A key comparing text under the invariant culture, a reference type as GS5's sort key is
    /// </summary>
    sealed class InvariantText(string value) :
        IComparable
    {
        readonly string value = value;

        public int CompareTo(object? obj) =>
            string.Compare(value, ((InvariantText)obj!).value, StringComparison.InvariantCulture);
    }

    /// <summary>
    /// The two elements a pair of changes is made to, and the names to restore them to
    /// </summary>
    sealed class Targets
    {
        public Targets(ObservableRangeCollection<BenchmarkPerson> people)
        {
            var ascending = people.OrderBy(person => person.Name, StringComparer.InvariantCulture).ToList();
            Evicted = ascending[0];
            EvictedName = Evicted.Name;
            Untouched = ascending[1];
            UntouchedName = Untouched.Name;
        }

        public readonly BenchmarkPerson Evicted;
        public readonly string EvictedName;
        public readonly BenchmarkPerson Untouched;
        public readonly string UntouchedName;
    }

    static readonly Expression<Func<BenchmarkPerson, IComparable>> invariantKey = person => new InvariantText(person.Name);
    static readonly Expression<Func<BenchmarkPerson, string>> nameRead = person => person.Name;

    CollectionObserver collectionObserver = null!;
    Targets max256 = null!;
    Targets max4096 = null!;
    Targets ordering256 = null!;
    Targets ordering4096 = null!;
    readonly List<IDisposable> standing = [];
    Targets unobserved = null!;

    [Benchmark]
    public void ChangeObservedByNothing() =>
        Evict(unobserved);

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = standing.Count - 1; i >= 0; --i)
            standing[i].Dispose();
        standing.Clear();
    }

    static ObservableRangeCollection<BenchmarkPerson> CreatePeople(int count, int seed)
    {
        var random = new Random(seed);
        var people = BenchmarkPerson.CreateCollection(count);
        foreach (var person in people)
            person.Name = $"{random.Next():x8}{random.Next():x8}";
        return people;
    }

    static void Evict(Targets targets)
    {
        targets.Evicted.Name = "zzzzzzzz";
        targets.Evicted.Name = targets.EvictedName;
    }

    static void LeaveAlone(Targets targets)
    {
        targets.Untouched.Name = "0";
        targets.Untouched.Name = targets.UntouchedName;
    }

    Targets Max(int count, int seed)
    {
        var people = CreatePeople(count, seed);
        Stand(Stand(collectionObserver.ObserveReadOnlyList(people)).ObserveMax(nameRead, StringComparer.InvariantCulture));
        return new(people);
    }

    [Benchmark]
    public void MaxEvicted256() =>
        Evict(max256);

    [Benchmark(Baseline = true)]
    public void MaxEvicted4096() =>
        Evict(max4096);

    [Benchmark]
    public void MaxLeftAlone256() =>
        LeaveAlone(max256);

    [Benchmark]
    public void MaxLeftAlone4096() =>
        LeaveAlone(max4096);

    Targets Ordering(int count, int seed)
    {
        var people = CreatePeople(count, seed);
        Stand(Stand(Stand(collectionObserver.ObserveReadOnlyList(people)).ObserveOrderBy(invariantKey, true)).ObserveFirst());
        return new(people);
    }

    [Benchmark]
    public void OrderingEvicted256() =>
        Evict(ordering256);

    [Benchmark]
    public void OrderingEvicted4096() =>
        Evict(ordering4096);

    [Benchmark]
    public void OrderingLeftAlone256() =>
        LeaveAlone(ordering256);

    [Benchmark]
    public void OrderingLeftAlone4096() =>
        LeaveAlone(ordering4096);

    [GlobalSetup]
    public void Setup()
    {
        collectionObserver = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        max256 = Max(256, 1);
        max4096 = Max(4096, 2);
        ordering256 = Ordering(256, 1);
        ordering4096 = Ordering(4096, 2);
        unobserved = new(CreatePeople(4096, 2));
    }

    T Stand<T>(T disposable)
        where T : IDisposable
    {
        standing.Add(disposable);
        return disposable;
    }
}
