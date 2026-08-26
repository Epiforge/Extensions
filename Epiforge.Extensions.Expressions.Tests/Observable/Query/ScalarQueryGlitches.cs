namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class ScalarQueryGlitches
{
    static List<TResult> RecordEvaluations<TResult>(IObservableScalarQuery<TResult> query, Action mutate)
    {
        var recorded = new List<TResult>();
        void handler(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IObservableScalarQuery<TResult>.Evaluation))
                recorded.Add(query.Evaluation.Result);
        }
        query.PropertyChanged += handler;
        try
        {
            mutate();
        }
        finally
        {
            query.PropertyChanged -= handler;
        }
        return recorded;
    }

    [TestMethod]
    public void AllPublishesNothingWhenAMatchingElementIsAdded()
    {
        var source = new ObservableRangeCollection<int>([2, 4, 6]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var allQuery = sourceQuery.ObserveAll(element => element % 2 == 0))
            {
                Assert.IsTrue(allQuery.Evaluation.Result);
                Assert.HasCount(0, RecordEvaluations(allQuery, () => source.Add(8)));
                Assert.IsTrue(allQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void AllPublishesOnceWhenAnUnmatchingElementIsAdded()
    {
        var source = new ObservableRangeCollection<int>([2, 4, 6]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var allQuery = sourceQuery.ObserveAll(element => element % 2 == 0))
            {
                Assert.IsTrue(allQuery.Evaluation.Result);
                CollectionAssert.AreEqual(new bool[] { false }, RecordEvaluations(allQuery, () => source.Add(7)));
                Assert.IsFalse(allQuery.Evaluation.Result);
                CollectionAssert.AreEqual(new bool[] { true }, RecordEvaluations(allQuery, () => source.Remove(7)));
                Assert.IsTrue(allQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void AveragePublishesOnceWhenAnElementIsAdded()
    {
        var source = new ObservableRangeCollection<int>([10, 20]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var averageQuery = sourceQuery.ObserveAverage())
            {
                Assert.AreEqual(15, averageQuery.Evaluation.Result);
                CollectionAssert.AreEqual(new int[] { 20 }, RecordEvaluations(averageQuery, () => source.Add(30)));
                Assert.AreEqual(20, averageQuery.Evaluation.Result);
                CollectionAssert.AreEqual(new int[] { 15 }, RecordEvaluations(averageQuery, () => source.Remove(30)));
                Assert.AreEqual(15, averageQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void AveragePublishesOnceWhenAnElementWhichDoesNotChangeTheSumIsAdded()
    {
        var source = new ObservableRangeCollection<int>([10, 20]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var averageQuery = sourceQuery.ObserveAverage())
            {
                Assert.AreEqual(15, averageQuery.Evaluation.Result);
                CollectionAssert.AreEqual(new int[] { 10 }, RecordEvaluations(averageQuery, () => source.Add(0)));
                Assert.AreEqual(10, averageQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void AveragePublishesOnceWhenAnElementChangesValue()
    {
        var source = new ObservableRangeCollection<int>([10, 20]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var averageQuery = sourceQuery.ObserveAverage())
            {
                Assert.AreEqual(15, averageQuery.Evaluation.Result);
                CollectionAssert.AreEqual(new int[] { 30 }, RecordEvaluations(averageQuery, () => source[0] = 40));
                Assert.AreEqual(30, averageQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
