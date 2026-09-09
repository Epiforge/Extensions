namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Decomposes the linear cost of taking an element out of a grouped view into the search which finds it and the shift which closes the gap behind it
/// </summary>
/// <remarks>
/// A grouped query moves an element between groups by calling <c>Remove</c> on the collection backing its old group, which searches for it and then shifts everything after it down. That is why the cost of a migration grows with the size of the group, and it is the whole of this library's disadvantage against DynamicData above about eight thousand elements. <b>Whether it is worth removing depends entirely on which half is the expensive one</b>, because only the search can be avoided: backing the group with a structure that yields an element's position without looking for it would still have to shift the array that consumers read
/// </remarks>
/// <remarks>
/// The three arms differ only in what they avoid, and each pair of them subtracts to one answer:
/// <list type="bullet">
/// <item><description><c>SearchThenShift</c> less <c>ShiftOnly</c> — what the search costs, which is what a positional structure would save</description></item>
/// <item><description><c>ShiftOnly</c> less <c>NeitherSearchNorShift</c> — what the shift costs, which no change to this library can avoid while the group is an array</description></item>
/// </list>
/// </remarks>
/// <remarks>
/// Every arm restores what it removed, so the collection is the same size at the end of an operation as at the start and the measurement does not drift. The insertion is common to all three and cancels out of both subtractions. <b>The collection is the one the grouped query actually uses rather than a bare list</b>, so the notification each operation raises is included — and, being common to all three arms, cancels as well
/// </remarks>
[MemoryDiagnoser]
public class GroupRemovalCostBenchmarks
{
    ObservableRangeCollection<BenchmarkPerson> group = null!;
    int middle;
    BenchmarkPerson middleElement = null!;

    /// <summary>
    /// How many elements the group holds, which for a collection grouped sixteen ways is a sixteenth of the collection
    /// </summary>
    [Params(64, 256, 1_024, 4_096)]
    public int GroupSize { get; set; }

    /// <summary>
    /// Removes the last element and puts it back, which neither searches nor shifts and is the floor the other two stand on
    /// </summary>
    [Benchmark(Baseline = true)]
    public void NeitherSearchNorShift()
    {
        var last = group[group.Count - 1];
        group.RemoveAt(group.Count - 1);
        group.Add(last);
    }

    /// <summary>
    /// Removes the middle element by position and puts it back, which shifts but does not search
    /// </summary>
    [Benchmark]
    public void ShiftOnly()
    {
        group.RemoveAt(middle);
        group.Insert(middle, middleElement);
    }

    /// <summary>
    /// Removes the middle element by value and puts it back, which is what a grouped migration does today
    /// </summary>
    [Benchmark]
    public void SearchThenShift()
    {
        group.Remove(middleElement);
        group.Insert(middle, middleElement);
    }

    [GlobalSetup]
    public void Setup()
    {
        group = [];
        for (var i = 0; i < GroupSize; ++i)
            group.Add(new BenchmarkPerson($"P{i}", i));
        middle = GroupSize / 2;
        middleElement = group[middle];
    }
}
