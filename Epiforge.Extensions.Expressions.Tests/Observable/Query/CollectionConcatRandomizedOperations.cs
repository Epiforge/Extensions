namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionConcatRandomizedOperations
{
    static string MutateSource(Random random, ObservableRangeCollection<int> source, ref int nextValue)
    {
        var count = source.Count;
        var choice = random.Next(100);
        if (count == 0 || choice < 22)
        {
            var index = random.Next(count + 1);
            var added = new List<int>();
            for (int i = 0, ii = random.Next(1, 4); i < ii; ++i)
                added.Add(nextValue++);
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
        if (choice < 60)
        {
            var replaceCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - replaceCount + 1);
            var replacements = new List<int>();
            for (int i = 0, ii = random.Next(1, 3); i < ii; ++i)
                replacements.Add(nextValue++);
            source.ReplaceRange(index, replaceCount, replacements);
            return $"replace {replaceCount} at {index} with {replacements.Count}";
        }
        if (choice < 80)
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
    public void RandomOperationsAgreeWithADirectConcatenation()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunSeed(seed);
    }

    static void RunSeed(int seed)
    {
        var random = new Random(seed);
        var nextValue = 1000;
        var first = new ObservableRangeCollection<int>(Enumerable.Range(0, 6));
        var second = new ObservableRangeCollection<int>(Enumerable.Range(100, 6));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var firstQuery = collectionObserver.ObserveReadOnlyList(first))
        using (var secondQuery = collectionObserver.ObserveReadOnlyList(second))
        {
            using (var concatQuery = firstQuery.ObserveConcat(secondQuery))
            {
                var mirror = new List<int>(concatQuery);
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
                            mirror.AddRange(concatQuery);
                            break;
                    }
                }
                concatQuery.CollectionChanged += collectionChanged;
                for (var step = 0; step < 200; ++step)
                {
                    var mutatingFirst = random.Next(2) == 0;
                    var operation = MutateSource(random, mutatingFirst ? first : second, ref nextValue);
                    var context = $"seed {seed}, step {step}, after {(mutatingFirst ? "first" : "second")} {operation}";
                    var expected = first.Concat(second).ToList();
                    var actual = concatQuery.ToList();
                    Assert.AreEqual(expected.Count, concatQuery.Count, $"{context}: count diverged");
                    CollectionAssert.AreEqual(expected, actual, $"{context}: the concatenation diverged");
                    CollectionAssert.AreEqual(expected, mirror, $"{context}: the notification mirror diverged");
                    for (var i = 0; i < expected.Count; ++i)
                        Assert.AreEqual(expected[i], concatQuery[i], $"{context}: the indexer diverged at {i}");
                }
                concatQuery.CollectionChanged -= collectionChanged;
            }
            Assert.AreEqual(0, firstQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
