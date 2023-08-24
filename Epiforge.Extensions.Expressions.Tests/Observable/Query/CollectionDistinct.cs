namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionDistinct
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10).SelectMany(i => new int[] { i, i, i, i, i }));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var distinctQuery = sourceQuery.ObserveDistinct())
            {
                Assert.IsNull(distinctQuery.OperationFault);
                Assert.AreEqual(10, distinctQuery.Count);
                source.RemoveAt(0);
                Assert.AreEqual(10, distinctQuery.Count);
                source.RemoveRange(0, 4);
                Assert.AreEqual(9, distinctQuery.Count);
                source.Add(10);
                Assert.AreEqual(10, distinctQuery.Count);
                source.AddRange(new int[] { 10, 10, 10, 10 });
                Assert.AreEqual(10, distinctQuery.Count);
                source.Reset(Enumerable.Range(0, 5).SelectMany(i => new int[] { i, i }));
                Assert.AreEqual(5, distinctQuery.Count);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
