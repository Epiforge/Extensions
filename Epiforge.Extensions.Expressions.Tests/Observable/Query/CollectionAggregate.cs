namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionAggregate
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableCollection<double>() { 1, 2 };
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            double seedFactory() => 0;
            double func(double accumulate, double element) => accumulate + element / source.Count;
            double resultSelector(double accumulate) => accumulate;
            using (var aggregateQuery = sourceQuery.ObserveAggregate(seedFactory, func, resultSelector))
            {
                Assert.IsNull(aggregateQuery.Evaluation.Fault);
                Assert.AreEqual(1.5, aggregateQuery.Evaluation.Result);
                source.Add(3);
                Assert.AreEqual(2, aggregateQuery.Evaluation.Result);
                source.RemoveAt(0);
                Assert.AreEqual(2.5, aggregateQuery.Evaluation.Result);
                Assert.AreEqual(1, sourceQuery.CachedObservableQueries);
                using var aggregateQuery2 = sourceQuery.ObserveAggregate(seedFactory, func, resultSelector);
                Assert.AreSame(aggregateQuery, aggregateQuery2);
                Assert.AreEqual(1, sourceQuery.CachedObservableQueries);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
