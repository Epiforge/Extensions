namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class QuerySyncRoot
{
    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void DerivedQuerySyncRootThrows()
    {
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(TestPerson.CreatePeopleCollection());
        using var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length == 4);
        _ = whereQuery.SyncRoot;
    }

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void SyncRootThrows()
    {
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<int>());
        _ = sourceQuery.SyncRoot;
    }

    [TestMethod]
    public void UsingSyncRootReturnsTheGivenObject()
    {
        var lockObject = new object();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<int>()))
        using (var usingSyncRootQuery = sourceQuery.ObserveUsingSyncRoot(lockObject))
            Assert.AreSame(lockObject, usingSyncRootQuery.SyncRoot);
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }
}
