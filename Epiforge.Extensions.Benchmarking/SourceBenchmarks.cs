namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class SourceBenchmarks
{
    BenchmarkPerson added = null!;
    int cursor;
    ObservableRangeCollection<BenchmarkPerson> source = null!;

    [Params(100, 1000, 10000)]
    public int ElementCount { get; set; }

    [Benchmark]
    public void AddAndRemove()
    {
        source.Add(added);
        source.RemoveAt(source.Count - 1);
    }

    [GlobalSetup]
    public void Setup()
    {
        source = BenchmarkPerson.CreateCollection(ElementCount);
        added = new BenchmarkPerson("Added", 0);
    }

    [Benchmark]
    public void ValueChange()
    {
        var person = source[cursor];
        cursor = cursor + 1 == source.Count ? 0 : cursor + 1;
        person.Rank ^= 2;
    }
}
