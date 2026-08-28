namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionSelectRandomizedOperations
{
    static readonly string?[] names = ["Bill", "A", "Erin", "Georgie", "Cliff", "Jo", "Nan", "Hunt", null];

    static List<int> Expected(ObservableRangeCollection<TestPerson> source)
    {
        var expected = new List<int>(source.Count);
        for (int i = 0, ii = source.Count; i < ii; ++i)
            expected.Add(Project(source[i]));
        return expected;
    }

    static string MutateSource(Random random, ObservableRangeCollection<TestPerson> source, bool allowNullNames)
    {
        var count = source.Count;
        var choice = random.Next(100);
        if (count == 0 || choice < 20)
        {
            var index = random.Next(count + 1);
            var added = Enumerable.Range(0, random.Next(1, 4)).Select(_ => count > 0 && random.Next(3) == 0 ? source[random.Next(count)] : new TestPerson(NextName(random, allowNullNames)!)).ToList();
            source.InsertRange(index, added);
            return $"insert {added.Count} at {index}";
        }
        if (choice < 38)
        {
            var removeCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - removeCount + 1);
            source.RemoveRange(index, removeCount);
            return $"remove {removeCount} at {index}";
        }
        if (choice < 54)
        {
            var replaceCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - replaceCount + 1);
            var replacements = Enumerable.Range(0, random.Next(1, 3)).Select(_ => new TestPerson(NextName(random, allowNullNames)!)).ToList();
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
            var replacement = Enumerable.Range(0, random.Next(0, 10)).Select(_ => new TestPerson(NextName(random, allowNullNames)!)).ToList();
            source.Reset(replacement);
            return $"reset to {replacement.Count}";
        }
        var target = source[random.Next(count)];
        var name = NextName(random, allowNullNames);
        target.Name = name;
        return $"rename to {name ?? "null"}";
    }

    static string? NextName(Random random, bool allowNullNames)
    {
        while (true)
        {
            var name = names[random.Next(names.Length)];
            if (name is not null || allowNullNames)
                return name;
        }
    }

    static int Project(TestPerson person)
    {
        var name = person.Name;
        return name is null ? 0 : name.Length;
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
    public void RandomOperationsWithFaultingElementsAgreeWithADirectProjection()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunSeed(seed, true);
    }

    static void RunSeed(int seed, bool allowNullNames)
    {
        var random = new Random(seed);
        var source = new ObservableRangeCollection<TestPerson>(Enumerable.Range(0, 12).Select(index => new TestPerson(names[index % (names.Length - 1)]!)));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var selectQuery = sourceQuery.ObserveSelect(person => person.Name!.Length))
            {
                var mirror = new List<int>(selectQuery);
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
                            mirror.AddRange(selectQuery);
                            break;
                    }
                }
                selectQuery.CollectionChanged += collectionChanged;
                for (var step = 0; step < 150; ++step)
                {
                    if (step % 4 == 3)
                    {
                        var retained = Expected(source);
                        using var enumerator = selectQuery.GetEnumerator();
                        MutateSource(random, source, allowNullNames);
                        var observed = new List<int>(retained.Count);
                        while (enumerator.MoveNext())
                            observed.Add(enumerator.Current);
                        CollectionAssert.AreEqual(retained, observed, $"seed {seed}, step {step}: an enumeration in progress did not yield the projection as it was when the enumeration began");
                        continue;
                    }
                    var operation = MutateSource(random, source, allowNullNames);
                    var context = $"seed {seed}, step {step}, after {operation}";
                    var expected = Expected(source);
                    var actual = selectQuery.ToList();
                    Assert.AreEqual(expected.Count, selectQuery.Count, $"{context}: count diverged");
                    CollectionAssert.AreEqual(expected, actual, $"{context}: the projection diverged");
                    CollectionAssert.AreEqual(expected, mirror, $"{context}: the notification mirror diverged");
                    for (var i = 0; i < expected.Count; ++i)
                        Assert.AreEqual(expected[i], selectQuery[i], $"{context}: the indexer diverged at {i}");
                }
                selectQuery.CollectionChanged -= collectionChanged;
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
