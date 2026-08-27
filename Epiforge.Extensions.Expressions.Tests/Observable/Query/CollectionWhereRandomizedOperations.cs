namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionWhereRandomizedOperations
{
    static readonly string[] names = ["Bill", "A", "Erin", "Georgie", "Cliff", "Jo", "Nan", "Hunt"];

    static string MutateSource(Random random, ObservableRangeCollection<TestPerson> source)
    {
        var count = source.Count;
        var choice = random.Next(100);
        if (count == 0 || choice < 20)
        {
            var index = random.Next(count + 1);
            var added = Enumerable.Range(0, random.Next(1, 4)).Select(_ => count > 0 && random.Next(4) == 0 ? source[random.Next(count)] : new TestPerson(names[random.Next(names.Length)])).ToList();
            source.InsertRange(index, added);
            return $"insert {added.Count} at {index}";
        }
        if (choice < 40)
        {
            var removeCount = random.Next(1, Math.Min(4, count) + 1);
            var index = random.Next(count - removeCount + 1);
            source.RemoveRange(index, removeCount);
            return $"remove {removeCount} at {index}";
        }
        if (choice < 55)
        {
            var replaceCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - replaceCount + 1);
            var replacements = Enumerable.Range(0, random.Next(1, 4)).Select(_ => new TestPerson(names[random.Next(names.Length)])).ToList();
            source.ReplaceRange(index, replaceCount, replacements);
            return $"replace {replaceCount} at {index} with {replacements.Count}";
        }
        if (choice < 65)
        {
            var moveCount = random.Next(1, Math.Min(4, count) + 1);
            var oldIndex = random.Next(count - moveCount + 1);
            var newIndex = random.Next(count - moveCount + 1);
            source.MoveRange(oldIndex, newIndex, moveCount);
            return $"move {moveCount} from {oldIndex} to {newIndex}";
        }
        if (choice < 70)
        {
            var replacement = Enumerable.Range(0, random.Next(0, 25)).Select(_ => new TestPerson(names[random.Next(names.Length)])).ToList();
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
    public void RandomStructuralAndValueChangesAgreeWithADirectFilter()
    {
        for (var seed = 0; seed < 25; ++seed)
            RunSeed(seed);
    }

    static void RunSeed(int seed)
    {
        var random = new Random(seed);
        var source = new ObservableRangeCollection<TestPerson>(Enumerable.Range(0, 30).Select(index => new TestPerson(names[index % names.Length])));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length == 4))
            {
                var mirror = new List<TestPerson>(whereQuery);
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
                            mirror.AddRange(whereQuery);
                            break;
                    }
                }
                whereQuery.CollectionChanged += collectionChanged;
                for (var step = 0; step < 200; ++step)
                {
                    string operation;
                    if (random.Next(4) == 0)
                    {
                        var expectedBeforeMutating = source.Where(person => person.Name!.Length == 4).ToList();
                        var drained = new List<TestPerson>();
                        using (var live = whereQuery.GetEnumerator())
                        {
                            if (live.MoveNext())
                                drained.Add(live.Current);
                            operation = MutateSource(random, source);
                            while (live.MoveNext())
                                drained.Add(live.Current);
                        }
                        CollectionAssert.AreEqual(expectedBeforeMutating, drained, $"seed {seed}, step {step}, after {operation}: an enumerator begun before the mutation did not yield the collection as it was");
                    }
                    else
                        operation = MutateSource(random, source);
                    var context = $"seed {seed}, step {step}, after {operation}";
                    var expected = source.Where(person => person.Name!.Length == 4).ToList();
                    CollectionAssert.AreEqual(expected, whereQuery.ToList(), $"{context}: contents diverged");
                    CollectionAssert.AreEqual(expected, mirror, $"{context}: the notification mirror diverged");
                    Assert.AreEqual(expected.Count, whereQuery.Count, $"{context}: count diverged");
                    if (expected.Count > 0)
                    {
                        var probe = random.Next(expected.Count);
                        Assert.AreSame(expected[probe], whereQuery[probe], $"{context}: the indexer diverged at {probe}");
                    }
                }
                whereQuery.CollectionChanged -= collectionChanged;
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
