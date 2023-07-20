namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionUsingSynchronizationContext
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new RangeObservableCollection<int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using var synchronizationContext = new AsyncSynchronizationContext();
            using (var usingSynchronizationContextQuery = sourceQuery.ObserveUsingSynchronizationContext(synchronizationContext))
            {
                source.Add(1);
                Assert.AreEqual("1", string.Join(",", usingSynchronizationContextQuery));
                source.AddRange(Enumerable.Range(2, 2));
                Assert.AreEqual("1,2,3", string.Join(",", usingSynchronizationContextQuery));
                source.InsertRange(2, Enumerable.Range(4, 2));
                Assert.AreEqual("1,2,4,5,3", string.Join(",", usingSynchronizationContextQuery));
                source.ReplaceRange(2, 3, Enumerable.Range(3, 2));
                Assert.AreEqual("1,2,3,4", string.Join(",", usingSynchronizationContextQuery));
                source.ReplaceRange(2, 2, Enumerable.Range(3, 3));
                Assert.AreEqual("1,2,3,4,5", string.Join(",", usingSynchronizationContextQuery));
                source.RemoveRange(3, 2);
                Assert.AreEqual("1,2,3", string.Join(",", usingSynchronizationContextQuery));
                source.Reset(Enumerable.Range(1, 5));
                Assert.AreEqual("1,2,3,4,5", string.Join(",", usingSynchronizationContextQuery));
                source.MoveRange(3, 0, 2);
                Assert.AreEqual("4,5,1,2,3", string.Join(",", usingSynchronizationContextQuery));
                Assert.AreEqual(5, usingSynchronizationContextQuery.Count);
                Assert.AreEqual(1, usingSynchronizationContextQuery[2]);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
