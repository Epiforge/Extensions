namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class DictionaryMutationBenchmarks
{
    const int elementCount = 10000;

    ObservableConcurrentDictionary<int, int> concurrent = null!;
    ObservableDictionary<int, int> hashed = null!;
    ObservableSortedDictionary<int, int> sorted = null!;

    [Benchmark]
    public void AddToConcurrent()
    {
        for (var i = elementCount; i < elementCount * 2; ++i)
            concurrent.TryAdd(i, i);
    }

    [Benchmark(Baseline = true)]
    public void AddToHashed()
    {
        for (var i = elementCount; i < elementCount * 2; ++i)
            hashed.Add(i, i);
    }

    [Benchmark]
    public void AddToSorted()
    {
        for (var i = elementCount; i < elementCount * 2; ++i)
            sorted.Add(i, i);
    }

    [IterationSetup(Targets = [nameof(AddToConcurrent), nameof(RemoveFromConcurrent), nameof(SetOnConcurrent)])]
    public void FillConcurrent()
    {
        concurrent = new ObservableConcurrentDictionary<int, int>();
        for (var i = 0; i < elementCount; ++i)
            concurrent.TryAdd(i, i);
    }

    [IterationSetup(Targets = [nameof(AddToHashed), nameof(RemoveFromHashed), nameof(SetOnHashed)])]
    public void FillHashed()
    {
        hashed = new ObservableDictionary<int, int>();
        for (var i = 0; i < elementCount; ++i)
            hashed.Add(i, i);
    }

    [IterationSetup(Targets = [nameof(AddToSorted), nameof(RemoveFromSorted), nameof(SetOnSorted)])]
    public void FillSorted()
    {
        sorted = new ObservableSortedDictionary<int, int>();
        for (var i = 0; i < elementCount; ++i)
            sorted.Add(i, i);
    }

    [Benchmark]
    public void RemoveFromConcurrent()
    {
        for (var i = 0; i < elementCount; ++i)
            concurrent.TryRemove(i, out _);
    }

    [Benchmark]
    public void RemoveFromHashed()
    {
        for (var i = 0; i < elementCount; ++i)
            hashed.Remove(i);
    }

    [Benchmark]
    public void RemoveFromSorted()
    {
        for (var i = 0; i < elementCount; ++i)
            sorted.Remove(i);
    }

    [Benchmark]
    public void SetOnConcurrent()
    {
        for (var i = 0; i < elementCount; ++i)
            concurrent[i] = i + 1;
    }

    [Benchmark]
    public void SetOnHashed()
    {
        for (var i = 0; i < elementCount; ++i)
            hashed[i] = i + 1;
    }

    [Benchmark]
    public void SetOnSorted()
    {
        for (var i = 0; i < elementCount; ++i)
            sorted[i] = i + 1;
    }
}
