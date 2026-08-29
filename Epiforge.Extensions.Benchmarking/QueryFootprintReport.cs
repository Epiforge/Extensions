namespace Epiforge.Extensions.Benchmarking;

using System.Linq.Expressions;
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
