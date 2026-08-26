namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionSliceOutOfRange
{
    static List<int> Enumerate(IObservableCollectionQuery<int> query)
    {
        var enumerated = new List<int>();
        foreach (var element in query)
            enumerated.Add(element);
        return enumerated;
    }

    [TestMethod]
    public void SkipNegativeCountsFromTheEnd()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var skipQuery = sourceQuery.ObserveSkip(-3))
            {
                Assert.AreEqual(3, skipQuery.Count);
                Assert.IsTrue(new int[] { 7, 8, 9 }.SequenceEqual(Enumerate(skipQuery)));
            }
            using (var skipQuery = sourceQuery.ObserveSkip(-1))
            {
                Assert.AreEqual(1, skipQuery.Count);
                Assert.IsTrue(new int[] { 9 }.SequenceEqual(Enumerate(skipQuery)));
            }
            using (var skipQuery = sourceQuery.ObserveSkip(-20))
            {
                Assert.AreEqual(10, skipQuery.Count);
                Assert.IsTrue(Enumerable.Range(0, 10).SequenceEqual(Enumerate(skipQuery)));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SkipAndTakeOfTheSameCountPartitionTheCollection()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            foreach (var count in new int[] { -20, -7, -1, 0, 1, 7, 20 })
            {
                using var takeQuery = sourceQuery.ObserveTake(count);
                using var skipQuery = sourceQuery.ObserveSkip(count);
                Assert.IsTrue(Enumerable.Range(0, 10).SequenceEqual(Enumerate(takeQuery).Concat(Enumerate(skipQuery))));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SkipPastTheEnd()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var skipQuery = sourceQuery.ObserveSkip(1000))
            {
                Assert.AreEqual(0, skipQuery.Count);
                Assert.HasCount(0, Enumerate(skipQuery));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SkipSurvivesTheSourceEmptying()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var skipQuery = sourceQuery.ObserveSkip(2))
            {
                Assert.HasCount(8, Enumerate(skipQuery));
                source.Clear();
                Assert.AreEqual(0, skipQuery.Count);
                Assert.HasCount(0, Enumerate(skipQuery));
                source.AddRange(Enumerable.Range(0, 5));
                Assert.IsTrue(new int[] { 2, 3, 4 }.SequenceEqual(Enumerate(skipQuery)));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SliceFromEndBeforeTheStart()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var sliceQuery = sourceQuery.ObserveSlice(^1000..))
            {
                Assert.AreEqual(10, sliceQuery.Count);
                Assert.AreEqual(0, sliceQuery[0]);
                Assert.IsTrue(Enumerable.Range(0, 10).SequenceEqual(Enumerate(sliceQuery)));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void TakeNegativeCountsFromTheEnd()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var takeQuery = sourceQuery.ObserveTake(-3))
            {
                Assert.AreEqual(7, takeQuery.Count);
                Assert.IsTrue(Enumerable.Range(0, 7).SequenceEqual(Enumerate(takeQuery)));
            }
            using (var takeQuery = sourceQuery.ObserveTake(-1))
            {
                Assert.AreEqual(9, takeQuery.Count);
                Assert.IsTrue(Enumerable.Range(0, 9).SequenceEqual(Enumerate(takeQuery)));
            }
            using (var takeQuery = sourceQuery.ObserveTake(-20))
            {
                Assert.AreEqual(0, takeQuery.Count);
                Assert.HasCount(0, Enumerate(takeQuery));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void TakePastTheEnd()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var takeQuery = sourceQuery.ObserveTake(1000))
            {
                Assert.AreEqual(10, takeQuery.Count);
                Assert.IsTrue(Enumerable.Range(0, 10).SequenceEqual(Enumerate(takeQuery)));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void TakeSurvivesTheSourceShrinking()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var takeQuery = sourceQuery.ObserveTake(6))
            {
                Assert.HasCount(6, Enumerate(takeQuery));
                source.RemoveRange(2, 8);
                Assert.AreEqual(2, takeQuery.Count);
                Assert.IsTrue(new int[] { 0, 1 }.SequenceEqual(Enumerate(takeQuery)));
                source.Clear();
                Assert.AreEqual(0, takeQuery.Count);
                Assert.HasCount(0, Enumerate(takeQuery));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
