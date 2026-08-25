namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class QueryLifetime
{
    [TestMethod]
    public void IdenticalCollectionQueriesAreDistinctScopesOverOneCachedQuery()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var first = sourceQuery.ObserveWhere(p => p.Name!.Length == 4);
        var cachedAfterFirstObservation = sourceQuery.CachedObservableQueries;
        using var second = sourceQuery.ObserveWhere(p => p.Name!.Length == 4);
        Assert.AreNotSame(first, second);
        Assert.AreEqual(cachedAfterFirstObservation, sourceQuery.CachedObservableQueries);
        Assert.IsTrue(first.SequenceEqual(second));
    }

    [TestMethod]
    public void DisposingACollectionQueryTwiceDoesNotReleaseAnother()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var first = sourceQuery.ObserveWhere(p => p.Name!.Length == 4);
        var second = sourceQuery.ObserveWhere(p => p.Name!.Length == 4);
        Assert.AreEqual(2, second.Count);
        first.Dispose();
        first.Dispose();
        Assert.IsTrue(first.IsDisposed);
        Assert.AreNotEqual(0, sourceQuery.CachedObservableQueries);
        source[0].Name = "Johnny";
        Assert.AreEqual(1, second.Count);
        second.Dispose();
        Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
    }

    [TestMethod]
    public void CollectionQueryDisposalNotificationIsScopedToTheInstanceDisposed()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var first = sourceQuery.ObserveWhere(p => p.Name!.Length == 4);
        using var second = sourceQuery.ObserveWhere(p => p.Name!.Length == 4);
        var firstDisposals = 0;
        var secondDisposals = 0;
        first.Disposed += (_, _) => ++firstDisposals;
        second.Disposed += (_, _) => ++secondDisposals;
        first.Dispose();
        first.Dispose();
        Assert.AreEqual(1, firstDisposals);
        Assert.AreEqual(0, secondDisposals);
    }

    [TestMethod]
    public void CollectionChangeNotificationsReachEveryScope()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var first = sourceQuery.ObserveWhere(p => p.Name!.Length == 4);
        using var second = sourceQuery.ObserveWhere(p => p.Name!.Length == 4);
        var firstChanges = 0;
        var secondChanges = 0;
        first.CollectionChanged += (_, _) => ++firstChanges;
        second.CollectionChanged += (_, _) => ++secondChanges;
        source[0].Name = "Johnny";
        Assert.AreNotEqual(0, firstChanges);
        Assert.AreEqual(firstChanges, secondChanges);
    }

    [TestMethod]
    public void IdenticalDictionaryQueriesAreDistinctScopesOverOneCachedQuery()
    {
        var source = TestPerson.CreatePeopleDictionary();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var first = sourceQuery.ObserveWhere((key, value) => value.Name!.Length == 4);
        var cachedAfterFirstObservation = sourceQuery.CachedObservableQueries;
        using var second = sourceQuery.ObserveWhere((key, value) => value.Name!.Length == 4);
        Assert.AreNotSame(first, second);
        Assert.AreEqual(cachedAfterFirstObservation, sourceQuery.CachedObservableQueries);
        Assert.AreEqual(first.Count, second.Count);
    }

    [TestMethod]
    public void DisposingADictionaryQueryTwiceDoesNotReleaseAnother()
    {
        var source = TestPerson.CreatePeopleDictionary();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        var first = sourceQuery.ObserveWhere((key, value) => value.Name!.Length == 4);
        var second = sourceQuery.ObserveWhere((key, value) => value.Name!.Length == 4);
        Assert.AreEqual(2, second.Count);
        first.Dispose();
        first.Dispose();
        Assert.IsTrue(first.IsDisposed);
        Assert.AreNotEqual(0, sourceQuery.CachedObservableQueries);
        second.Dispose();
        Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
    }
}
