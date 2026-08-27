namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryWhereRandomizedOperations
{
    static void AssertSame(IReadOnlyDictionary<int, TestPerson> expected, IReadOnlyDictionary<int, TestPerson> actual, string context, string subject)
    {
        Assert.AreEqual(expected.Count, actual.Count, $"{context}: {subject} has the wrong number of entries");
        foreach (var (key, value) in expected)
        {
            Assert.IsTrue(actual.TryGetValue(key, out var actualValue), $"{context}: {subject} is missing {key}");
            Assert.AreSame(value, actualValue, $"{context}: {subject} has the wrong value at {key}");
        }
    }

    static Dictionary<int, TestPerson> Expected(ObservableDictionary<int, TestPerson> source)
    {
        var expected = new Dictionary<int, TestPerson>();
        foreach (var (key, value) in source)
            if (Matches(key, value))
                expected.Add(key, value);
        return expected;
    }

    static bool Matches(int key, TestPerson value)
    {
        try
        {
            return (key + value.Name!.Length) % 2 == 0;
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsAgreeWithADirectFilter()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunSeed(seed, false);
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsWithFaultingEntriesExcludeOnlyTheFaultedEntries()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunSeed(seed, true);
    }

    static void RunSeed(int seed, bool allowNullNames)
    {
        var random = new Random(seed);
        var mutations = new DictionaryRandomizedMutations(random, allowNullNames);
        var source = mutations.CreateSource(12);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere((key, value) => (key + value.Name!.Length) % 2 == 0))
            {
                var mirror = whereQuery.ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => keyValuePair.Value);
                void dictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<int, TestPerson> e)
                {
                    if (e.Action is NotifyDictionaryChangedAction.Reset)
                    {
                        mirror.Clear();
                        foreach (var keyValuePair in whereQuery!)
                            mirror[keyValuePair.Key] = keyValuePair.Value;
                        return;
                    }
                    if (e.OldItems is { } oldItems)
                        foreach (var keyValuePair in oldItems)
                            mirror.Remove(keyValuePair.Key);
                    if (e.NewItems is { } newItems)
                        foreach (var keyValuePair in newItems)
                            mirror[keyValuePair.Key] = keyValuePair.Value;
                }
                ((INotifyDictionaryChanged<int, TestPerson>)whereQuery).DictionaryChanged += dictionaryChanged;
                for (var step = 0; step < 150; ++step)
                {
                    var operation = mutations.Mutate(source);
                    var context = $"seed {seed}, step {step}, after {operation}";
                    var expected = Expected(source);
                    Assert.AreEqual(expected.Count, whereQuery.Count, $"{context}: count diverged");
                    AssertSame(expected, whereQuery.ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => keyValuePair.Value), context, "the enumeration");
                    AssertSame(expected, mirror, context, "the notification mirror");
                    foreach (var (key, value) in expected)
                    {
                        Assert.IsTrue(whereQuery.ContainsKey(key), $"{context}: the query does not contain {key}");
                        Assert.AreSame(value, whereQuery[key], $"{context}: the indexer diverged at {key}");
                    }
                }
                ((INotifyDictionaryChanged<int, TestPerson>)whereQuery).DictionaryChanged -= dictionaryChanged;
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
