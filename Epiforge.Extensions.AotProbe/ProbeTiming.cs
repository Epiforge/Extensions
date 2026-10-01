namespace Epiforge.Extensions.AotProbe;

/// <summary>
/// Times what an evaluation costs on each mechanism, so that one build run with and without an interpreter, or with and without dynamic code, can be compared one route at a time
/// </summary>
/// <remarks>
/// Each measure runs one warm-up round and then seven timed rounds, and reports the medians of the time and of the bytes allocated per element. The raising measure changes one person observed a thousand times on direct subscription, so it is a thousand evaluations; the changing measures change each of a thousand people observed once, on direct subscription and on the graph; the unobserved measure is the floor beneath those two
/// </remarks>
static class ProbeTiming
{
    const int elementCount = 1000;
    const int iterations = 200;
    const int rounds = 7;

    static readonly Expression<Func<ProbePerson, bool>> rankIsPositive = person => person.Rank > 0;

    static void ChangeEveryRank(ProbePerson[] people)
    {
        foreach (var person in people)
            person.Rank ^= 2;
    }

    static string Measure(string name, Action operation)
    {
        for (var i = 0; i < iterations; ++i)
            operation();
        var nanoseconds = new double[rounds];
        var bytes = new double[rounds];
        for (var round = 0; round < rounds; ++round)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            for (var i = 0; i < iterations; ++i)
                operation();
            var elapsed = Stopwatch.GetTimestamp() - started;
            bytes[round] = (double)(GC.GetAllocatedBytesForCurrentThread() - allocated) / (iterations * elementCount);
            nanoseconds[round] = elapsed * 1_000_000_000.0 / Stopwatch.Frequency / (iterations * elementCount);
        }
        Array.Sort(nanoseconds);
        Array.Sort(bytes);
        return string.Create(CultureInfo.InvariantCulture, $"timing {name}: {nanoseconds[rounds / 2]:F1} ns, {bytes[rounds / 2]:F1} B per element");
    }

    static ProbePerson[] People() =>
        [.. Enumerable.Range(1, elementCount).Select(i => new ProbePerson { Name = $"P{i}", Rank = i })];

    /// <summary>
    /// Adds a line for each measure to the report
    /// </summary>
    internal static void Run(List<string> lines)
    {
        var raised = new ProbePerson { Name = "Raised", Rank = 1 };
        var raisingObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true });
        var raising = Enumerable.Range(0, elementCount).Select(_ => raisingObserver.Observe(rankIsPositive, raised)).ToList();
        lines.Add(Measure("direct raise", () => raised.Rank ^= 2));
        foreach (var observation in raising)
            observation.Dispose();
        var unobserved = People();
        lines.Add(Measure("unobserved change", () => ChangeEveryRank(unobserved)));
        foreach (var direct in new[] { true, false })
        {
            var people = People();
            var observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = direct });
            var observations = people.Select(person => observer.Observe(rankIsPositive, person)).ToList();
            lines.Add(Measure(direct ? "direct change" : "graph change", () => ChangeEveryRank(people)));
            foreach (var observation in observations)
                observation.Dispose();
        }
    }
}
