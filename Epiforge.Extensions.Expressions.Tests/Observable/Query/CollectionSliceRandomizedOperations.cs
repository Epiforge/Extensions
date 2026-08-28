namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionSliceRandomizedOperations
{
    static readonly Range[] ranges = new Range[] { 2..8, 0..^2, ^5..^0, 1..^1, 3..^3 };

    static string MutateSource(Random random, ObservableRangeCollection<int> source, ref int nextValue)
    {
        var count = source.Count;
        var choice = random.Next(100);
        if (count == 0 || choice < 24)
        {
            var index = random.Next(count + 1);
            var added = new List<int>();
            for (int i = 0, ii = random.Next(1, 4); i < ii; ++i)
                added.Add(nextValue++);
            source.InsertRange(index, added);
            return $"insert {added.Count} at {index}";
        }
        if (choice < 46)
        {
            var removeCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - removeCount + 1);
            source.RemoveRange(index, removeCount);
            return $"remove {removeCount} at {index}";
        }
        if (choice < 64)
        {
            var replaceCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - replaceCount + 1);
            var replacements = new List<int>();
            for (int i = 0, ii = random.Next(1, 3); i < ii; ++i)
                replacements.Add(nextValue++);
            source.ReplaceRange(index, replaceCount, replacements);
            return $"replace {replaceCount} at {index} with {replacements.Count}";
        }
        if (choice < 82)
        {
            var moveCount = random.Next(1, Math.Min(3, count) + 1);
            var oldIndex = random.Next(count - moveCount + 1);
            var newIndex = random.Next(count - moveCount + 1);
            source.MoveRange(oldIndex, newIndex, moveCount);
            return $"move {moveCount} from {oldIndex} to {newIndex}";
        }
        var replacement = new List<int>();
        for (int i = 0, ii = random.Next(0, 10); i < ii; ++i)
            replacement.Add(nextValue++);
        source.Reset(replacement);
        return $"reset to {replacement.Count}";
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsAgreeWithASkipAndTake()
    {
        for (var seed = 0; seed < 10; ++seed)
            RunSkipAndTakeSeed(seed);
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsAgreeWithATakenRange()
    {
        for (var seed = 0; seed < 10; ++seed)
            foreach (var range in ranges)
                RunRangeSeed(seed, range);
    }

    static void RunRangeSeed(int seed, Range range)
    {
        var random = new Random(seed);
        var nextValue = 1000;
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var sliceQuery = sourceQuery.ObserveSlice(range))
            {
                for (var step = 0; step < 120; ++step)
                {
                    var operation = MutateSource(random, source, ref nextValue);
                    var context = $"seed {seed}, range {range}, step {step}, after {operation}";
                    var expected = source.Take(range).ToList();
                    var actual = sliceQuery.ToList();
                    Assert.AreEqual(expected.Count, sliceQuery.Count, $"{context}: count diverged");
                    CollectionAssert.AreEqual(expected, actual, $"{context}: the slice diverged");
                    for (var i = 0; i < expected.Count; ++i)
                        Assert.AreEqual(expected[i], sliceQuery[i], $"{context}: the indexer diverged at {i}");
                }
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    static void RunSkipAndTakeSeed(int seed)
    {
        var random = new Random(seed);
        var nextValue = 1000;
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var skipQuery = sourceQuery.ObserveSkip(3))
            using (var takeQuery = sourceQuery.ObserveTake(4))
            {
                for (var step = 0; step < 120; ++step)
                {
                    var operation = MutateSource(random, source, ref nextValue);
                    var context = $"seed {seed}, step {step}, after {operation}";
                    var expectedSkip = source.Skip(3).ToList();
                    var expectedTake = source.Take(4).ToList();
                    Assert.AreEqual(expectedSkip.Count, skipQuery.Count, $"{context}: the skipped count diverged");
                    Assert.AreEqual(expectedTake.Count, takeQuery.Count, $"{context}: the taken count diverged");
                    CollectionAssert.AreEqual(expectedSkip, skipQuery.ToList(), $"{context}: the skipped elements diverged");
                    CollectionAssert.AreEqual(expectedTake, takeQuery.ToList(), $"{context}: the taken elements diverged");
                    for (var i = 0; i < expectedSkip.Count; ++i)
                        Assert.AreEqual(expectedSkip[i], skipQuery[i], $"{context}: the skipped indexer diverged at {i}");
                    for (var i = 0; i < expectedTake.Count; ++i)
                        Assert.AreEqual(expectedTake[i], takeQuery[i], $"{context}: the taken indexer diverged at {i}");
                }
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
