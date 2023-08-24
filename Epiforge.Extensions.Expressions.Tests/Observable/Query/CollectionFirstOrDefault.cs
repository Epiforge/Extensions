namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionFirstOrDefault
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var firstOrDefaultQuery = sourceQuery.ObserveFirstOrDefault())
            {
                Assert.IsNull(firstOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstOrDefaultQuery.Evaluation.Result);
                source.Remove(0);
                Assert.AreEqual(1, firstOrDefaultQuery.Evaluation.Result);
                source.Clear();
                Assert.IsNull(firstOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstOrDefaultQuery.Evaluation.Result);
                source.Add(30);
                Assert.IsNull(firstOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(30, firstOrDefaultQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var firstOrDefaultQuery = sourceQuery.ObserveFirstOrDefault(i => i % 3 == 0))
            {
                Assert.IsNull(firstOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstOrDefaultQuery.Evaluation.Result);
                source.Remove(0);
                Assert.AreEqual(3, firstOrDefaultQuery.Evaluation.Result);
                source.RemoveAll(i => i % 3 == 0);
                Assert.IsNull(firstOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstOrDefaultQuery.Evaluation.Result);
                source.Add(30);
                Assert.IsNull(firstOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(30, firstOrDefaultQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
