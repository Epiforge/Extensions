namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionGroupByRandomizedOperations
{
    static void AssertGroupsAgree(ObservableRangeCollection<int> source, IObservableCollectionQuery<IObservableGrouping<int, int>> groupByQuery, string context)
    {
        var expected = source.GroupBy(Key).ToDictionary(grouping => grouping.Key, grouping => grouping.OrderBy(value => value).ToList());
        var observed = new Dictionary<int, List<int>>();
        foreach (var grouping in groupByQuery)
        {
            Assert.IsFalse(observed.ContainsKey(grouping.Key), $"{context}: key {grouping.Key} was present in more than one grouping");
            observed.Add(grouping.Key, grouping.OrderBy(value => value).ToList());
        }
        Assert.AreEqual(expected.Count, groupByQuery.Count, $"{context}: the number of groupings diverged");
        CollectionAssert.AreEquivalent(expected.Keys.ToList(), observed.Keys.ToList(), $"{context}: the set of keys diverged");
        foreach (var key in expected.Keys)
            CollectionAssert.AreEqual(expected[key], observed[key], $"{context}: the members of grouping {key} diverged");
    }

    static int Key(int value) =>
        value % 5;

    static string MutateSource(Random random, ObservableRangeCollection<int> source, ref int nextValue)
    {
        var count = source.Count;
        var choice = random.Next(100);
        if (count == 0 || choice < 26)
        {
            var index = random.Next(count + 1);
            var added = new List<int>();
            for (int i = 0, ii = random.Next(1, 4); i < ii; ++i)
                added.Add(count > 0 && random.Next(4) == 0 ? source[random.Next(count)] : nextValue++);
            source.InsertRange(index, added);
            return $"insert {added.Count} at {index}";
        }
        if (choice < 50)
        {
            var removeCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - removeCount + 1);
            source.RemoveRange(index, removeCount);
            return $"remove {removeCount} at {index}";
        }
        if (choice < 68)
        {
            var replaceCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - replaceCount + 1);
            var replacements = new List<int>();
            for (int i = 0, ii = random.Next(1, 3); i < ii; ++i)
                replacements.Add(nextValue++);
            source.ReplaceRange(index, replaceCount, replacements);
            return $"replace {replaceCount} at {index} with {replacements.Count}";
        }
        if (choice < 84)
        {
            var moveCount = random.Next(1, Math.Min(3, count) + 1);
            var oldIndex = random.Next(count - moveCount + 1);
            var newIndex = random.Next(count - moveCount + 1);
            source.MoveRange(oldIndex, newIndex, moveCount);
            return $"move {moveCount} from {oldIndex} to {newIndex}";
        }
        var replacement = new List<int>();
        for (int i = 0, ii = random.Next(0, 9); i < ii; ++i)
            replacement.Add(nextValue++);
        source.Reset(replacement);
        return $"reset to {replacement.Count}";
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsAgreeWithADirectGrouping()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunSeed(seed);
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsLeaveDistinctAgreeingWithTheDistinctKeys()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunDistinctSeed(seed);
    }

    static void RunDistinctSeed(int seed)
    {
        var random = new Random(seed);
        var nextValue = 1000;
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 12).Select(index => index * 3));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var distinctQuery = sourceQuery.ObserveDistinct())
            {
                for (var step = 0; step < 150; ++step)
                {
                    var operation = MutateSource(random, source, ref nextValue);
                    var context = $"seed {seed}, step {step}, after {operation}";
                    var expected = source.Distinct().OrderBy(value => value).ToList();
                    var observed = distinctQuery.OrderBy(value => value).ToList();
                    Assert.AreEqual(expected.Count, distinctQuery.Count, $"{context}: the count diverged");
                    CollectionAssert.AreEqual(expected, observed, $"{context}: the distinct values diverged");
                }
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    static void RunSeed(int seed)
    {
        var random = new Random(seed);
        var nextValue = 1000;
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 12));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var groupByQuery = sourceQuery.ObserveGroupBy(value => value % 5))
            {
                for (var step = 0; step < 150; ++step)
                {
                    var operation = MutateSource(random, source, ref nextValue);
                    AssertGroupsAgree(source, groupByQuery, $"seed {seed}, step {step}, after {operation}");
                }
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
