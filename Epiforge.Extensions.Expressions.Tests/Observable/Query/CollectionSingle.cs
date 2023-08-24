namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionSingle
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(1, 1));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var singleQuery = sourceQuery.ObserveSingle())
            {
                Assert.IsNull(singleQuery.Evaluation.Fault);
                Assert.AreEqual(1, singleQuery.Evaluation.Result);
                source.Add(2);
                Assert.IsNotNull(singleQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleQuery.Evaluation.Result);
                source.RemoveAt(0);
                Assert.IsNull(singleQuery.Evaluation.Fault);
                Assert.AreEqual(2, singleQuery.Evaluation.Result);
                source.Clear();
                Assert.IsNotNull(singleQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleQuery.Evaluation.Result);
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
        var source = new ObservableRangeCollection<int>(Enumerable.Range(1, 3));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var singleQuery = sourceQuery.ObserveSingle(i => i % 3 == 0))
            {
                Assert.IsNull(singleQuery.Evaluation.Fault);
                Assert.AreEqual(3, singleQuery.Evaluation.Result);
                source.RemoveAt(2);
                Assert.IsNotNull(singleQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleQuery.Evaluation.Result);
                source.Add(3);
                Assert.IsNull(singleQuery.Evaluation.Fault);
                Assert.AreEqual(3, singleQuery.Evaluation.Result);
                source.Add(5);
                Assert.IsNull(singleQuery.Evaluation.Fault);
                Assert.AreEqual(3, singleQuery.Evaluation.Result);
                source.Add(6);
                Assert.IsNotNull(singleQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleQuery.Evaluation.Result);
                source.Clear();
                Assert.IsNotNull(singleQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleQuery.Evaluation.Result);
                source.Add(3);
                source.Add(6);
                Assert.IsNotNull(singleQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleQuery.Evaluation.Result);
                source.RemoveAt(0);
                Assert.IsNull(singleQuery.Evaluation.Fault);
                Assert.AreEqual(6, singleQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
