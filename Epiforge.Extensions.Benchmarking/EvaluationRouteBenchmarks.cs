namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Splits one evaluation of a predicate over a property by the route that reaches the property, with dynamic code, with it turned off on the JIT, and under Native AOT, to find what an evaluation without dynamic code pays for
/// </summary>
/// <remarks>
/// Each evaluating arm computes <c>person.Rank &gt; 0</c> once. The hand-written delegate is the floor. The interpreted arm is the lambda compiled with interpretation preferred, which is how direct subscription evaluated without dynamic code before it evaluated through closures. The invoker, value and unwrapped invocation arms read the property through <see cref="MethodInvoker"/>, <see cref="PropertyInfo.GetValue(object?)"/> and <see cref="MethodBase.Invoke(object?, BindingFlags, Binder?, object?[], CultureInfo?)"/> with <see cref="BindingFlags.DoNotWrapExceptions"/>, and the two <c>IndexOf</c> arms call a method of one argument through the first and the last, which are the routes a reflected call can take. The raising arms change the person under one observation on direct subscription and under none, so their difference is one evaluation as the library does it in each job
/// </remarks>
[MemoryDiagnoser]
[NativeAot]
[WithoutDynamicCode]
public class EvaluationRouteBenchmarks
{
    const string text = "evaluated";

    static readonly MethodInfo indexOf = typeof(string).GetMethod(nameof(string.IndexOf), [typeof(char)])!;
    static readonly object[] indexOfArguments = ['v'];
    static readonly Expression<Func<BenchmarkPerson, bool>> rankIsPositive = person => person.Rank > 0;
    static readonly MethodInfo rankGetter = typeof(BenchmarkPerson).GetProperty(nameof(BenchmarkPerson.Rank))!.GetMethod!;
    static readonly PropertyInfo rank = typeof(BenchmarkPerson).GetProperty(nameof(BenchmarkPerson.Rank))!;

    readonly BenchmarkPerson person = new("evaluated", 1);
    MethodInvoker getter = null!;
    Func<BenchmarkPerson, bool> handWritten = null!;
    MethodInvoker indexOfInvoker = null!;
    Func<BenchmarkPerson, bool> interpreted = null!;
    IDisposable? observation;

    [GlobalCleanup]
    public void Cleanup()
    {
        observation?.Dispose();
        observation = null;
    }

    [Benchmark(Baseline = true)]
    public bool HandWritten() =>
        handWritten(person);

    [Benchmark]
    public object? IndexOfThroughInvoke() =>
        indexOf.Invoke(text, BindingFlags.DoNotWrapExceptions, null, indexOfArguments, null);

    [Benchmark]
    public object? IndexOfThroughMethodInvoker() =>
        indexOfInvoker.Invoke(text, indexOfArguments[0]);

    [Benchmark]
    public bool Interpreted() =>
        interpreted(person);

    [Benchmark]
    public void RaiseUnderAnObservation() =>
        person.Rank ^= 2;

    [Benchmark]
    public void RaiseUnobserved() =>
        person.Rank ^= 2;

    [GlobalSetup]
    public void Setup()
    {
        getter = MethodInvoker.Create(rankGetter);
        handWritten = person => person.Rank > 0;
        indexOfInvoker = MethodInvoker.Create(indexOf);
        interpreted = rankIsPositive.Compile(preferInterpretation: true);
        if (!handWritten(person) || !interpreted(person) || (int)getter.Invoke(person)! != person.Rank || (int)rank.GetValue(person)! != person.Rank || (int)rankGetter.Invoke(person, BindingFlags.DoNotWrapExceptions, null, null, null)! != person.Rank || (int)indexOfInvoker.Invoke(text, indexOfArguments[0])! != 1 || (int)indexOf.Invoke(text, BindingFlags.DoNotWrapExceptions, null, indexOfArguments, null)! != 1)
            throw new InvalidOperationException("the evaluating arms do not read the rank");
    }

    [GlobalSetup(Target = nameof(RaiseUnderAnObservation))]
    public void SetupObservation()
    {
        Setup();
        var expressionObserver = new ExpressionObserver();
        var observed = expressionObserver.Observe(rankIsPositive, person);
        observation = observed;
        if (expressionObserver.CachedObservableExpressions != 0)
            throw new InvalidOperationException("the raising arm's observation did not take the fast path");
        person.Rank = -1;
        if (observed.Evaluation.Result)
            throw new InvalidOperationException("the raising arm's observation does not follow the rank");
        person.Rank = 1;
    }

    [Benchmark]
    public bool ThroughGetValue() =>
        (int)rank.GetValue(person)! > 0;

    [Benchmark]
    public bool ThroughInvokeWithoutWrapping() =>
        (int)rankGetter.Invoke(person, BindingFlags.DoNotWrapExceptions, null, null, null)! > 0;

    [Benchmark]
    public bool ThroughMethodInvoker() =>
        (int)getter.Invoke(person)! > 0;
}
