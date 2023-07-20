namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionUsingSyncRoot
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new RangeObservableCollection<int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            var syncRoot = new object();
            using (var usingSyncRootQuery = sourceQuery.ObserveUsingSyncRoot(syncRoot))
            {
                source.Add(1);
                lock (syncRoot)
                    Assert.AreEqual("1", string.Join(",", usingSyncRootQuery));
                source.AddRange(Enumerable.Range(2, 2));
                lock (syncRoot)
                    Assert.AreEqual("1,2,3", string.Join(",", usingSyncRootQuery));
                source.InsertRange(2, Enumerable.Range(4, 2));
                lock (syncRoot)
                    Assert.AreEqual("1,2,4,5,3", string.Join(",", usingSyncRootQuery));
                source.ReplaceRange(2, 3, Enumerable.Range(3, 2));
                lock (syncRoot)
                    Assert.AreEqual("1,2,3,4", string.Join(",", usingSyncRootQuery));
                source.ReplaceRange(2, 2, Enumerable.Range(3, 3));
                lock (syncRoot)
                    Assert.AreEqual("1,2,3,4,5", string.Join(",", usingSyncRootQuery));
                source.RemoveRange(3, 2);
                lock (syncRoot)
                    Assert.AreEqual("1,2,3", string.Join(",", usingSyncRootQuery));
                source.Reset(Enumerable.Range(1, 5));
                lock (syncRoot)
                    Assert.AreEqual("1,2,3,4,5", string.Join(",", usingSyncRootQuery));
                source.MoveRange(3, 0, 2);
                lock (syncRoot)
                {
                    Assert.AreEqual("4,5,1,2,3", string.Join(",", usingSyncRootQuery));
                    Assert.AreEqual(5, usingSyncRootQuery.Count);
                    Assert.AreEqual(1, usingSyncRootQuery[2]);
                }
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
