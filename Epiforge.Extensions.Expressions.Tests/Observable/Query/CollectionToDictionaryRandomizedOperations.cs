namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionToDictionaryRandomizedOperations
{
    static void AssertDictionaryAgrees(ObservableRangeCollection<int> source, IObservableDictionaryQuery<int, int> toDictionaryQuery, string context)
    {
        var expected = new Dictionary<int, int>();
        foreach (var value in source)
            if (!expected.ContainsKey(Key(value)))
                expected.Add(Key(value), value);
        Assert.AreEqual(expected.Count, toDictionaryQuery.Count, $"{context}: the count diverged");
        foreach (var keyValuePair in expected)
        {
            int observed;
            try
            {
                observed = toDictionaryQuery[keyValuePair.Key];
            }
            catch (KeyNotFoundException)
            {
                Assert.Fail($"{context}: the dictionary did not contain key {keyValuePair.Key}");
                return;
            }
            Assert.AreEqual(keyValuePair.Value, observed, $"{context}: the value for key {keyValuePair.Key} diverged");
        }
        Assert.AreEqual(source.Count - expected.Count, FaultCount(toDictionaryQuery.OperationFault), $"{context}: the number of duplicate key faults diverged");
    }

    static int FaultCount(Exception? operationFault) =>
        operationFault switch
        {
            null => 0,
            AggregateException aggregateException => aggregateException.InnerExceptions.Count,
            _ => 1
        };

    static int Key(int value) =>
        value % 4;

    static string MutateSource(Random random, ObservableRangeCollection<int> source, ref int nextValue)
    {
        var count = source.Count;
        var choice = random.Next(100);
        if (count == 0 || choice < 26)
        {
            var index = random.Next(count + 1);
            var added = new List<int>();
            for (int i = 0, ii = random.Next(1, 4); i < ii; ++i)
                added.Add(count > 0 && random.Next(5) == 0 ? source[random.Next(count)] : nextValue++);
            source.InsertRange(index, added);
            return $"insert {added.Count} at {index}";
        }
        if (choice < 52)
        {
            var removeCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - removeCount + 1);
            source.RemoveRange(index, removeCount);
            return $"remove {removeCount} at {index}";
        }
        if (choice < 70)
        {
            var replaceCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - replaceCount + 1);
            var replacements = new List<int>();
            for (int i = 0, ii = random.Next(1, 3); i < ii; ++i)
                replacements.Add(nextValue++);
            source.ReplaceRange(index, replaceCount, replacements);
            return $"replace {replaceCount} at {index} with {replacements.Count}";
        }
        if (choice < 88)
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
    public void RandomOperationsAgreeWithADirectConversion()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunSeed(seed);
    }

    static void RunSeed(int seed)
    {
        var random = new Random(seed);
        var nextValue = 1000;
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 12));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var toDictionaryQuery = sourceQuery.ObserveToDictionary(value => value % 4))
            {
                AssertDictionaryAgrees(source, toDictionaryQuery, $"seed {seed}, before any changes");
                for (var step = 0; step < 150; ++step)
                {
                    var operation = MutateSource(random, source, ref nextValue);
                    AssertDictionaryAgrees(source, toDictionaryQuery, $"seed {seed}, step {step}, after {operation}");
                }
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
