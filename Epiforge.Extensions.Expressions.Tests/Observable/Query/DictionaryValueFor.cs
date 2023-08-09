namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryValueFor
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableDictionary<int, int>(Enumerable.Range(0, 10).ToDictionary(i => i));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var valueForQuery = sourceQuery.ObserveValueFor(9))
            {
                Assert.IsNull(valueForQuery.Evaluation.Fault);
                Assert.AreEqual(9, valueForQuery.Evaluation.Result);
                source.Remove(9);
                Assert.IsNotNull(valueForQuery.Evaluation.Fault);
                Assert.AreEqual(0, valueForQuery.Evaluation.Result);
                source.Add(9, 30);
                Assert.IsNull(valueForQuery.Evaluation.Fault);
                Assert.AreEqual(30, valueForQuery.Evaluation.Result);
                source[9] = 15;
                Assert.IsNull(valueForQuery.Evaluation.Fault);
                Assert.AreEqual(15, valueForQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
