namespace Epiforge.Extensions.AotProbe;

/// <summary>
/// Exercises observed expressions and queries on both mechanisms and reports, one line per check, whether each followed a change as expected, so that a runtime without dynamic code can be compared with one which has it
/// </summary>
public static class Probe
{
    static void Check(List<string> lines, string name, Func<string?> check)
    {
        try
        {
            var failure = check();
            lines.Add(failure is null ? $"PASS {name}" : $"FAIL {name}: {failure}");
        }
        catch (Exception ex)
        {
            lines.Add($"FAIL {name}: {Describe(ex)}");
        }
    }

    static void CheckExpressions(List<string> lines, bool direct)
    {
        var mechanism = direct ? "direct" : "graph";
        ExpressionObserver Observer() =>
            new(new ExpressionObserverOptions { UseDirectSubscription = direct });
        Check(lines, $"expression {mechanism} comparison", () => Follows(Observer(), direct, person => person.Rank > 1, person => person.Rank = 2, false, true));
        Check(lines, $"expression {mechanism} arithmetic", () => Follows(Observer(), direct, person => person.Rank * 2 + person.Score, person => person.Score = 10, 3, 12));
        Check(lines, $"expression {mechanism} chain", () => Follows(Observer(), direct, person => person.Name.Length, person => person.Name = "Beatrice", 3, 8));
        Check(lines, $"expression {mechanism} short circuit", () => Follows(Observer(), direct, person => person.Manager != null && person.Manager.Rank > 0, person => person.Manager = new ProbePerson { Rank = 1 }, false, true));
        Check(lines, $"expression {mechanism} static call", () => Follows(Observer(), direct, person => Math.Max(person.Rank, person.Score), person => person.Score = 7, 1, 7));
        Check(lines, $"expression {mechanism} value type property", () => Follows(Observer(), direct, person => person.Span.TotalMinutes, person => person.Span = TimeSpan.FromMinutes(90), 0.0, 90.0));
        Check(lines, $"expression {mechanism} fault and recovery", () =>
        {
            var observer = Observer();
            var person = new ProbePerson { Rank = 6 };
            using var observation = observer.Observe((ProbePerson p) => p.Rank / p.Score, person);
            if (observation.Evaluation.Fault is not DivideByZeroException)
                return $"expected DivideByZeroException, observed {Describe(observation.Evaluation)}";
            person.Score = 2;
            if (observation.Evaluation.Fault is not null || observation.Evaluation.Result != 3)
                return $"expected 3 after recovery, observed {Describe(observation.Evaluation)}";
            return Mechanism(observer, direct);
        });
    }

