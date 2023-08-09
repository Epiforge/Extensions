namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryConcurrently
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableDictionary<int, int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var concurrentlyQuery = sourceQuery.ObserveConcurrently())
            {
                Assert.IsNull(concurrentlyQuery.OperationFault);
                Assert.AreEqual(0, concurrentlyQuery.Count);
                source.Add(1, 1);
                Assert.AreEqual(1, concurrentlyQuery.Count);
                source[1] = 2;
                Assert.AreEqual(2, concurrentlyQuery[1]);
                source.Remove(1);
                Assert.AreEqual(0, concurrentlyQuery.Count);
                source.Reset(new Dictionary<int, int> { { 1, 1 }, { 2, 2 } });
                Assert.AreEqual(2, concurrentlyQuery.Count);
                Assert.IsTrue(concurrentlyQuery.ContainsKey(2));
                Assert.IsTrue(concurrentlyQuery.Keys.SequenceEqual(new[] { 1, 2 }));
                Assert.IsTrue(concurrentlyQuery.Values.SequenceEqual(new[] { 1, 2 }));
                Assert.IsTrue(concurrentlyQuery.TryGetValue(2, out var value) && value == 2);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
