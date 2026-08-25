namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class ScalarQueryLifetime
{
    [TestMethod]
    public void IdenticalScalarQueriesAreDistinctHandlesOverOneCachedQuery()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var first = sourceQuery.ObserveCount(p => p.Name!.Length == 4);
        var cachedAfterFirstObservation = sourceQuery.CachedObservableQueries;
        using var second = sourceQuery.ObserveCount(p => p.Name!.Length == 4);
        Assert.AreNotSame(first, second);
        Assert.AreEqual(cachedAfterFirstObservation, sourceQuery.CachedObservableQueries);
        Assert.AreEqual(first.Evaluation.Result, second.Evaluation.Result);
    }

    [TestMethod]
    public void DisposingAScalarQueryTwiceIsIdempotent()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var countQuery = sourceQuery.ObserveCount(p => p.Name!.Length == 4);
        countQuery.Dispose();
        countQuery.Dispose();
        Assert.IsTrue(countQuery.IsDisposed);
        Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
    }

    [TestMethod]
    public void DisposingAScalarQueryTwiceDoesNotReleaseAnother()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var first = sourceQuery.ObserveCount(p => p.Name!.Length == 4);
        var second = sourceQuery.ObserveCount(p => p.Name!.Length == 4);
        Assert.AreEqual(2, second.Evaluation.Result);
        first.Dispose();
        first.Dispose();
        Assert.AreNotEqual(0, sourceQuery.CachedObservableQueries);
        source[0].Name = "Johnny";
        Assert.AreEqual(1, second.Evaluation.Result);
        second.Dispose();
        Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
    }

    [TestMethod]
    public void ScalarDisposalNotificationIsScopedToTheHandleDisposed()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var first = sourceQuery.ObserveCount(p => p.Name!.Length == 4);
        using var second = sourceQuery.ObserveCount(p => p.Name!.Length == 4);
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
    public void IdenticalTransformsAreDistinctHandlesOverOneCachedQuery()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var countQuery = sourceQuery.ObserveCount(p => p.Name!.Length == 4);
        using var first = countQuery.ObserveTransform(count => (long)count);
        var cachedAfterFirstObservation = countQuery.CachedObservableQueries;
        using var second = countQuery.ObserveTransform(count => (long)count);
        Assert.AreNotSame(first, second);
        Assert.AreEqual(cachedAfterFirstObservation, countQuery.CachedObservableQueries);
        Assert.AreEqual(first.Evaluation.Result, second.Evaluation.Result);
    }
}
