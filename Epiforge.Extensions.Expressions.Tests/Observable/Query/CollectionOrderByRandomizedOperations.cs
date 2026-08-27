namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionOrderByRandomizedOperations
{
    static readonly string[] names = ["Bill", "A", "Erin", "Georgie", "Cliff", "Jo", "Nan", "Hunt", "Erin", "Jo"];

    static void AssertSameElements(IReadOnlyList<TestPerson> expected, IReadOnlyList<TestPerson> actual, string context)
    {
        var expectedCounts = CountByInstance(expected);
        var actualCounts = CountByInstance(actual);
        Assert.AreEqual(expectedCounts.Count, actualCounts.Count, $"{context}: the number of distinct elements diverged");
        foreach (var (person, expectedCount) in expectedCounts)
        {
            Assert.IsTrue(actualCounts.TryGetValue(person, out var actualCount), $"{context}: {person.Name} is missing from the ordering");
            Assert.AreEqual(expectedCount, actualCount, $"{context}: {person.Name} appears the wrong number of times");
        }
    }

    static int Compare(TestPerson? x, TestPerson? y)
    {
        var comparison = x!.Name!.Length.CompareTo(y!.Name!.Length);
        return comparison != 0 ? comparison : Comparer<string>.Default.Compare(x.Name!, y.Name!);
    }

    static Dictionary<TestPerson, int> CountByInstance(IEnumerable<TestPerson> people)
    {
        var counts = new Dictionary<TestPerson, int>();
        foreach (var person in people)
            counts[person] = counts.TryGetValue(person, out var existing) ? existing + 1 : 1;
        return counts;
    }

    static string MutateSource(Random random, ObservableRangeCollection<TestPerson> source)
    {
        var count = source.Count;
        var choice = random.Next(100);
        if (count == 0 || choice < 22)
        {
            var index = random.Next(count + 1);
            var added = Enumerable.Range(0, random.Next(1, 4)).Select(_ => count > 0 && random.Next(4) == 0 ? source[random.Next(count)] : new TestPerson(names[random.Next(names.Length)])).ToList();
            source.InsertRange(index, added);
            return $"insert {added.Count} at {index}";
        }
        if (choice < 42)
        {
            var removeCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - removeCount + 1);
            source.RemoveRange(index, removeCount);
            return $"remove {removeCount} at {index}";
        }
        if (choice < 56)
        {
            var replaceCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - replaceCount + 1);
            var replacements = Enumerable.Range(0, random.Next(1, 3)).Select(_ => new TestPerson(names[random.Next(names.Length)])).ToList();
            source.ReplaceRange(index, replaceCount, replacements);
            return $"replace {replaceCount} at {index} with {replacements.Count}";
        }
        if (choice < 68)
        {
            var moveCount = random.Next(1, Math.Min(3, count) + 1);
            var oldIndex = random.Next(count - moveCount + 1);
            var newIndex = random.Next(count - moveCount + 1);
            source.MoveRange(oldIndex, newIndex, moveCount);
            return $"move {moveCount} from {oldIndex} to {newIndex}";
        }
        if (choice < 73)
        {
            var replacement = Enumerable.Range(0, random.Next(0, 12)).Select(_ => new TestPerson(names[random.Next(names.Length)])).ToList();
            source.Reset(replacement);
            return $"reset to {replacement.Count}";
        }
        var target = source[random.Next(count)];
        var name = names[random.Next(names.Length)];
        target.Name = name;
        return $"rename to {name}";
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsLeaveTheOrderingSortedAndComplete()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunSeed(seed);
    }

    static void RunSeed(int seed)
    {
        var random = new Random(seed);
        var source = new ObservableRangeCollection<TestPerson>(Enumerable.Range(0, 12).Select(index => new TestPerson(names[index % names.Length])));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy((person => person.Name!.Length, false), (person => person.Name!, false)))
            {
                var mirror = new List<TestPerson>(orderByQuery);
                void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            for (var i = 0; i < e.NewItems!.Count; ++i)
                                mirror.Insert(e.NewStartingIndex + i, (TestPerson)e.NewItems[i]!);
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            mirror.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                            break;
                        case NotifyCollectionChangedAction.Replace:
                            mirror.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                            for (var i = 0; i < e.NewItems!.Count; ++i)
                                mirror.Insert(e.OldStartingIndex + i, (TestPerson)e.NewItems[i]!);
                            break;
                        case NotifyCollectionChangedAction.Move:
                            var moved = mirror.GetRange(e.OldStartingIndex, e.OldItems!.Count);
                            mirror.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                            mirror.InsertRange(e.NewStartingIndex, moved);
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            mirror.Clear();
                            mirror.AddRange(orderByQuery);
                            break;
                    }
                }
                orderByQuery.CollectionChanged += collectionChanged;
                for (var step = 0; step < 150; ++step)
                {
                    var operation = MutateSource(random, source);
                    var context = $"seed {seed}, step {step}, after {operation}";
                    var ordered = orderByQuery.ToList();
                    Assert.AreEqual(source.Count, orderByQuery.Count, $"{context}: count diverged");
                    Assert.AreEqual(source.Count, ordered.Count, $"{context}: the enumeration yielded the wrong number of elements");
                    AssertSameElements(source, ordered, context);
                    for (var i = 1; i < ordered.Count; ++i)
                        Assert.IsTrue(Compare(ordered[i - 1], ordered[i]) <= 0, $"{context}: {ordered[i - 1].Name} precedes {ordered[i].Name} at position {i}");
                    CollectionAssert.AreEqual(ordered, mirror, $"{context}: the notification mirror diverged");
                    if (ordered.Count > 0)
                    {
                        var probe = random.Next(ordered.Count);
                        Assert.AreSame(ordered[probe], orderByQuery[probe], $"{context}: the indexer diverged at {probe}");
                    }
                }
                orderByQuery.CollectionChanged -= collectionChanged;
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
