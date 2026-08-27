namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionarySelectRandomizedOperations
{
    static Dictionary<int, int> ExpectedByKey(ObservableDictionary<int, TestPerson> source)
    {
        var expected = new Dictionary<int, int>();
        foreach (var (key, value) in source)
            if (NameLength(value) is { } length)
                expected.Add(key, length);
        return expected;
    }

    static Dictionary<int, List<string>> ExpectedByNameLength(ObservableDictionary<int, TestPerson> source)
    {
        var expected = new Dictionary<int, List<string>>();
        foreach (var (_, value) in source)
        {
            var name = value.Name!;
            if (expected.TryGetValue(name.Length, out var candidates))
                candidates.Add(name);
            else
                expected.Add(name.Length, [name]);
        }
        return expected;
    }

    static int? NameLength(TestPerson value)
    {
        var name = value.Name;
        return name is null ? null : name.Length;
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsWithAnInjectiveKeySelectorAgreeWithADirectProjection()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunInjectiveSeed(seed, false);
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsWithFaultingEntriesProjectOnlyTheEntriesWhichDoNotFault()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunInjectiveSeed(seed, true);
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsWithACollidingKeySelectorKeepOneClaimantPerProjectedKey()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunCollidingSeed(seed);
    }

    static void RunCollidingSeed(int seed)
    {
        var random = new Random(seed);
        var mutations = new DictionaryRandomizedMutations(random, false);
        var source = mutations.CreateSource(12);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var selectQuery = sourceQuery.ObserveSelect((key, value) => value.Name!.Length, (key, value) => value.Name!))
            {
                for (var step = 0; step < 150; ++step)
                {
                    var operation = mutations.Mutate(source);
                    var context = $"seed {seed}, step {step}, after {operation}";
                    var expected = ExpectedByNameLength(source);
                    var actual = selectQuery.ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => keyValuePair.Value);
                    Assert.AreEqual(expected.Count, selectQuery.Count, $"{context}: count diverged");
                    Assert.AreEqual(expected.Count, actual.Count, $"{context}: the enumeration yielded the wrong number of entries");
                    foreach (var (length, candidates) in expected)
                    {
                        Assert.IsTrue(actual.TryGetValue(length, out var name), $"{context}: no entry was projected for length {length}");
                        Assert.IsTrue(candidates.Contains(name!), $"{context}: the value at {length} is {name}, which no source entry of that length holds");
                        Assert.AreEqual(name, selectQuery[length], $"{context}: the indexer diverged at {length}");
                    }
                }
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    static void RunInjectiveSeed(int seed, bool allowNullNames)
    {
        var random = new Random(seed);
        var mutations = new DictionaryRandomizedMutations(random, allowNullNames);
        var source = mutations.CreateSource(12);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var selectQuery = sourceQuery.ObserveSelect((key, value) => key, (key, value) => value.Name!.Length))
            {
                var mirror = selectQuery.ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => keyValuePair.Value);
                void dictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<int, int> e)
                {
                    if (e.Action is NotifyDictionaryChangedAction.Reset)
                    {
                        mirror.Clear();
                        foreach (var keyValuePair in selectQuery!)
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
                ((INotifyDictionaryChanged<int, int>)selectQuery).DictionaryChanged += dictionaryChanged;
                for (var step = 0; step < 150; ++step)
                {
                    var operation = mutations.Mutate(source);
                    var context = $"seed {seed}, step {step}, after {operation}";
                    var expected = ExpectedByKey(source);
                    var actual = selectQuery.ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => keyValuePair.Value);
                    Assert.AreEqual(expected.Count, selectQuery.Count, $"{context}: count diverged");
                    Assert.AreEqual(expected.Count, actual.Count, $"{context}: the enumeration yielded the wrong number of entries");
                    foreach (var (key, length) in expected)
                    {
                        Assert.IsTrue(actual.TryGetValue(key, out var actualLength), $"{context}: {key} is missing from the projection");
                        Assert.AreEqual(length, actualLength, $"{context}: the projected value at {key} diverged");
                        Assert.AreEqual(length, selectQuery[key], $"{context}: the indexer diverged at {key}");
                    }
                    Assert.AreEqual(expected.Count, mirror.Count, $"{context}: the notification mirror has the wrong number of entries");
                    foreach (var (key, length) in expected)
                    {
                        Assert.IsTrue(mirror.TryGetValue(key, out var mirroredLength), $"{context}: {key} is missing from the notification mirror");
                        Assert.AreEqual(length, mirroredLength, $"{context}: the notification mirror diverged at {key}");
                    }
                }
                ((INotifyDictionaryChanged<int, int>)selectQuery).DictionaryChanged -= dictionaryChanged;
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
