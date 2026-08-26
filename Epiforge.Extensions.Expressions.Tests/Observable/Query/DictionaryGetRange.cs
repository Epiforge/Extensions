namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryGetRange
{
    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void NonRangeDictionarySource()
    {
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(new Dictionary<string, string> { { "key", "value" } });
        sourceQuery.GetRange(new[] { "key" });
    }

    [TestMethod]
    public void RangeDictionarySource()
    {
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(new ObservableDictionary<string, string> { { "key", "value" } }))
        {
            var range = sourceQuery.GetRange(new[] { "key" });
            Assert.AreEqual(1, range.Count);
            Assert.AreEqual(new KeyValuePair<string, string>("key", "value"), range[0]);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }

    [TestMethod]
    public void ReadOnlyRangeDictionarySource()
    {
        var source = new Epiforge.Extensions.Collections.Generic.ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } });
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            var range = sourceQuery.GetRange(new[] { "key" });
            Assert.AreEqual(1, range.Count);
            Assert.AreEqual(new KeyValuePair<string, string>("key", "value"), range[0]);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }
}
