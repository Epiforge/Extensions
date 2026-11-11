namespace Epiforge.Extensions.AotProbe;

/// <summary>
/// Times what an evaluation costs on each mechanism, so that one build run with and without an interpreter, or with and without dynamic code, can be compared one route at a time
/// </summary>
/// <remarks>
/// Each measure warms up for at least a second and then runs fifteen timed rounds, and reports the median, least and greatest time and the median bytes allocated per element. The raising measure changes one person observed a thousand times on direct subscription, so it is a thousand evaluations; the changing measures change each of a thousand people observed once, on direct subscription and on the graph; the unobserved measure is the floor beneath those two; the components measures invoke, construct, compare and default a thousand times through the components library, and the reflection measures invoke and construct the same members through the runtime's invokers, as the components library does without dynamic code; the member path measures read and write a thousand people's ranks through dynamic methods emitted as Blazor's member paths emit them, and through reflection as Blazor's member paths reflect them
/// </remarks>
static class ProbeTiming
{
    const int elementCount = 1000;
    const int iterations = 200;
    const int rounds = 15;

    static readonly object boxedCharacter = 'v';
    static readonly object boxedTicks = 600_000_000L;
    static readonly Expression<Func<ProbePerson, bool>> rankIsPositive = person => person.Rank > 0;
    static readonly TimeSpan warmUp = TimeSpan.FromSeconds(1);

