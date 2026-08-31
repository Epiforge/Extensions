namespace Epiforge.Extensions.Benchmarking;

using Epiforge.Extensions.Collections.Generic;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class PrefixWeightedSequenceBenchmarks
{
    int middleIndex;
    PrefixWeightedSequenceNode<int> middleNode = null!;
    PrefixWeightedSequence<int> sequence = null!;
    bool weighted;

    [Params(100, 1000, 10000)]
    public int ItemCount { get; set; }

    [Benchmark]
    public void InsertThenRemoveAtMiddle()
    {
        sequence.Insert(middleIndex, 0, 1);
        sequence.RemoveAt(middleIndex);
    }

    [Benchmark(Baseline = true)]
    public int PrefixWeightBeforeMiddle() =>
        sequence.PrefixWeightBefore(middleIndex);

    [Benchmark]
    public void SetWeightAtMiddle()
    {
        weighted = !weighted;
        sequence.SetWeight(middleNode, weighted ? 1 : 0);
    }

    [GlobalSetup]
    public void Setup()
    {
        sequence = new PrefixWeightedSequence<int>();
        for (var i = 0; i < ItemCount; ++i)
            sequence.Insert(i, i, 1);
        middleIndex = ItemCount / 2;
        middleNode = sequence.NodeAt(middleIndex);
    }
}
