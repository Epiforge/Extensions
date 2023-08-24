namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionFirst
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var firstQuery = sourceQuery.ObserveFirst())
            {
                Assert.IsNull(firstQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstQuery.Evaluation.Result);
                source.Remove(0);
                Assert.AreEqual(1, firstQuery.Evaluation.Result);
                source.Clear();
                Assert.IsInstanceOfType<IndexOutOfRangeException>(firstQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstQuery.Evaluation.Result);
                source.Add(30);
                Assert.IsNull(firstQuery.Evaluation.Fault);
                Assert.AreEqual(30, firstQuery.Evaluation.Result);
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
            using (var firstQuery = sourceQuery.ObserveFirst(i => i % 3 == 0))
            {
                Assert.IsNull(firstQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstQuery.Evaluation.Result);
                source.Remove(0);
                Assert.AreEqual(3, firstQuery.Evaluation.Result);
                source.RemoveAll(i => i % 3 == 0);
                Assert.IsNotNull(firstQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstQuery.Evaluation.Result);
                source.Add(30);
                Assert.IsNull(firstQuery.Evaluation.Fault);
                Assert.AreEqual(30, firstQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
