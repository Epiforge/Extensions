namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryToCollectionRandomizedOperations
{
    static void AssertSameElements(IReadOnlyList<int> expected, IReadOnlyList<int> actual, string context, string subject)
    {
        Assert.AreEqual(expected.Count, actual.Count, $"{context}: {subject} has the wrong number of elements");
        var expectedCounts = CountByValue(expected);
        var actualCounts = CountByValue(actual);
        Assert.AreEqual(expectedCounts.Count, actualCounts.Count, $"{context}: {subject} has the wrong number of distinct elements");
        foreach (var (value, expectedCount) in expectedCounts)
        {
            Assert.IsTrue(actualCounts.TryGetValue(value, out var actualCount), $"{context}: {subject} is missing {value}");
            Assert.AreEqual(expectedCount, actualCount, $"{context}: {subject} holds {value} the wrong number of times");
        }
    }

    static Dictionary<int, int> CountByValue(IEnumerable<int> values)
    {
        var counts = new Dictionary<int, int>();
        foreach (var value in values)
            counts[value] = counts.TryGetValue(value, out var existing) ? existing + 1 : 1;
        return counts;
    }

    static List<int> Expected(ObservableDictionary<int, TestPerson> source)
    {
        var expected = new List<int>();
        foreach (var (_, value) in source)
        {
            var name = value.Name;
            if (name is not null)
                expected.Add(name.Length);
        }
        return expected;
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsAgreeWithADirectProjection()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunSeed(seed, false);
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsWithFaultingEntriesYieldOnlyTheEntriesWhichDoNotFault()
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
            using (var toCollectionQuery = sourceQuery.ObserveToCollection((key, value) => value.Name!.Length))
            {
                var mirror = new List<int>(toCollectionQuery);
                void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            for (var i = 0; i < e.NewItems!.Count; ++i)
                                mirror.Insert(e.NewStartingIndex + i, (int)e.NewItems[i]!);
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            mirror.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                            break;
                        case NotifyCollectionChangedAction.Replace:
                            mirror.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                            for (var i = 0; i < e.NewItems!.Count; ++i)
                                mirror.Insert(e.OldStartingIndex + i, (int)e.NewItems[i]!);
                            break;
                        case NotifyCollectionChangedAction.Move:
                            var moved = mirror.GetRange(e.OldStartingIndex, e.OldItems!.Count);
                            mirror.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                            mirror.InsertRange(e.NewStartingIndex, moved);
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            mirror.Clear();
                            mirror.AddRange(toCollectionQuery!);
                            break;
                    }
                }
                toCollectionQuery.CollectionChanged += collectionChanged;
                for (var step = 0; step < 150; ++step)
                {
                    var operation = mutations.Mutate(source);
                    var context = $"seed {seed}, step {step}, after {operation}";
                    var expected = Expected(source);
                    var actual = toCollectionQuery.ToList();
                    Assert.AreEqual(expected.Count, toCollectionQuery.Count, $"{context}: count diverged");
                    AssertSameElements(expected, actual, context, "the enumeration");
                    CollectionAssert.AreEqual(actual, mirror, $"{context}: the notification mirror diverged from the enumeration");
                    for (var i = 0; i < actual.Count; ++i)
                        Assert.AreEqual(actual[i], toCollectionQuery[i], $"{context}: the indexer diverged at {i}");
                }
                toCollectionQuery.CollectionChanged -= collectionChanged;
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
