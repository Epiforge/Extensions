namespace Epiforge.Extensions.Benchmarking;

using DynamicData;
using DynamicData.Binding;
using NMF.Expressions;
using NMF.Expressions.Linq;
using ObservableComputations;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Text;

static class QueryFootprintReport
{
    static readonly int[] elementCounts = [250, 1000, 4000, 10000];

    sealed class ByRank :
        IComparer<BenchmarkPerson>
    {
        internal static readonly ByRank Default = new();

        public int Compare(BenchmarkPerson? x, BenchmarkPerson? y) =>
            x!.Rank.CompareTo(y!.Rank);
    }

    static readonly (string Name, Func<BenchmarkPerson, Expression<Func<BenchmarkPerson, bool>>> Predicate)[] shapes =
    [
        ("OneNode", threshold => person => true),
        ("ThreeNodes", threshold => person => person.Rank > 0),
        ("FiveNodes", threshold => person => person.Rank % 2 == 0),
        ("ElevenNodes", threshold => person => person.Rank % 2 == 0 && person.Name.Length > 1),
        ("SharedChangeableSubexpression", threshold => person => person.Rank > threshold.Rank)
    ];

    static Reading Measure(int elementCount, Func<BenchmarkPerson, Expression<Func<BenchmarkPerson, bool>>> predicate)
    {
        var observer = new CollectionObserver();
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var threshold = new BenchmarkPerson("threshold", 0);
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(predicate(threshold));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        where.Dispose();
        sourceQuery.Dispose();
        where = null;
        sourceQuery = null;
        var afterDispose = Settle() - baseline;
        var cachedQueries = observer.CachedObservableQueries;
        var cachedExpressions = observer.ExpressionObserver.CachedObservableExpressions;
        observer = null;
        var afterObserverReleased = Settle() - baseline;
        GC.KeepAlive(source);
        GC.KeepAlive(threshold);
        return new(allocated, retained, afterDispose, afterObserverReleased, cachedQueries, cachedExpressions);
    }

    sealed record Reading(long Allocated, long Retained, long AfterDispose, long AfterObserverReleased, int CachedQueries, int CachedExpressions);

    sealed record ComparisonReading(long Allocated, long Retained, long AfterDispose);

    /// <summary>
    /// Measures what a filtered view of the same collection, under the same predicate, retains in each library
    /// </summary>
    /// <remarks>
    /// The baseline is taken after whatever the caller is expected to be holding anyway, so that each reading is what the view adds and not what the data costs. For this library that is the collection; for DynamicData's cache it is the collection and the cache, because holding a cache is the analogue of holding a collection rather than of building a view — which is why what the cache itself retains is measured on its own row, since a caller who has an <see cref="ObservableCollection{T}" /> and adopts DynamicData pays for both.
    /// </remarks>
    static ComparisonReading MeasureExpressionsFilter(int elementCount)
    {
        var observer = new CollectionObserver();
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(person => person.Rank > 0);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        where.Dispose();
        sourceQuery.Dispose();
        where = null;
        sourceQuery = null;
        observer = null;
        var afterDispose = Settle() - baseline;
        GC.KeepAlive(source);
        return new(allocated, retained, afterDispose);
    }

    /// <summary>
    /// Measures what this instrument reads when nothing whatever is built over the collection, which is the control every other row of this table depends on and which should read zero in all three columns
    /// </summary>
    static ComparisonReading MeasureNothing(int elementCount)
    {
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        var afterDispose = Settle() - baseline;
        GC.KeepAlive(source);
        return new(allocated, retained, afterDispose);
    }

    /// <summary>
    /// Measures the same filtered view as <see cref="MeasureExpressionsFilter(int)" />, but takes its baseline before the collection exists and drops the collection along with the query, so that what remains afterwards is what nothing in the measurement is holding any longer
    /// </summary>
    /// <remarks>
    /// This arm exists to discriminate between two readings of a high <c>After dispose</c> figure in the arm above: memory the elements are still holding because something outlived the query's disposal, and memory which nothing in the measurement can account for. If this returns near zero while the arm above does not, the elements are holding it
    /// </remarks>
    static ComparisonReading MeasureExpressionsFilterReleasingTheSource(int elementCount)
    {
        var baseline = Settle();
        var observer = new CollectionObserver();
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(person => person.Rank > 0);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        where.Dispose();
        sourceQuery.Dispose();
        where = null;
        sourceQuery = null;
        observer = null;
        source = null;
        var afterDispose = Settle() - baseline;
        return new(allocated, retained, afterDispose);
    }

