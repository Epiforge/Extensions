namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryAggregate
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableDictionary<string, double>() { { "one", 1 }, { "two", 2 } };
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            double seedFactory() => 0;
            double func(double accumulate, string key, double value) => accumulate + value / source.Count;
            double resultSelector(double accumulate) => accumulate;
            using (var aggregateQuery = sourceQuery.ObserveAggregate(seedFactory, func, resultSelector))
            {
                Assert.IsNull(aggregateQuery.Evaluation.Fault);
                Assert.AreEqual(1.5, aggregateQuery.Evaluation.Result);
                source.Add("three", 3);
                Assert.AreEqual(2, aggregateQuery.Evaluation.Result);
                source.Remove("one");
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
