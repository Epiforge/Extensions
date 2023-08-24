namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionLastOrDefault
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var lastOrDefaultQuery = sourceQuery.ObserveLastOrDefault())
            {
                Assert.IsNull(lastOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(9, lastOrDefaultQuery.Evaluation.Result);
                source.Remove(9);
                Assert.AreEqual(8, lastOrDefaultQuery.Evaluation.Result);
                source.Clear();
                Assert.IsNull(lastOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, lastOrDefaultQuery.Evaluation.Result);
                source.Add(30);
                Assert.IsNull(lastOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(30, lastOrDefaultQuery.Evaluation.Result);
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
            using (var lastOrDefaultQuery = sourceQuery.ObserveLastOrDefault(i => i % 3 == 0))
            {
                Assert.IsNull(lastOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(9, lastOrDefaultQuery.Evaluation.Result);
                source.Remove(9);
                Assert.AreEqual(6, lastOrDefaultQuery.Evaluation.Result);
                source.RemoveAll(i => i % 3 == 0);
                Assert.IsNull(lastOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, lastOrDefaultQuery.Evaluation.Result);
                source.Add(30);
                Assert.IsNull(lastOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(30, lastOrDefaultQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
