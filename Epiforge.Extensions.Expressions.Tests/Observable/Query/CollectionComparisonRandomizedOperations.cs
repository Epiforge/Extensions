namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Drives the maximum and minimum across every kind of change over values of which some are equal and some are distinct but compare as equal, and requires the result to be one the collection holds and to compare as the extreme
/// </summary>
/// <remarks>
/// The query keeps each value the comparer distinguishes once, with its occurrences. Values the comparer cannot tell apart share an entry which reports one of them, and when that one leaves the entry must report another which is still present rather than keep reporting the one which left. A case-insensitive comparer over upper and lower case letters makes that happen on nearly every step
/// </remarks>
[TestClass]
public class CollectionComparisonRandomizedOperations
{
    static readonly string[] letters = ["a", "A", "b", "B", "c", "C", "d", "D"];

    static string Mutate(Random random, ObservableRangeCollection<string> source)
    {
        var count = source.Count;
        var choice = random.Next(100);
        if (count == 0 || choice < 30)
        {
            var index = random.Next(count + 1);
            var added = new List<string>();
            for (int i = 0, ii = random.Next(1, 3); i < ii; ++i)
                added.Add(letters[random.Next(letters.Length)]);
            source.InsertRange(index, added);
            return $"insert {string.Join(string.Empty, added)} at {index}";
        }
        if (choice < 60)
        {
            var removeCount = random.Next(1, Math.Min(2, count) + 1);
            var index = random.Next(count - removeCount + 1);
            source.RemoveRange(index, removeCount);
            return $"remove {removeCount} at {index}";
        }
        if (choice < 80)
        {
            var index = random.Next(count);
            var replacement = letters[random.Next(letters.Length)];
            source[index] = replacement;
            return $"replace at {index} with {replacement}";
        }
        if (choice < 95)
        {
            var oldIndex = random.Next(count);
            var newIndex = random.Next(count);
            source.Move(oldIndex, newIndex);
            return $"move from {oldIndex} to {newIndex}";
        }
        var reset = new List<string>();
        for (int i = 0, ii = random.Next(0, 5); i < ii; ++i)
            reset.Add(letters[random.Next(letters.Length)]);
        source.Reset(reset);
        return $"reset to {string.Join(string.Empty, reset)}";
    }

    static void AssertExtreme(ObservableRangeCollection<string> source, IObservableScalarQuery<string> query, IComparer<string> comparer, int sought, string context)
    {
        if (source.Count == 0)
        {
            Assert.IsNotNull(query.Evaluation.Fault, $"{context}: the extreme of nothing did not fault");
            return;
        }
        Assert.IsNull(query.Evaluation.Fault, $"{context}: faulted");
        var result = query.Evaluation.Result;
        CollectionAssert.Contains(source, result, $"{context}: reported {result}, which the collection does not hold");
        var extreme = sought > 0 ? source.Max(comparer) : source.Min(comparer);
        Assert.AreEqual(0, comparer.Compare(extreme, result), $"{context}: reported {result} where the extreme is {extreme}");
    }

    [TestMethod]
    [Timeout(300000)]
    public void TheExtremeIsAlwaysAValueTheCollectionHolds()
    {
        for (var seed = 0; seed < 20; ++seed)
        {
            var random = new Random(seed);
            var source = new ObservableRangeCollection<string>(["b", "B", "a"]);
            var collectionObserver = CollectionObserverHelpers.Create();
            using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
            using (var caselessMax = sourceQuery.ObserveMax(StringComparer.OrdinalIgnoreCase))
            using (var caselessMin = sourceQuery.ObserveMin(StringComparer.OrdinalIgnoreCase))
            using (var ordinalMax = sourceQuery.ObserveMax(StringComparer.Ordinal))
            using (var ordinalMin = sourceQuery.ObserveMin(StringComparer.Ordinal))
                for (var step = 0; step < 200; ++step)
                {
                    var operation = Mutate(random, source);
                    var context = $"seed {seed}, step {step}, after {operation}, holding {string.Join(string.Empty, source)}";
                    AssertExtreme(source, caselessMax, StringComparer.OrdinalIgnoreCase, 1, $"{context}, caseless maximum");
                    AssertExtreme(source, caselessMin, StringComparer.OrdinalIgnoreCase, -1, $"{context}, caseless minimum");
                    AssertExtreme(source, ordinalMax, StringComparer.Ordinal, 1, $"{context}, ordinal maximum");
                    AssertExtreme(source, ordinalMin, StringComparer.Ordinal, -1, $"{context}, ordinal minimum");
                }
            Assert.AreEqual(0, collectionObserver.CachedObservableQueries, $"seed {seed}: a query was left cached");
        }
    }

    /// <summary>
    /// The extreme leaves while an indistinguishable value remains, which must be reported in its place
    /// </summary>
    [TestMethod]
    public void AnIndistinguishableValueIsPromotedWhenTheOneReportedLeaves()
    {
        var source = new ObservableRangeCollection<string>(["a", "B", "b"]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        using (var max = sourceQuery.ObserveMax(StringComparer.OrdinalIgnoreCase))
        {
            var reported = max.Evaluation.Result;
            Assert.AreEqual(0, StringComparer.OrdinalIgnoreCase.Compare("b", reported));
            source.Remove(reported);
            Assert.IsNull(max.Evaluation.Fault);
            Assert.AreEqual(reported == "B" ? "b" : "B", max.Evaluation.Result, "the remaining indistinguishable value was not reported in place of the one which left");
            source.Remove(max.Evaluation.Result);
            Assert.AreEqual("a", max.Evaluation.Result);
        }
    }
}