    static void ChangeEveryRank(ProbePerson[] people)
    {
        foreach (var person in people)
            person.Rank ^= 2;
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Called only where RuntimeFeature.IsDynamicCodeSupported is true")]
    static Func<object, TResult> EmitReader<TResult>(PropertyInfo property)
    {
        var method = new DynamicMethod($"Read_{property.Name}", typeof(TResult), [typeof(object)], typeof(ProbeTiming).Module, true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, property.DeclaringType!);
        il.Emit(OpCodes.Callvirt, property.GetGetMethod(true)!);
        if (property.PropertyType.IsValueType && !typeof(TResult).IsValueType)
            il.Emit(OpCodes.Box, property.PropertyType);
        il.Emit(OpCodes.Ret);
        return (Func<object, TResult>)method.CreateDelegate(typeof(Func<object, TResult>));
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Called only where RuntimeFeature.IsDynamicCodeSupported is true")]
    static Action<object, object?> EmitWriter(PropertyInfo property)
    {
        var method = new DynamicMethod($"Write_{property.Name}", typeof(void), [typeof(object), typeof(object)], typeof(ProbeTiming).Module, true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, property.DeclaringType!);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(property.PropertyType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, property.PropertyType);
        il.Emit(OpCodes.Callvirt, property.GetSetMethod(true)!);
        il.Emit(OpCodes.Ret);
        return (Action<object, object?>)method.CreateDelegate(typeof(Action<object, object?>));
    }

    static string Measure(string name, Action operation)
    {
        var warmingStarted = Stopwatch.GetTimestamp();
        do
        {
            for (var i = 0; i < iterations; ++i)
                operation();
        }
        while (Stopwatch.GetElapsedTime(warmingStarted) < warmUp);
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
        return string.Create(CultureInfo.InvariantCulture, $"timing {name}: {nanoseconds[rounds / 2]:F1} ns (min {nanoseconds[0]:F1}, max {nanoseconds[rounds - 1]:F1}), {bytes[rounds / 2]:F1} B per element");
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
        var rankGetter = typeof(ProbePerson).GetProperty(nameof(ProbePerson.Rank))!.GetMethod!;
        var invoker = rankGetter.GetFastInvoker();
        var invoked = People();
        lines.Add(Measure("components invoke", () =>
        {
            foreach (var person in invoked)
                invoker.Invoke(person);
        }));
        var indexOf = typeof(string).GetMethod(nameof(string.IndexOf), [typeof(char)])!.GetFastInvoker();
        lines.Add(Measure("components invoke with an argument", () =>
        {
            for (var i = 0; i < elementCount; ++i)
                indexOf.Invoke("evaluated", boxedCharacter);
        }));
        var constructor = typeof(TimeSpan).GetConstructor([typeof(long)])!.GetFastInvoker();
        lines.Add(Measure("components construct", () =>
        {
            for (var i = 0; i < elementCount; ++i)
                constructor.Invoke(null, boxedTicks);
        }));
        var getterInvoker = MethodInvoker.Create(rankGetter);
        lines.Add(Measure("reflection invoke", () =>
        {
            foreach (var person in invoked)
                getterInvoker.Invoke(person);
        }));
        var indexOfInvoker = MethodInvoker.Create(typeof(string).GetMethod(nameof(string.IndexOf), [typeof(char)])!);
        lines.Add(Measure("reflection invoke with an argument", () =>
        {
            for (var i = 0; i < elementCount; ++i)
                indexOfInvoker.Invoke("evaluated", boxedCharacter);
        }));
        var constructorInvoker = ConstructorInvoker.Create(typeof(TimeSpan).GetConstructor([typeof(long)])!);
        lines.Add(Measure("reflection construct", () =>
        {
            for (var i = 0; i < elementCount; ++i)
                constructorInvoker.Invoke(boxedTicks);
        }));
        var leftNumbers = Enumerable.Range(0, elementCount).Select(i => (object)i).ToArray();
        var rightNumbers = Enumerable.Range(0, elementCount).Select(i => (object)i).ToArray();
        var leftNames = Enumerable.Range(0, elementCount).Select(i => (object)$"P{i}").ToArray();
        var rightNames = Enumerable.Range(0, elementCount).Select(i => (object)$"P{i}").ToArray();
        var numberEquality = FastEqualityComparer.Get(typeof(int));
        lines.Add(Measure("components equality int", () =>
        {
            for (var i = 0; i < elementCount; ++i)
                numberEquality.Equals(leftNumbers[i], rightNumbers[i]);
        }));
        lines.Add(Measure("components hash int", () =>
        {
            for (var i = 0; i < elementCount; ++i)
                numberEquality.GetHashCode(leftNumbers[i]);
        }));
        var nameEquality = FastEqualityComparer.Get(typeof(string));
        lines.Add(Measure("components equality string", () =>
        {
            for (var i = 0; i < elementCount; ++i)
                nameEquality.Equals(leftNames[i], rightNames[i]);
        }));
        var numberComparer = FastComparer.Get(typeof(int));
        lines.Add(Measure("components comparison int", () =>
        {
            for (var i = 0; i < elementCount; ++i)
                numberComparer.Compare(leftNumbers[i], rightNumbers[i]);
        }));
        lines.Add(Measure("components default", () =>
        {
            for (var i = 0; i < elementCount; ++i)
                typeof(TimeSpan).FastDefault();
        }));
        var rank = typeof(ProbePerson).GetProperty(nameof(ProbePerson.Rank))!;
        var ranks = invoked.Select(person => (object)person.Rank).ToArray();
        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            var read = EmitReader<object?>(rank);
            lines.Add(Measure("member path read emitted", () =>
            {
                foreach (var person in invoked)
                    read(person);
            }));
            var readTyped = EmitReader<int>(rank);
            lines.Add(Measure("member path typed read emitted", () =>
            {
                foreach (var person in invoked)
                    readTyped(person);
            }));
            var write = EmitWriter(rank);
            lines.Add(Measure("member path write emitted", () =>
            {
                for (var i = 0; i < elementCount; ++i)
                    write(invoked[i], ranks[i]);
            }));
        }
        else
        {
            lines.Add("timing member path read emitted: unsupported");
            lines.Add("timing member path typed read emitted: unsupported");
            lines.Add("timing member path write emitted: unsupported");
        }
        lines.Add(Measure("member path read reflected", () =>
        {
            foreach (var person in invoked)
                rank.GetValue(person, BindingFlags.DoNotWrapExceptions, null, null, null);
        }));
        lines.Add(Measure("member path typed read reflected", () =>
        {
            foreach (var person in invoked)
                _ = (int)rank.GetValue(person, BindingFlags.DoNotWrapExceptions, null, null, null)!;
        }));
        lines.Add(Measure("member path write reflected", () =>
        {
            for (var i = 0; i < elementCount; ++i)
                rank.SetValue(invoked[i], ranks[i], BindingFlags.DoNotWrapExceptions, null, null, null);
        }));
    }
}
