namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Changes a collection on one thread while another waits for a condition over it, many at a time, and counts what went wrong: a wait never met although its condition held, a fault, and a node still cached once everything was disposed of
/// </summary>
/// <remarks>
/// This is the shape in which a consumer met the observer losing a change raised on another thread while an observation was being built. Nothing here can force the interleaving it looks for, so it runs enough of them for a loss to be seen, and a zero is an absence of evidence rather than a proof; the tests which force each interleaving are what prove a fix, and this is what shows the consumer's shape no longer meets the defect.
/// A wait counts as unmet only if it has still not completed two seconds after both the wait was set up and the reset had finished, polled rather than awaited, so that a machine too busy to run a continuation promptly is not mistaken for a lost change
/// </remarks>
static class ConcurrencyStressReport
{
    static readonly TimeSpan patience = TimeSpan.FromSeconds(2);

    sealed record Reading(int Unmet, IReadOnlyDictionary<string, int> Faults, int CachedExpressions, TimeSpan Elapsed);

    static async Task<Reading> MeasureAsync(bool useDirectSubscription, bool throughCondition, int iterations)
    {
        var observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = useDirectSubscription });
        var unmet = 0;
        var faults = new ConcurrentDictionary<string, int>();
        var stopwatch = Stopwatch.StartNew();
        await Parallel.ForEachAsync(Enumerable.Range(0, iterations), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (iteration, _) =>
        {
            var random = new Random(iteration);
            var inner = new ObservableRangeCollection<string>(["b", "a"]);
            var collection = new ReadOnlyObservableCollection<string>(inner);
            var changing = Task.Run(() =>
            {
                Thread.SpinWait(random.Next(0, 20000));
                inner.Reset(["a", "b"]);
            });
            using var cancellation = new CancellationTokenSource();
            IDisposable? observation = null;
            Task waiting;
            try
            {
                if (throughCondition)
                    waiting = observer.ConditionAsync(() => collection[0] == "a", cancellation.Token);
                else
                    (waiting, observation) = Wait(observer, collection, cancellation.Token);
            }
            catch (Exception ex)
            {
                faults.AddOrUpdate(ex.GetType().Name, 1, (_, count) => count + 1);
                await changing.ConfigureAwait(false);
                return;
            }
            await changing.ConfigureAwait(false);
            if (!SpinWait.SpinUntil(() => waiting.IsCompleted, patience))
            {
                Interlocked.Increment(ref unmet);
                cancellation.Cancel();
            }
            try
            {
                await waiting.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                faults.AddOrUpdate(ex.GetType().Name, 1, (_, count) => count + 1);
            }
            observation?.Dispose();
        }).ConfigureAwait(false);
        return new(unmet, faults, observer.CachedObservableExpressions, stopwatch.Elapsed);
    }

    internal static async Task RunAsync(int iterations)
    {
        var report = new StringBuilder();
        report.AppendLine("# Concurrency stress report");
        report.AppendLine();
        report.AppendLine($"Taken {DateTime.Now:yyyy-MM-dd HH:mm}, {Environment.ProcessorCount} logical processors, eight waits at a time, each against a reset raised on another thread after a random spin.");
        report.AppendLine();
        report.AppendLine("`Unmet` counts waits still not met two seconds after the reset had finished, although the reset made the condition true. `Cached` is the observer's count of nodes still cached once every wait was over, which should be zero.");
        report.AppendLine();
        report.AppendLine("| Waiting through | `UseDirectSubscription` | Iterations | Unmet | Faults | Cached | Elapsed |");
        report.AppendLine("|--- |--- |---: |---: |--- |---: |---: |");
        foreach (var (throughCondition, useDirectSubscription) in new[] { (true, false), (true, true), (false, false), (false, true) })
        {
            var reading = await MeasureAsync(useDirectSubscription, throughCondition, iterations).ConfigureAwait(false);
            var faults = reading.Faults.Count == 0 ? "none" : string.Join(", ", reading.Faults.OrderBy(fault => fault.Key).Select(fault => $"{fault.Key} {fault.Value:N0}"));
            report.AppendLine($"| {(throughCondition ? "`ConditionAsync`, which builds the graph" : "an observation taking an argument")} | {useDirectSubscription} | {iterations:N0} | {reading.Unmet:N0} | {faults} | {reading.CachedExpressions:N0} | {reading.Elapsed.TotalSeconds:N0} s |");
        }
        var text = report.ToString();
        Console.Write(text);
        var path = Path.Combine(AppContext.BaseDirectory, "stress-report.md");
        File.WriteAllText(path, text);
        Console.WriteLine();
        Console.WriteLine($"written to {path}");
    }

    /// <summary>
    /// Waits as <c>ConditionAsync</c> does but through an observation taking an argument, yielding a task which completes on the thread meeting the condition, so that whether it has been met can be read without a thread to run a continuation
    /// </summary>
    static (Task Waiting, IDisposable Observation) Wait(ExpressionObserver observer, ReadOnlyObservableCollection<string> collection, CancellationToken cancellationToken)
    {
        var met = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observation = observer.Observe(static (ReadOnlyObservableCollection<string> c) => c[0] == "a", collection);
        void evaluationChanged()
        {
            if (observation.Evaluation.Fault is { } fault)
                met.TrySetException(fault);
            else if (observation.Evaluation.Result)
                met.TrySetResult();
        }
        observation.PropertyChanged += (_, _) => evaluationChanged();
        evaluationChanged();
        cancellationToken.Register(() => met.TrySetCanceled(cancellationToken));
        return (met.Task, observation);
    }
}
