namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionToLookupRandomizedOperations
{
    static void AssertLookupAgrees(ObservableRangeCollection<int> source, IObservableLookupQuery<int, int> lookupQuery, HashSet<int> keysEverSeen, string context)
    {
        var expected = new Dictionary<int, List<int>>();
        foreach (var key in keysEverSeen)
            expected.Add(key, []);
        foreach (var value in source)
            expected[Key(value)].Add(value);
        foreach (var key in expected.Keys)
            expected[key].Sort();
        var observed = new Dictionary<int, List<int>>();
        foreach (var grouping in lookupQuery.Values)
        {
            Assert.IsFalse(observed.ContainsKey(grouping.Key), $"{context}: key {grouping.Key} was present in more than one grouping");
            observed.Add(grouping.Key, grouping.OrderBy(value => value).ToList());
        }
        CollectionAssert.AreEquivalent(expected.Keys.ToList(), observed.Keys.ToList(), $"{context}: the set of retained keys diverged");
        foreach (var key in expected.Keys)
            CollectionAssert.AreEqual(expected[key], observed[key], $"{context}: the members of grouping {key} diverged");
        Assert.IsNull(lookupQuery.OperationFault, $"{context}: an unexpected fault was reported");
    }

    static int Key(int value) =>
        value % 9;

    static string MutateSource(Random random, ObservableRangeCollection<int> source, ref int nextValue)
    {
        var count = source.Count;
        var choice = random.Next(100);
        if (count == 0 || choice < 30)
        {
            var index = random.Next(count + 1);
            var added = new List<int>();
            for (int i = 0, ii = random.Next(1, 4); i < ii; ++i)
                added.Add(count > 0 && random.Next(5) == 0 ? source[random.Next(count)] : nextValue++);
            source.InsertRange(index, added);
            return $"insert {added.Count} at {index}";
        }
        if (choice < 60)
        {
            var removeCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - removeCount + 1);
            source.RemoveRange(index, removeCount);
            return $"remove {removeCount} at {index}";
        }
        if (choice < 74)
        {
            var replaceCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - replaceCount + 1);
            var replacements = new List<int>();
            for (int i = 0, ii = random.Next(1, 3); i < ii; ++i)
                replacements.Add(nextValue++);
            source.ReplaceRange(index, replaceCount, replacements);
            return $"replace {replaceCount} at {index} with {replacements.Count}";
        }
        if (choice < 90)
        {
            var moveCount = random.Next(1, Math.Min(3, count) + 1);
            var oldIndex = random.Next(count - moveCount + 1);
            var newIndex = random.Next(count - moveCount + 1);
            source.MoveRange(oldIndex, newIndex, moveCount);
            return $"move {moveCount} from {oldIndex} to {newIndex}";
        }
        var replacement = new List<int>();
        for (int i = 0, ii = random.Next(0, 8); i < ii; ++i)
            replacement.Add(nextValue++);
        source.Reset(replacement);
        return $"reset to {replacement.Count}";
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsAgreeWithADirectLookup()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunSeed(seed);
    }

    static void RunSeed(int seed)
    {
        var random = new Random(seed);
        var nextValue = 1000;
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 6));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var lookupQuery = sourceQuery.ObserveToLookup(value => value % 9))
            {
                var keysEverSeen = new HashSet<int>();
                foreach (var value in source)
                    keysEverSeen.Add(Key(value));
                AssertLookupAgrees(source, lookupQuery, keysEverSeen, $"seed {seed}, before any changes");
                for (var step = 0; step < 150; ++step)
                {
                    var operation = MutateSource(random, source, ref nextValue);
                    foreach (var value in source)
                        keysEverSeen.Add(Key(value));
                    AssertLookupAgrees(source, lookupQuery, keysEverSeen, $"seed {seed}, step {step}, after {operation}");
                }
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
