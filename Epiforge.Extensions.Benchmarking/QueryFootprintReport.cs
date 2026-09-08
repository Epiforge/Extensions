namespace Epiforge.Extensions.Benchmarking;

using DynamicData;
using DynamicData.Binding;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Text;

static class QueryFootprintReport
{
    static readonly int[] elementCounts = [250, 1000, 4000, 10000];

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
        report.AppendLine("## What a filtered view retains, against DynamicData");
        report.AppendLine();
        report.AppendLine("Every row is the same predicate over the same collection. `Retained` is what the standing view occupies once settled, above whatever the caller holds anyway: the collection for this library and for DynamicData's list, the collection and the cache for DynamicData's cache. **What the cache itself retains is its own row**, because a caller who has an `ObservableCollection<T>` and adopts DynamicData pays for that as well as for the view.");
        report.AppendLine();
        report.AppendLine("**Two rows are controls rather than results.** `nothing built` takes the baseline and then does nothing at all, so all three of its columns must read zero; anything else means this instrument cannot measure retention and no other figure in either table may be quoted. `releasing the collection too` takes its baseline before the collection exists and then drops the collection along with the query, so it must return near zero as well; if it does while the plain `Expressions` row does not, then what that row still shows after disposal is being held by the elements themselves.");
        report.AppendLine();
        report.AppendLine("| Shape | Elements | Allocated | Retained | Retained per element | After dispose |");
        report.AppendLine("|--- |---: |---: |---: |---: |---: |");
        foreach (var elementCount in elementCounts)
            foreach (var (name, measure) in new (string Name, Func<int, ComparisonReading> Measure)[]
            {
                ("Expressions, nothing built (control)", MeasureNothing),
                ("Expressions", MeasureExpressionsFilter),
                ("Expressions, releasing the collection too", MeasureExpressionsFilterReleasingTheSource),
                ("DynamicData cache, the cache alone", MeasureDynamicDataCacheItself),
                ("DynamicData cache, the view over it", MeasureDynamicDataCacheFilter),
                ("DynamicData list, the view over the collection", MeasureDynamicDataListFilter)
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
