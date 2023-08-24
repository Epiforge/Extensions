namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionAverage
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableRangeCollection<decimal>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var averageQuery = sourceQuery.ObserveAverage())
            {
                Assert.IsNotNull(averageQuery.Evaluation.Fault);
                Assert.AreEqual(0, averageQuery.Evaluation.Result);
                source.Add(1m);
                Assert.IsNull(averageQuery.Evaluation.Fault);
                Assert.AreEqual(1m, averageQuery.Evaluation.Result);
                source.AddRange(Enumerable.Range(2, 3).Select(Convert.ToDecimal));
                Assert.AreEqual(2.5m, averageQuery.Evaluation.Result);
                source.RemoveRange(0, 2);
                Assert.AreEqual(3.5m, averageQuery.Evaluation.Result);
                source.RemoveAt(0);
                Assert.AreEqual(4m, averageQuery.Evaluation.Result);
                source.RemoveAt(0);
                Assert.IsNotNull(averageQuery.Evaluation.Fault);
                Assert.AreEqual(0m, averageQuery.Evaluation.Result);
                source.Reset(Enumerable.Range(2, 3).Select(Convert.ToDecimal));
                Assert.IsNull(averageQuery.Evaluation.Fault);
                Assert.AreEqual(3m, averageQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
