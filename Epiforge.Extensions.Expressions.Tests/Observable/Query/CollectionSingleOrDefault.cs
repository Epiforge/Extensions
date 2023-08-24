namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionSingleOrDefault
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(1, 1));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var singleOrDefaultQuery = sourceQuery.ObserveSingleOrDefault())
            {
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(1, singleOrDefaultQuery.Evaluation.Result);
                source.Add(2);
                Assert.IsNotNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleOrDefaultQuery.Evaluation.Result);
                source.RemoveAt(0);
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(2, singleOrDefaultQuery.Evaluation.Result);
                source.Clear();
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleOrDefaultQuery.Evaluation.Result);
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
            using (var singleOrDefaultQuery = sourceQuery.ObserveSingleOrDefault(i => i % 3 == 0))
            {
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(3, singleOrDefaultQuery.Evaluation.Result);
                source.RemoveAt(2);
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleOrDefaultQuery.Evaluation.Result);
                source.Add(3);
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(3, singleOrDefaultQuery.Evaluation.Result);
                source.Add(6);
                Assert.IsNotNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleOrDefaultQuery.Evaluation.Result);
                source.Clear();
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleOrDefaultQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
