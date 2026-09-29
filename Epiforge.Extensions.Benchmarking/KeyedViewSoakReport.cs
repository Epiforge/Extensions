namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Changes a collection on one thread while three others change the keys and values of its elements, many rounds at a time, and then checks a grouping, a lookup, an ordering by two keys, a dictionary and a rank over it against what LINQ makes of the collection as it was left
/// </summary>
/// <remarks>
/// The grouping, the lookup, the ordering and the dictionary observe each element's key themselves and hear the collection's changes before its own handlers do, so a change to the collection and a change to a key meet under their locks rather than under one query's beneath them. Nothing here can force the interleaving which would lose one of them, so it runs enough of them for a loss to be seen, and a zero is an absence of evidence rather than a proof.
/// A key is twelve divided by three less than the rank, so a rank of three makes it throw, and a view must leave that element out and report the fault while the rank stays there. The ordering's second key is the name, descending, and the dictionary maps each key to the name of the element earliest in the collection which claims it. Every view is disposed of at the end of its round, after which the observer must hold nothing cached. Progress is written every ten seconds, and a minute without a round finishing is called out, since a stuck round would be a finding rather than a slow one
/// </remarks>
static class KeyedViewSoakReport
{
    const int changesPerThread = 3000;
    const int collectionSize = 32;
    const int poolSize = 64;

    static readonly string[] names = ["a", "b", "c", "d"];
    static readonly Expression<Func<BenchmarkPerson, int>> key = person => 12 / (person.Rank - 3);
    static readonly Expression<Func<BenchmarkPerson, IComparable>> comparableKey = person => 12 / (person.Rank - 3);
    static readonly Expression<Func<BenchmarkPerson, IComparable>> comparableName = person => person.Name;
    static readonly Expression<Func<BenchmarkPerson, string>> name = person => person.Name;

    sealed record Reading(int Rounds, IReadOnlyDictionary<string, int> Mismatches, IReadOnlyDictionary<string, int> Faults, int LeakedRounds, string? FirstMismatch, TimeSpan Elapsed);

    static bool Evaluates(BenchmarkPerson person) =>
        person.Rank != 3;

    static int KeyOf(BenchmarkPerson person) =>
        12 / (person.Rank - 3);

    static IEnumerable<(string View, string Detail)> Check(IReadOnlyList<BenchmarkPerson> source, IObservableCollectionQuery<IObservableGrouping<int, BenchmarkPerson>> grouping, IObservableLookupQuery<int, BenchmarkPerson> lookup, IObservableCollectionQuery<BenchmarkPerson> ordering, IObservableDictionaryQuery<int, string> dictionary, IObservableScalarQuery<(int Ahead, int Tied)> rank, BenchmarkPerson ranked)
    {
        var anyFault = source.Any(person => !Evaluates(person));
        var evaluated = source.Where(Evaluates).ToList();
        var expectedGroups = Groups(evaluated.GroupBy(KeyOf).Select(group => (group.Key, (IEnumerable<BenchmarkPerson>)group)));
        var actualGroups = Groups(grouping.Select(group => (group.Key, (IEnumerable<BenchmarkPerson>)group)));
        if (actualGroups != expectedGroups)
            yield return ("grouping", $"held {actualGroups} where the collection groups as {expectedGroups}");
        if (grouping.Any(group => group.Count == 0))
            yield return ("grouping", "held an empty group");
        if (grouping.OperationFault is not null != anyFault)
            yield return ("grouping", anyFault ? "reported no fault" : "reported a fault");
        var actualLookup = Groups(((IEnumerable<IObservableGrouping<int, BenchmarkPerson>>)lookup).Select(group => (group.Key, (IEnumerable<BenchmarkPerson>)group)));
        if (actualLookup != expectedGroups)
            yield return ("lookup", $"held {actualLookup} where the collection groups as {expectedGroups}");
        if (lookup.OperationFault is not null != anyFault)
            yield return ("lookup", anyFault ? "reported no fault" : "reported a fault");
        var expectedOrder = string.Join(" ", evaluated.OrderBy(KeyOf).ThenByDescending(person => person.Name, StringComparer.Ordinal).Select(person => $"{KeyOf(person)}{person.Name}"));
        var actualOrder = string.Join(" ", ordering.Select(person => Evaluates(person) ? $"{KeyOf(person)}{person.Name}" : "?"));
        if (actualOrder != expectedOrder)
            yield return ("ordering", $"held {actualOrder} where the collection orders as {expectedOrder}");
        else if (Identities(ordering) != Identities(evaluated))
            yield return ("ordering", "held different elements with the same keys");
        if (ordering.OperationFault is not null != anyFault)
            yield return ("ordering", anyFault ? "reported no fault" : "reported a fault");
        var expectedDictionary = new Dictionary<int, string>();
        var duplicated = false;
        foreach (var person in evaluated)
            if (!expectedDictionary.TryAdd(KeyOf(person), person.Name))
                duplicated = true;
        var expectedMapping = string.Join(" ", expectedDictionary.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}={entry.Value}"));
        var actualMapping = string.Join(" ", dictionary.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}={entry.Value}"));
        if (actualMapping != expectedMapping)
            yield return ("dictionary", $"mapped {actualMapping} where the collection maps {expectedMapping}");
        if (dictionary.OperationFault is not null != (anyFault || duplicated))
            yield return ("dictionary", anyFault || duplicated ? "reported no fault" : "reported a fault");
        var (rankFault, (ahead, tied)) = rank.Evaluation;
        if (anyFault || !evaluated.Contains(ranked))
        {
            if (rankFault is null)
                yield return ("rank", "reported no fault");
        }
        else
        {
            var rankedKey = KeyOf(ranked);
            var expectedAhead = evaluated.Count(person => KeyOf(person) < rankedKey || KeyOf(person) == rankedKey && string.CompareOrdinal(person.Name, ranked.Name) > 0);
            var expectedTied = evaluated.Count(person => KeyOf(person) == rankedKey && person.Name == ranked.Name);
            if (rankFault is not null || (ahead, tied) != (expectedAhead, expectedTied))
                yield return ("rank", $"read {(rankFault is null ? $"{ahead} ahead and {tied} tied" : rankFault.GetType().Name)} where the collection ranks {expectedAhead} ahead and {expectedTied} tied");
        }
    }