    static void CheckQueries(List<string> lines, bool direct)
    {
        var mechanism = direct ? "direct" : "graph";
        CollectionObserver Observer() =>
            new(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = direct }));
        static ObservableRangeCollection<ProbePerson> People() =>
            new(Enumerable.Range(1, 5).Select(i => new ProbePerson { Name = $"P{i}", Rank = i, Score = i * 10 }));
        Check(lines, $"query {mechanism} where", () =>
        {
            var people = People();
            using var observation = Observer().ObserveReadOnlyList(people);
            using var where = observation.ObserveWhere(person => person.Rank > 2);
            var before = where.Count;
            people[0].Rank = 9;
            return Expect(before, 3, where.Count, 4);
        });
        Check(lines, $"query {mechanism} order by", () =>
        {
            var people = People();
            using var observation = Observer().ObserveReadOnlyList(people);
            using var ordered = observation.ObserveOrderBy(person => person.Rank);
            var before = string.Join(",", ordered.Select(person => person.Name));
            people[0].Rank = 9;
            return Expect(before, "P1,P2,P3,P4,P5", string.Join(",", ordered.Select(person => person.Name)), "P2,P3,P4,P5,P1");
        });
        Check(lines, $"query {mechanism} group by", () =>
        {
            var people = People();
            using var observation = Observer().ObserveReadOnlyList(people);
            using var groups = observation.ObserveGroupBy(person => person.Rank % 2);
            static string Shape(IObservableCollectionQuery<IObservableGrouping<int, ProbePerson>> groups) =>
                string.Join(",", groups.OrderBy(group => group.Key).Select(group => $"{group.Key}:{group.Count}"));
            var before = Shape(groups);
            people[0].Rank = 2;
            return Expect(before, "0:2,1:3", Shape(groups), "0:3,1:2");
        });
        Check(lines, $"query {mechanism} sum", () =>
        {
            var people = People();
            using var observation = Observer().ObserveReadOnlyList(people);
            using var ranks = observation.ObserveSelect(person => person.Rank);
            using var sum = ranks.ObserveSum();
            var before = Describe(sum.Evaluation);
            people[0].Rank = 11;
            return Expect(before, "15", Describe(sum.Evaluation), "25");
        });
        Check(lines, $"query {mechanism} select", () =>
        {
            var people = People();
            using var observation = Observer().ObserveReadOnlyList(people);
            using var lengths = observation.ObserveSelect(person => person.Name.Length);
            var before = string.Join(",", lengths);
            people[0].Name = "Quinn";
            return Expect(before, "2,2,2,2,2", string.Join(",", lengths), "5,2,2,2,2");
        });
        Check(lines, $"query {mechanism} max", () =>
        {
            var people = People();
            using var observation = Observer().ObserveReadOnlyList(people);
            using var scores = observation.ObserveSelect(person => person.Score);
            using var max = scores.ObserveMax();
            var before = Describe(max.Evaluation);
            people[0].Score = 99;
            return Expect(before, "50", Describe(max.Evaluation), "99");
        });
    }

    static string Describe(Exception ex)
    {
        var innermost = ex;
        while (innermost.InnerException is { } inner)
            innermost = inner;
        var frames = (innermost.StackTrace ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        frames = [.. frames.Take(1), .. frames.Skip(1).Where(frame => frame.Contains("Epiforge.Extensions.", StringComparison.Ordinal) && !frame.Contains(".AotProbe.", StringComparison.Ordinal)).Take(3)];
        return $"{innermost.GetType().FullName}: {innermost.Message}{(innermost == ex ? string.Empty : $" (thrown as {ex.GetType().FullName})")} | {string.Join(" | ", frames)}";
    }

    static string Describe<TResult>((Exception? Fault, TResult Result) evaluation) =>
        evaluation.Fault is { } fault ? $"fault {fault.GetType().FullName}: {fault.Message}" : Convert.ToString(evaluation.Result, CultureInfo.InvariantCulture) ?? "null";

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The probe reports whether creating a dynamic method throws")]
    static string DynamicMethodOutcome()
    {
        try
        {
            var dynamicMethod = new DynamicMethod("FortyTwo", typeof(int), Type.EmptyTypes);
            var il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldc_I4, 42);
            il.Emit(OpCodes.Ret);
            return ((Func<int>)dynamicMethod.CreateDelegate(typeof(Func<int>)))() == 42 ? "works" : "returned the wrong value";
        }
        catch (Exception ex)
        {
            return $"throws {ex.GetType().FullName}: {ex.Message}";
        }
    }

    static string? Expect<T>(T before, T expectedBefore, T after, T expectedAfter) =>
        EqualityComparer<T>.Default.Equals(before, expectedBefore) && EqualityComparer<T>.Default.Equals(after, expectedAfter) ? null : $"expected {expectedBefore} then {expectedAfter}, observed {before} then {after}";

    static string? Follows<TResult>(ExpressionObserver observer, bool direct, Expression<Func<ProbePerson, TResult>> expression, Action<ProbePerson> change, TResult expectedBefore, TResult expectedAfter)
    {
        var person = new ProbePerson { Name = "Ann", Rank = 1, Score = 1 };
        using var observation = observer.Observe(expression, person);
        var before = Describe(observation.Evaluation);
        change(person);
        return Expect(before, Describe(((Exception?)null, expectedBefore)), Describe(observation.Evaluation), Describe(((Exception?)null, expectedAfter))) ?? Mechanism(observer, direct);
    }

    static string? Mechanism(ExpressionObserver observer, bool direct) =>
        direct && observer.CachedObservableExpressions != 0 ? $"the fast path was not used ({observer.CachedObservableExpressions} graph nodes cached)" : null;

    /// <summary>
    /// Runs every check and every timing and returns the report, beginning with what the runtime says about dynamic code
    /// </summary>
    public static IReadOnlyList<string> Run(string host)
    {
        var lines = new List<string>
        {
            $"host {host}",
            $"framework {RuntimeInformation.FrameworkDescription}",
            $"runtime identifier {RuntimeInformation.RuntimeIdentifier}",
            $"os {RuntimeInformation.OSDescription}",
            $"IsDynamicCodeSupported {RuntimeFeature.IsDynamicCodeSupported}",
            $"IsDynamicCodeCompiled {RuntimeFeature.IsDynamicCodeCompiled}",
            $"DynamicMethod {DynamicMethodOutcome()}"
        };
        foreach (var direct in new[] { true, false })
        {
            CheckExpressions(lines, direct);
            CheckQueries(lines, direct);
        }
        try
        {
            ProbeTiming.Run(lines);
        }
        catch (Exception ex)
        {
            lines.Add($"timing failed: {Describe(ex)}");
        }
        var failed = lines.Count(line => line.StartsWith("FAIL ", StringComparison.Ordinal));
        var passed = lines.Count(line => line.StartsWith("PASS ", StringComparison.Ordinal));
        lines.Add($"summary {passed} passed, {failed} failed");
        return lines;
    }
}
