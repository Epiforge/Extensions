namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryValueForOrDefault
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableDictionary<int, int>(Enumerable.Range(0, 10).ToDictionary(i => i));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var valueForOrDefaultQuery = sourceQuery.ObserveValueForOrDefault(9))
            {
                Assert.IsNull(valueForOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(9, valueForOrDefaultQuery.Evaluation.Result);
                source.Remove(9);
                Assert.IsNull(valueForOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, valueForOrDefaultQuery.Evaluation.Result);
                source.Add(9, 30);
                Assert.IsNull(valueForOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(30, valueForOrDefaultQuery.Evaluation.Result);
                source[9] = 15;
                Assert.IsNull(valueForOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(15, valueForOrDefaultQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