    /// <summary>
    /// Builds and drops the same filtered view five times over five separate collections under a single baseline, so that what remains can be divided by the number of cycles
    /// </summary>
    /// <remarks>
    /// This is the arm which decides whether unreclaimed memory is a leak or a cost. Memory which grows with the number of cycles is being lost on every query a process ever builds; memory which does not is a one-time cost of first use, paid by a cache which fills in once. The DynamicData counterpart below is the control, because it is already known to return to its baseline in a single cycle and must therefore stay flat across five
    /// </remarks>
    static ComparisonReading MeasureExpressionsFilterOverFiveCycles(int elementCount)
    {
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var cycle = 0; cycle < 5; ++cycle)
            BuildAndDropAFilteredView(elementCount);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        var afterDispose = Settle() - baseline;
        return new(allocated, retained, afterDispose);
    }

    static void BuildAndDropAFilteredView(int elementCount)
    {
        var observer = new CollectionObserver();
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(person => person.Rank > 0);
        where.Dispose();
        sourceQuery.Dispose();
    }

    /// <summary>
    /// Creates a collection of the given size, reads what it retains, drops it, and reads what remains, without this library being involved at all
    /// </summary>
    /// <remarks>
    /// This is the control which isolates the library from the harness, and it should have been the first one written. If dropping a bare collection of this size returns to zero, then a residue left by an arm which also built a query belongs to the query. If it does not, the residue belongs to neither library and no figure in the <c>After dispose</c> column of this table means anything
    /// </remarks>
    static ComparisonReading MeasureCollectionAlone(int elementCount)
    {
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        GC.KeepAlive(source);
        source = null;
        var afterDispose = Settle() - baseline;
        return new(allocated, retained, afterDispose);
    }

    /// <summary>
    /// Runs the same five cycles as the arm above and then one more over ten elements, so that the last graph built is a tiny one
    /// </summary>
    /// <remarks>
    /// Five cycles were already shown to leave exactly one cycle's worth of memory, which admits two readings. If something holds a strong reference to the most recently built graph, then ending on a ten-element graph collapses the residue to almost nothing. If the residue instead stays at the full size, nothing is holding the last graph and the figure is a property of how much the arm allocated rather than of what it built
    /// </remarks>
    static ComparisonReading MeasureExpressionsFilterOverFiveCyclesThenATinyOne(int elementCount)
    {
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var cycle = 0; cycle < 5; ++cycle)
            BuildAndDropAFilteredView(elementCount);
        BuildAndDropAFilteredView(10);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        var afterDispose = Settle() - baseline;
        return new(allocated, retained, afterDispose);
    }

    /// <remarks>
    /// The cycle body is a separate method for the same reason its counterpart above is: a measurement which builds and drops inside the frame it then measures from reads that frame's dead locals as live, and an instrument may not apply that treatment to one library and not the other
    /// </remarks>
    static ComparisonReading MeasureDynamicDataListFilterOverFiveCycles(int elementCount)
    {
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var cycle = 0; cycle < 5; ++cycle)
            BuildAndDropADynamicDataListView(elementCount);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        var afterDispose = Settle() - baseline;
        return new(allocated, retained, afterDispose);
    }

    static void BuildAndDropADynamicDataListView(int elementCount)
    {
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var subscription = source.ToObservableChangeSet().AutoRefresh(person => person.Rank).Filter(person => person.Rank > 0).Bind(out var bound).Subscribe();
        subscription.Dispose();
    }

    static void BuildAndDropACollection(int elementCount)
    {
        var source = BenchmarkPerson.CreateCollection(elementCount);
        GC.KeepAlive(source);
    }

    /// <summary>
    /// Creates and drops a collection of the given size five times from a method which has returned each time, so that the bare-collection control can be read without the frame artifact the control itself demonstrated
    /// </summary>
    static ComparisonReading MeasureCollectionAloneOverFiveCycles(int elementCount)
    {
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var cycle = 0; cycle < 5; ++cycle)
            BuildAndDropACollection(elementCount);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        var afterDispose = Settle() - baseline;
        return new(allocated, retained, afterDispose);
    }

    /// <summary>
    /// Measures what a live grouped view retains in this library, sixteen ways over the collection
    /// </summary>
    /// <remarks>
    /// Grouping was never measured for retention, only filtering, and the two are not the same shape in either library: a grouping here is a collection per group, and DynamicData's is a keyed cache per group. The question this answers is how much headroom there is before an index kept per occurrence — which would cost roughly a treap node and a dictionary entry per element — would put this library behind
    /// </remarks>
    static ComparisonReading MeasureExpressionsGrouping(int elementCount)
    {
        var observer = new CollectionObserver();
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var groupBy = sourceQuery.ObserveGroupBy(person => person.Rank % 16);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        groupBy.Dispose();
        sourceQuery.Dispose();
        groupBy = null;
        sourceQuery = null;
        observer = null;
        var afterDispose = Settle() - baseline;
        GC.KeepAlive(source);
        return new(allocated, retained, afterDispose);
    }

    static ComparisonReading MeasureDynamicDataCacheGrouping(int elementCount)
    {
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var cache = new SourceCache<BenchmarkPerson, string>(person => person.Name);
        cache.AddOrUpdate(source);
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var groups = cache.Connect().AutoRefresh(person => person.Rank).Group(person => person.Rank % 16).AsObservableCache();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        groups.Dispose();
        groups = null;
        var afterDispose = Settle() - baseline;
        GC.KeepAlive(source);
        GC.KeepAlive(cache);
        return new(allocated, retained, afterDispose);
    }

    static ComparisonReading MeasureDynamicDataCacheItself(int elementCount)
    {
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var cache = new SourceCache<BenchmarkPerson, string>(person => person.Name);
        cache.AddOrUpdate(source);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        cache.Dispose();
        cache = null;
        var afterDispose = Settle() - baseline;
        GC.KeepAlive(source);
        return new(allocated, retained, afterDispose);
    }

    static ComparisonReading MeasureDynamicDataCacheFilter(int elementCount)
    {
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var cache = new SourceCache<BenchmarkPerson, string>(person => person.Name);
        cache.AddOrUpdate(source);
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var subscription = cache.Connect().AutoRefresh(person => person.Rank).Filter(person => person.Rank > 0).Bind(out var bound).Subscribe();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        subscription.Dispose();
        subscription = null;
        bound = null!;
        var afterDispose = Settle() - baseline;
        GC.KeepAlive(source);
        GC.KeepAlive(cache);
        return new(allocated, retained, afterDispose);
    }

    static ComparisonReading MeasureDynamicDataListFilter(int elementCount)
    {
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var subscription = source.ToObservableChangeSet().AutoRefresh(person => person.Rank).Filter(person => person.Rank > 0).Bind(out var bound).Subscribe();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        subscription.Dispose();
        subscription = null;
        bound = null!;
        var afterDispose = Settle() - baseline;
        GC.KeepAlive(source);
        return new(allocated, retained, afterDispose);
    }

    static void BuildAndDropAnNmfView(int elementCount)
    {
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var where = ((IEnumerable<BenchmarkPerson>)source).WithUpdates().Where(person => person.Rank > 0);
        where.Successors.SetDummy();
        where.Dispose();
    }

    static void BuildAndDropAnObservableComputationsView(int elementCount)
    {
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var consumer = new OcConsumer();
        source.Filtering(person => person.Rank > 0).For(consumer);
        consumer.Dispose();
    }

    static ComparisonReading MeasureDynamicDataCacheOrdering(int elementCount)
    {
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var cache = new SourceCache<BenchmarkPerson, string>(person => person.Name);
        cache.AddOrUpdate(source);
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var subscription = cache.Connect().AutoRefresh(person => person.Rank).SortAndBind(out var sorted, ByRank.Default).Subscribe();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        GC.KeepAlive(sorted);
        subscription.Dispose();
        subscription = null;
        sorted = null!;
        var afterDispose = Settle() - baseline;
        GC.KeepAlive(source);
        GC.KeepAlive(cache);
        return new(allocated, retained, afterDispose);
    }

    /// <summary>
    /// Measures what observing a dictionary retains with nothing built over the observation, which since 7.0.1 is the copy the observation keeps so that what is read of it always matches what it has announced
    /// </summary>
    static ComparisonReading MeasureExpressionsDictionaryObservationAlone(int elementCount)
    {
        var observer = new CollectionObserver();
        var source = new ObservableDictionary<int, BenchmarkPerson>();
        for (var i = 0; i < elementCount; ++i)
            source.Add(i, new BenchmarkPerson($"P{i}", i + 1));
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var sourceQuery = observer.ObserveReadOnlyDictionary(source);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        sourceQuery.Dispose();
        sourceQuery = null;
        observer = null;
        var afterDispose = Settle() - baseline;
        GC.KeepAlive(source);
        return new(allocated, retained, afterDispose);
    }

    /// <summary>
    /// Measures what observing a collection retains with nothing built over the observation, which since 7.0.1 is the copy the observation keeps so that what is read of it always matches what it has announced
    /// </summary>
    static ComparisonReading MeasureExpressionsObservationAlone(int elementCount)
    {
        var observer = new CollectionObserver();
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        sourceQuery.Dispose();
        sourceQuery = null;
        observer = null;
        var afterDispose = Settle() - baseline;
        GC.KeepAlive(source);
        return new(allocated, retained, afterDispose);
    }

    static ComparisonReading MeasureExpressionsOrdering(int elementCount)
    {
        var observer = new CollectionObserver();
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var orderBy = sourceQuery.ObserveOrderBy(person => person.Rank);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        orderBy.Dispose();
        sourceQuery.Dispose();
        orderBy = null;
        sourceQuery = null;
        observer = null;
        var afterDispose = Settle() - baseline;
        GC.KeepAlive(source);
        return new(allocated, retained, afterDispose);
    }

    /// <summary>
    /// Measures what a view built with NMF Expressions retains, in the form its documentation leads with: over <c>WithUpdates</c>, given a dummy successor, because an NMF view with nothing attached to it does not follow its source
    /// </summary>
    static ComparisonReading MeasureNmf(int elementCount, Func<INotifyEnumerable<BenchmarkPerson>, INotifyEnumerable> build)
    {
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var view = build(((IEnumerable<BenchmarkPerson>)source).WithUpdates());
        view.Successors.SetDummy();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        ((IDisposable)view).Dispose();
        view = null;
        var afterDispose = Settle() - baseline;
        GC.KeepAlive(source);
        return new(allocated, retained, afterDispose);
    }

    static ComparisonReading MeasureNmfFilter(int elementCount) =>
        MeasureNmf(elementCount, updates => updates.Where(person => person.Rank > 0));

    static ComparisonReading MeasureNmfFilterOverFiveCycles(int elementCount)
    {
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var cycle = 0; cycle < 5; ++cycle)
            BuildAndDropAnNmfView(elementCount);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        var afterDispose = Settle() - baseline;
        return new(allocated, retained, afterDispose);
    }

    static ComparisonReading MeasureNmfGrouping(int elementCount) =>
        MeasureNmf(elementCount, updates => updates.GroupBy(person => person.Rank % 16));

    static ComparisonReading MeasureNmfOrdering(int elementCount) =>
        MeasureNmf(elementCount, updates => updates.OrderBy(person => person.Rank));

    /// <summary>
    /// Measures what a view built with ObservableComputations retains, in the form its documentation leads with: bound to an <c>OcConsumer</c>, whose disposal tears it down
    /// </summary>
    static ComparisonReading MeasureObservableComputations(int elementCount, Action<ObservableRangeCollection<BenchmarkPerson>, OcConsumer> build)
    {
        var source = BenchmarkPerson.CreateCollection(elementCount);
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var consumer = new OcConsumer();
        build(source, consumer);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        consumer.Dispose();
        consumer = null;
        var afterDispose = Settle() - baseline;
        GC.KeepAlive(source);
        return new(allocated, retained, afterDispose);
    }

    static ComparisonReading MeasureObservableComputationsFilter(int elementCount) =>
        MeasureObservableComputations(elementCount, (source, consumer) => source.Filtering(person => person.Rank > 0).For(consumer));

    static ComparisonReading MeasureObservableComputationsFilterOverFiveCycles(int elementCount)
    {
        var baseline = Settle();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var cycle = 0; cycle < 5; ++cycle)
            BuildAndDropAnObservableComputationsView(elementCount);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = Settle() - baseline;
        var afterDispose = Settle() - baseline;
        return new(allocated, retained, afterDispose);
    }

    static ComparisonReading MeasureObservableComputationsGrouping(int elementCount) =>
        MeasureObservableComputations(elementCount, (source, consumer) => source.Grouping(person => person.Rank % 16).For(consumer));

    static ComparisonReading MeasureObservableComputationsOrdering(int elementCount) =>
        MeasureObservableComputations(elementCount, (source, consumer) => source.Ordering(person => person.Rank).For(consumer));

    public static void Run()
    {
        foreach (var (_, predicate) in shapes)
            Measure(elementCounts[0], predicate);
        var report = new StringBuilder();
        report.AppendLine("# Query footprint report");
        report.AppendLine();
        report.AppendLine($"Taken {DateTime.Now:yyyy-MM-dd HH:mm}, {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}, server GC {(System.Runtime.GCSettings.IsServerGC ? "on" : "off")}.");
        report.AppendLine();
        report.AppendLine("`Allocated` is every byte construction touched. `Retained` is what the live graph occupies once settled. `After dispose` is what remains once the query is disposed but the observer is still referenced. `After release` is what remains once the observer is dropped too, and is the control: if it is near zero, the measurement is sound and anything in the previous column was being held by the observer. `Cached` are the observer's own counts of queries and expressions still cached after disposal, both of which should be zero.");
        report.AppendLine();
        report.AppendLine("| Shape | Elements | Allocated | Retained | Retained per element | After dispose | After release | Cached queries | Cached expressions |");
        report.AppendLine("|--- |---: |---: |---: |---: |---: |---: |---: |---: |");
        foreach (var elementCount in elementCounts)
        {
            foreach (var (name, predicate) in shapes)
            {
                var reading = Measure(elementCount, predicate);
                var perElement = (double)reading.Retained / elementCount;
                report.AppendLine($"| `{name}` | {elementCount:N0} | {reading.Allocated:N0} B | {reading.Retained:N0} B | {perElement:N1} B | {reading.AfterDispose:N0} B | {reading.AfterObserverReleased:N0} B | {reading.CachedQueries:N0} | {reading.CachedExpressions:N0} |");
            }
        }
        report.AppendLine();
        report.AppendLine("## What a view retains, against DynamicData, NMF Expressions and ObservableComputations");
        report.AppendLine();
        report.AppendLine("Every row is the same predicate over the same collection. `Retained` is what the standing view occupies once settled, above whatever the caller holds anyway: the collection for this library and for DynamicData's list, the collection and the cache for DynamicData's cache. **What the cache itself retains is its own row**, because a caller who has an `ObservableCollection<T>` and adopts DynamicData pays for that as well as for the view.");
        report.AppendLine();
        report.AppendLine("**The last two rows group rather than filter**, sixteen ways, because grouping had never been measured for retention and the two operators are not the same shape in either library — a grouping here is a collection per group and DynamicData's is a keyed cache per group.");
        report.AppendLine();
        report.AppendLine("**Read the `Retained` column and disregard `After dispose` on any row whose work happens inline.** A method which builds an object graph, drops it, and then measures from that same frame reads its own dead locals as live: the source-level variable is null and the reference the JIT spilled elsewhere in the frame is not. `The collection alone, then released` demonstrates this with no library involved at all — it creates a collection, drops it, and still reads 96 B per element — so a residue on any inline row is the harness's and says nothing about what that row built.");
        report.AppendLine();
        report.AppendLine("**The `five cycles` rows are the ones which measure release, because their cycle bodies are separate methods which have returned before anything is measured.** Each builds and drops the whole arrangement five times under one baseline. Compare each against its own `five cycles` counterpart and never against an inline row; a figure which grows with the number of cycles is memory lost on every query a process builds. `five cycles then one over ten elements` ends on a tiny graph, so a residue which is really the most recently built graph would collapse there.");
        report.AppendLine();
        report.AppendLine("**The rows after DynamicData's grouped view** are NMF Expressions and ObservableComputations, each in the form its documentation leads with, filtering, sorting and grouping the same collection, and a `five cycles` row for each so that release can be read the same way; a sorted view of each of the four libraries; and this library's observation of a collection and of a dictionary with nothing built over them, which is the copy each keeps. Nothing reads any view before it is measured.");
        report.AppendLine();
        report.AppendLine("`nothing built` takes the baseline and then does nothing at all, so all three of its columns must read zero; anything else means this instrument cannot measure retention and no other figure in either table may be quoted.");
        report.AppendLine();
        report.AppendLine("| Shape | Elements | Allocated | Retained | Retained per element | After dispose |");
        report.AppendLine("|--- |---: |---: |---: |---: |---: |");
        foreach (var elementCount in elementCounts)
            foreach (var (name, measure) in new (string Name, Func<int, ComparisonReading> Measure)[]
            {
                ("Expressions, nothing built (control)", MeasureNothing),
                ("The collection alone, then released (control)", MeasureCollectionAlone),
                ("The collection alone, five cycles (control)", MeasureCollectionAloneOverFiveCycles),
                ("Expressions", MeasureExpressionsFilter),
                ("Expressions, releasing the collection too", MeasureExpressionsFilterReleasingTheSource),
                ("Expressions, five build-and-drop cycles", MeasureExpressionsFilterOverFiveCycles),
                ("Expressions, five cycles then one over ten elements", MeasureExpressionsFilterOverFiveCyclesThenATinyOne),
                ("DynamicData list, five build-and-drop cycles", MeasureDynamicDataListFilterOverFiveCycles),
                ("DynamicData cache, the cache alone", MeasureDynamicDataCacheItself),
                ("DynamicData cache, the view over it", MeasureDynamicDataCacheFilter),
                ("DynamicData list, the view over the collection", MeasureDynamicDataListFilter),
                ("Expressions, a grouped view", MeasureExpressionsGrouping),
                ("DynamicData cache, a grouped view over it", MeasureDynamicDataCacheGrouping),
                ("NMF Expressions, the view over the collection", MeasureNmfFilter),
                ("NMF Expressions, five build-and-drop cycles", MeasureNmfFilterOverFiveCycles),
                ("NMF Expressions, a grouped view", MeasureNmfGrouping),
                ("ObservableComputations, the view over the collection", MeasureObservableComputationsFilter),
                ("ObservableComputations, five build-and-drop cycles", MeasureObservableComputationsFilterOverFiveCycles),
                ("ObservableComputations, a grouped view", MeasureObservableComputationsGrouping),
                ("Expressions, a sorted view", MeasureExpressionsOrdering),
                ("DynamicData cache, a sorted view over it", MeasureDynamicDataCacheOrdering),
                ("NMF Expressions, a sorted view", MeasureNmfOrdering),
                ("ObservableComputations, a sorted view", MeasureObservableComputationsOrdering),
                ("Expressions, observing the collection alone", MeasureExpressionsObservationAlone),
                ("Expressions, observing a dictionary alone", MeasureExpressionsDictionaryObservationAlone)
            })
            {
                var reading = measure(elementCount);
                report.AppendLine($"| `{name}` | {elementCount:N0} | {reading.Allocated:N0} B | {reading.Retained:N0} B | {(double)reading.Retained / elementCount:N1} B | {reading.AfterDispose:N0} B |");
            }
        var text = report.ToString();
        Console.Write(text);
        var path = Path.Combine(AppContext.BaseDirectory, "footprint-report.md");
        File.WriteAllText(path, text);
        Console.WriteLine();
        Console.WriteLine($"written to {path}");
    }

    static long Settle()
    {
        for (var attempt = 0; attempt < 3; ++attempt)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        return GC.GetTotalMemory(true);
    }
}
