namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionLast
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var lastQuery = sourceQuery.ObserveLast())
            {
                Assert.IsNull(lastQuery.Evaluation.Fault);
                Assert.AreEqual(9, lastQuery.Evaluation.Result);
                source.Remove(9);
                Assert.AreEqual(8, lastQuery.Evaluation.Result);
                source.Clear();
                Assert.IsInstanceOfType<IndexOutOfRangeException>(lastQuery.Evaluation.Fault);
                Assert.AreEqual(0, lastQuery.Evaluation.Result);
                source.Add(30);
                Assert.IsNull(lastQuery.Evaluation.Fault);
                Assert.AreEqual(30, lastQuery.Evaluation.Result);
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
            using (var lastQuery = sourceQuery.ObserveLast(i => i % 3 == 0))
            {
                Assert.IsNull(lastQuery.Evaluation.Fault);
                Assert.AreEqual(9, lastQuery.Evaluation.Result);
                source.Remove(9);
                Assert.AreEqual(6, lastQuery.Evaluation.Result);
                source.RemoveAll(i => i % 3 == 0);
                Assert.IsNotNull(lastQuery.Evaluation.Fault);
                Assert.AreEqual(0, lastQuery.Evaluation.Result);
                source.Add(30);
                Assert.IsNull(lastQuery.Evaluation.Fault);
                Assert.AreEqual(30, lastQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
