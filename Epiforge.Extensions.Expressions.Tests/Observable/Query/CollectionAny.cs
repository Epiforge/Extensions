namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionAny
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(1, 10).Select(i => i * 3));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var anyQuery = sourceQuery.ObserveAny())
            {
                Assert.IsNull(anyQuery.Evaluation.Fault);
                Assert.IsTrue(anyQuery.Evaluation.Result);
                source[0] = 2;
                Assert.IsTrue(anyQuery.Evaluation.Result);
                source.RemoveAt(0);
                Assert.IsTrue(anyQuery.Evaluation.Result);
                --source[0];
                Assert.IsTrue(anyQuery.Evaluation.Result);
                source.Clear();
                Assert.IsFalse(anyQuery.Evaluation.Result);
                source.Add(7);
                Assert.IsTrue(anyQuery.Evaluation.Result);
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
        var source = new ObservableRangeCollection<int>(Enumerable.Range(1, 10).Select(i => i * 3));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var anyQuery = sourceQuery.ObserveAny(i => i % 3 != 0))
            {
                Assert.IsNull(anyQuery.Evaluation.Fault);
                Assert.IsFalse(anyQuery.Evaluation.Result);
                source[0] = 2;
                Assert.IsTrue(anyQuery.Evaluation.Result);
                source.RemoveAt(0);
                Assert.IsFalse(anyQuery.Evaluation.Result);
                --source[0];
                Assert.IsTrue(anyQuery.Evaluation.Result);
                source.Clear();
                Assert.IsFalse(anyQuery.Evaluation.Result);
                source.Add(7);
                Assert.IsTrue(anyQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