    /// <summary>
    /// Changes the collection on one thread while three others change ranks and names, all starting together, and returns once every thread has finished
    /// </summary>
    static void Churn(Random random, ObservableRangeCollection<BenchmarkPerson> source, List<BenchmarkPerson> pool, ConcurrentDictionary<string, int> faults)
    {
        var seeds = Enumerable.Range(0, 4).Select(_ => random.Next()).ToArray();
        using var start = new Barrier(4);
        var threads = new List<Thread>
        {
            new(() =>
            {
                var changes = new Random(seeds[0]);
                start.SignalAndWait();
                try
                {
                    for (var i = 0; i < changesPerThread; ++i)
                        switch (changes.Next(4))
                        {
                            case 0:
                                source.Insert(changes.Next(source.Count + 1), pool[changes.Next(poolSize)]);
                                break;
                            case 1 when source.Count > 0:
                                source.RemoveAt(changes.Next(source.Count));
                                break;
                            case 2 when source.Count > 1:
                                source.Move(changes.Next(source.Count), changes.Next(source.Count));
                                break;
                            case 3 when source.Count > 0:
                                source[changes.Next(source.Count)] = pool[changes.Next(poolSize)];
                                break;
                        }
                }
                catch (Exception ex)
                {
                    faults.AddOrUpdate(ex.GetType().Name, 1, (_, count) => count + 1);
                }
            })
        };
        for (var thread = 1; thread < 4; ++thread)
        {
            var seed = seeds[thread];
            threads.Add(new(() =>
            {
                var changes = new Random(seed);
                start.SignalAndWait();
                try
                {
                    for (var i = 0; i < changesPerThread; ++i)
                    {
                        var person = pool[changes.Next(poolSize)];
                        if (changes.Next(3) == 0)
                            person.Name = names[changes.Next(names.Length)];
                        else
                            person.Rank = changes.Next(7);
                    }
                }
                catch (Exception ex)
                {
                    faults.AddOrUpdate(ex.GetType().Name, 1, (_, count) => count + 1);
                }
            }));
        }
        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();
    }

    static string Groups(IEnumerable<(int Key, IEnumerable<BenchmarkPerson> Elements)> groups) =>
        string.Join(" ", groups.Where(group => group.Elements.Any()).OrderBy(group => group.Key).Select(group => $"{group.Key}:{Identities(group.Elements)}"));

    static string Identities(IEnumerable<BenchmarkPerson> elements) =>
        string.Join(",", elements.Select(RuntimeHelpers.GetHashCode).Order());

