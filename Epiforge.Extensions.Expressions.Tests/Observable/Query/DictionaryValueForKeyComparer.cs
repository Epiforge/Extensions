namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryValueForKeyComparer
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AChangeAnnouncedUnderADifferentlyCasedKeyIsFollowed(bool notFoundIsDefault)
    {
        var source = new ObservableDictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "alpha", 1 },
            { "beta", 2 }
        };
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var valueForQuery = notFoundIsDefault ? sourceQuery.ObserveValueForOrDefault("alpha") : sourceQuery.ObserveValueFor("alpha"))
            {
                Assert.IsNull(valueForQuery.Evaluation.Fault);
                Assert.AreEqual(1, valueForQuery.Evaluation.Result);
                source["ALPHA"] = 10;
                Assert.IsNull(valueForQuery.Evaluation.Fault);
                Assert.AreEqual(10, valueForQuery.Evaluation.Result, "a replacement announced under a differently cased key was not followed");
                source.Remove("Alpha");
                if (notFoundIsDefault)
                    Assert.IsNull(valueForQuery.Evaluation.Fault);
                else
                    Assert.IsInstanceOfType<KeyNotFoundException>(valueForQuery.Evaluation.Fault, "a removal announced under a differently cased key was not followed");
                Assert.AreEqual(0, valueForQuery.Evaluation.Result);
                source.Add("ALPHA", 20);
                Assert.IsNull(valueForQuery.Evaluation.Fault);
                Assert.AreEqual(20, valueForQuery.Evaluation.Result, "an addition announced under a differently cased key was not followed");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
