namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class ResultBoxingBenchmarks
{
    const int observationCount = 1000;

    static readonly object markerA = new();
    static readonly object markerB = new();

    BenchmarkPerson[] arguments = null!;
    ExpressionObserver observer = null!;
    IObservableExpression<BenchmarkPerson, object>[] referenceObservations = null!;
    BenchmarkSignal referenceSignal = null!;
    IObservableExpression<BenchmarkPerson, int>[] touchObservations = null!;
    BenchmarkSignal touchSignal = null!;
    IObservableExpression<BenchmarkPerson, int>[] valueObservations = null!;
    BenchmarkSignal valueSignal = null!;

    [Benchmark]
    public void ChangeReferenceTypedResult() =>
        referenceSignal.Marker = ReferenceEquals(referenceSignal.Marker, markerA) ? markerB : markerA;

    [Benchmark(Baseline = true)]
    public void ChangeValueTypedResult() =>
        valueSignal.Rank ^= 1;

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = 0; i < observationCount; ++i)
        {
            referenceObservations[i].Dispose();
            touchObservations[i].Dispose();
            valueObservations[i].Dispose();
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true });
        arguments = new BenchmarkPerson[observationCount];
        for (var i = 0; i < observationCount; ++i)
            arguments[i] = new BenchmarkPerson($"P{i}", i);
        referenceSignal = new BenchmarkSignal();
        touchSignal = new BenchmarkSignal();
        valueSignal = new BenchmarkSignal();
        Expression<Func<BenchmarkPerson, object>> referenceSelector = person => referenceSignal.Marker;
        Expression<Func<BenchmarkPerson, int>> touchSelector = person => touchSignal.Rank;
        Expression<Func<BenchmarkPerson, int>> valueSelector = person => valueSignal.Rank;
        referenceObservations = new IObservableExpression<BenchmarkPerson, object>[observationCount];
        touchObservations = new IObservableExpression<BenchmarkPerson, int>[observationCount];
        valueObservations = new IObservableExpression<BenchmarkPerson, int>[observationCount];
        for (var i = 0; i < observationCount; ++i)
        {
            referenceObservations[i] = observer.Observe(referenceSelector, arguments[i]);
            touchObservations[i] = observer.Observe(touchSelector, arguments[i]);
            valueObservations[i] = observer.Observe(valueSelector, arguments[i]);
        }
    }

    [Benchmark]
    public void TouchWithoutChangingValueTypedResult() =>
        touchSignal.TouchRank();
}