    static async Task<Reading> MeasureAsync(int rounds)
    {
        var mismatches = new ConcurrentDictionary<string, int>();
        var faults = new ConcurrentDictionary<string, int>();
        var leakedRounds = 0;
        string? firstMismatch = null;
        var stopwatch = Stopwatch.StartNew();
        var finished = 0;
        var lastFinished = 0;
        var stalledSince = TimeSpan.Zero;
        void Report()
        {
            var done = Volatile.Read(ref finished);
            var elapsed = stopwatch.Elapsed;
            if (done != lastFinished)
            {
                lastFinished = done;
                stalledSince = elapsed;
                var remaining = TimeSpan.FromSeconds(elapsed.TotalSeconds / done * (rounds - done));
                Console.WriteLine($"{done:N0} of {rounds:N0} rounds, {elapsed.TotalMinutes:N1} min, about {remaining.TotalMinutes:N1} min to go; mismatches {mismatches.Values.Sum():N0}, faults {faults.Values.Sum():N0}, leaked rounds {Volatile.Read(ref leakedRounds):N0}");
            }
            else if (elapsed - stalledSince >= TimeSpan.FromSeconds(60))
                Console.WriteLine($"no round has finished for {(elapsed - stalledSince).TotalSeconds:N0} s at {done:N0} of {rounds:N0}; a round may be stuck, which would itself be a finding, so take a dump of this process before ending it");
        }
        using var progress = new Timer(_ => Report(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        await Parallel.ForEachAsync(Enumerable.Range(0, rounds), new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 4) }, (round, _) =>
        {
            var random = new Random(round);
            var pool = new List<BenchmarkPerson>(poolSize);
            for (var i = 0; i < poolSize; ++i)
                pool.Add(new BenchmarkPerson(names[random.Next(names.Length)], random.Next(7)));
            var source = new ObservableRangeCollection<BenchmarkPerson>(Enumerable.Range(0, collectionSize).Select(_ => pool[random.Next(poolSize)]));
            var observer = new CollectionObserver();
            var views = new List<IDisposable>();
            try
            {
                var sourceQuery = observer.ObserveReadOnlyList(source);
                views.Add(sourceQuery);
                var grouping = sourceQuery.ObserveGroupBy(key);
                views.Add(grouping);
                var lookup = sourceQuery.ObserveToLookup(key);
                views.Add(lookup);
                var ordering = sourceQuery.ObserveOrderBy((comparableKey, false), (comparableName, true));
                views.Add(ordering);
                var dictionary = sourceQuery.ObserveToDictionary(key, name);
                views.Add(dictionary);
                var rank = sourceQuery.ObserveRank(pool[0], (comparableKey, false), (comparableName, true));
                views.Add(rank);
                Churn(random, source, pool, faults);
                foreach (var (view, detail) in Check(source, grouping, lookup, ordering, dictionary, rank, pool[0]))
                {
                    mismatches.AddOrUpdate(view, 1, (_, count) => count + 1);
                    Interlocked.CompareExchange(ref firstMismatch, $"round {round:N0}, the {view} {detail}", null);
                }
            }
            catch (Exception ex)
            {
                faults.AddOrUpdate($"{ex.GetType().Name} building or reading a view", 1, (_, count) => count + 1);
            }
            for (var i = views.Count - 1; i >= 0; --i)
                views[i].Dispose();
            if (observer.CachedObservableQueries != 0 || observer.ExpressionObserver.CachedObservableExpressions != 0)
                Interlocked.Increment(ref leakedRounds);
            Interlocked.Increment(ref finished);
            return ValueTask.CompletedTask;
        }).ConfigureAwait(false);
        return new(rounds, mismatches, faults, leakedRounds, firstMismatch, stopwatch.Elapsed);
    }

    internal static async Task RunAsync(int rounds)
    {
        var reading = await MeasureAsync(rounds).ConfigureAwait(false);
        static string Counts(IReadOnlyDictionary<string, int> counts) =>
            counts.Count == 0 ? "none" : string.Join(", ", counts.OrderBy(count => count.Key).Select(count => $"{count.Key} {count.Value:N0}"));
        var report = new StringBuilder();
        report.AppendLine("# Keyed view soak report");
        report.AppendLine();
        report.AppendLine($"Taken {DateTime.Now:yyyy-MM-dd HH:mm}, {Environment.ProcessorCount} logical processors, {Math.Max(1, Environment.ProcessorCount / 4)} rounds at a time, each over {collectionSize} elements drawn from {poolSize}, with one thread making {changesPerThread:N0} changes to the collection while three make {changesPerThread:N0} changes each to ranks and names.");
        report.AppendLine();
        report.AppendLine("`Mismatches` counts, by view, each way a view disagreed with what LINQ makes of the collection once every thread had finished: what it held, and whether it reported a fault. `Faults` counts exceptions thrown out of a change, by type. `Leaked rounds` counts rounds after which the observer still held a query or an expression cached once every view was disposed of. All three should be zero.");
        report.AppendLine();
        report.AppendLine("| Rounds | Mismatches | Faults | Leaked rounds | Elapsed |");
        report.AppendLine("|---: |--- |--- |---: |---: |");
        report.AppendLine($"| {reading.Rounds:N0} | {Counts(reading.Mismatches)} | {Counts(reading.Faults)} | {reading.LeakedRounds:N0} | {reading.Elapsed.TotalSeconds:N0} s |");
        if (reading.FirstMismatch is { } firstMismatch)
        {
            report.AppendLine();
            report.AppendLine($"First mismatch: {firstMismatch}.");
        }
        var text = report.ToString();
        Console.Write(text);
        var path = Path.Combine(AppContext.BaseDirectory, "soak-report.md");
        File.WriteAllText(path, text);
        Console.WriteLine();
        Console.WriteLine($"written to {path}");
    }
}
