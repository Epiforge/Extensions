namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Covers the caches through which a query shares one child among identical observations of it: that the second observation shares what the first made, that the child survives until its last observation is disposed, and that nothing of it stays cached afterward
/// </summary>
/// <remarks>
/// Every operator a collection, dictionary or scalar query caches is observed here, because each keeps its own cache and each is released by its own path, and so is every operator which builds what it caches by, such as the reversal of a comparer. The concurrency test observes and disposes many kinds of child on one parent from many threads at once, and the retention test asks whether a long-lived observer keeps anything of a chain once every query in it has been disposed
/// </remarks>
[TestClass]
public class ChildQueryCaches
{
    static readonly Func<int, int> accumulate = accumulated => accumulated;
    static readonly Func<int, int, int> addElement = (accumulated, element) => accumulated + element;
    static readonly Func<int, string, int, int> addValue = (accumulated, key, value) => accumulated + value;
    static readonly Func<int, int> last = count => count - 1;
    static readonly CollectionSynchronizationCallback runInline = (collection, context, accessMethod, writeAccess) => accessMethod();
    static readonly Func<int> seed = () => 0;
    static readonly SynchronizationContext synchronizationContext = new();
    static readonly object synchronizationObject = new();

    static void AssertSharedAndReleased(IObservableQuery parent, Func<IDisposable> observe)
    {
        Assert.AreEqual(0, parent.CachedObservableQueries);
        var first = observe();
        var cached = parent.CachedObservableQueries;
        Assert.AreNotEqual(0, cached);
        var second = observe();
        Assert.AreEqual(cached, parent.CachedObservableQueries);
        first.Dispose();
        Assert.AreEqual(cached, parent.CachedObservableQueries);
        second.Dispose();
        Assert.AreEqual(0, parent.CachedObservableQueries);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference BuildAndDisposeChain(CollectionObserver collectionObserver)
    {
        var person = new TestPerson("Ada");
        var source = collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<TestPerson>([person]));
        var associated = source.ObserveCast<object>();
        var named = source.ObserveWhere(p => p.Name!.Length > 0);
        var names = named.ObserveSelect(p => p.Name);
        var flattened = names.ObserveSelectMany(name => new object[] { name! });
        var all = associated.ObserveConcat(flattened);
        Assert.AreEqual(2, all.Count);
        all.Dispose();
        flattened.Dispose();
        names.Dispose();
        named.Dispose();
        associated.Dispose();
        source.Dispose();
        return new(person);
    }

    static void ObserveCollection(Func<IObservableCollectionQuery<int>, IDisposable> observe)
    {
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<int>(Enumerable.Range(1, 5))))
            AssertSharedAndReleased(sourceQuery, () => observe(sourceQuery));
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    static void ObserveDictionary(Func<IObservableDictionaryQuery<string, int>, IDisposable> observe)
    {
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 })))
            AssertSharedAndReleased(sourceQuery, () => observe(sourceQuery));
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void CollectionAggregate() =>
        ObserveCollection(source => source.ObserveAggregate(seed, addElement, accumulate));

    [TestMethod]
    public void CollectionAll() =>
        ObserveCollection(source => source.ObserveAll(n => n > 0));

    [TestMethod]
    public void CollectionAny() =>
        ObserveCollection(source => source.ObserveAny());

    [TestMethod]
    public void CollectionAnyWithPredicate() =>
        ObserveCollection(source => source.ObserveAny(n => n > 3));

    [TestMethod]
    public void CollectionAppend() =>
        ObserveCollection(source => source.ObserveAppend(6));

    [TestMethod]
    public void CollectionAverage() =>
        ObserveCollection(source => source.ObserveAverage());

    [TestMethod]
    public void CollectionConcat()
    {
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var second = collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<int>([6, 7])))
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<int>(Enumerable.Range(1, 5))))
            AssertSharedAndReleased(sourceQuery, () => sourceQuery.ObserveConcat(second));
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }

    [TestMethod]
    public void CollectionCount() =>
        ObserveCollection(source => source.ObserveCount());

    [TestMethod]
    public void CollectionElementAtForCount() =>
        ObserveCollection(source => source.ObserveElementAt(last));

    [TestMethod]
    public void CollectionFirst() =>
        ObserveCollection(source => source.ObserveFirst());

    [TestMethod]
    public void CollectionGroupBy() =>
        ObserveCollection(source => source.ObserveGroupBy(n => n % 2));

    [TestMethod]
    public void CollectionIndividualChanges() =>
        ObserveCollection(source => source.ObserveIndividualChanges());

    [TestMethod]
    public void CollectionMax() =>
        ObserveCollection(source => source.ObserveMax());

    [TestMethod]
    public void CollectionOrderBy() =>
        ObserveCollection(source => source.ObserveOrderBy(n => n));

    [TestMethod]
    public void CollectionPrepend() =>
        ObserveCollection(source => source.ObservePrepend(0));

    [TestMethod]
    public void CollectionSelect() =>
        ObserveCollection(source => source.ObserveSelect(n => n * 2));

    [TestMethod]
    public void CollectionSelectMany() =>
        ObserveCollection(source => source.ObserveSelectMany(n => new[] { n, n }));

    [TestMethod]
    public void CollectionSlice() =>
        ObserveCollection(source => source.ObserveSlice(1..3));

    [TestMethod]
    public void CollectionSum() =>
        ObserveCollection(source => source.ObserveSum());

    [TestMethod]
    public void CollectionToDictionary() =>
        ObserveCollection(source => source.ObserveToDictionary(n => n));

    [TestMethod]
    public void CollectionToLookup() =>
        ObserveCollection(source => source.ObserveToLookup(n => n % 2));

    [TestMethod]
    public void CollectionUsingSynchronizationCallback() =>
        ObserveCollection(source => source.ObserveUsingSynchronizationCallback(synchronizationObject, runInline));

    [TestMethod]
    public void CollectionUsingSynchronizationCallbackEventually() =>
        ObserveCollection(source => source.ObserveUsingSynchronizationCallbackEventually(synchronizationObject, runInline));

    [TestMethod]
    public void CollectionUsingSynchronizationContext() =>
        ObserveCollection(source => source.ObserveUsingSynchronizationContext(synchronizationContext));

    [TestMethod]
    public void CollectionUsingSynchronizationContextEventually() =>
        ObserveCollection(source => source.ObserveUsingSynchronizationContextEventually(synchronizationContext));

    [TestMethod]
    public void CollectionUsingSyncRoot() =>
        ObserveCollection(source => source.ObserveUsingSyncRoot(synchronizationObject));

    [TestMethod]
    public void CollectionUsingSyncRootEventually() =>
        ObserveCollection(source => source.ObserveUsingSyncRootEventually(synchronizationObject));

    [TestMethod]
    public void CollectionWhere() =>
        ObserveCollection(source => source.ObserveWhere(n => n > 2));

    [TestMethod]
    public void ConcurrentObservationsOfOneParent()
    {
        var collectionObserver = CollectionObserverHelpers.Create();
        using var second = collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<int>([6, 7]));
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<int>(Enumerable.Range(1, 5)));
        Func<IDisposable>[] observations =
        [
            () => sourceQuery.ObserveAny(),
            () => sourceQuery.ObserveConcat(second),
            () => sourceQuery.ObserveCount(),
            () => sourceQuery.ObserveFirst(),
            () => sourceQuery.ObserveGroupBy(n => n % 2),
            () => sourceQuery.ObserveOrderBy(n => n),
            () => sourceQuery.ObserveSelect(n => n * 2),
            () => sourceQuery.ObserveSelectMany(n => new[] { n, n }),
            () => sourceQuery.ObserveSlice(1..3),
            () => sourceQuery.ObserveWhere(n => n > 2)
        ];
        Parallel.For(0, 2000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            using var observation = observations[i % observations.Length]();
        });
        Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
    }

    [TestMethod]
    public void DictionaryAggregate() =>
        ObserveDictionary(source => source.ObserveAggregate(seed, addValue, accumulate));

    [TestMethod]
    public void DictionaryAll() =>
        ObserveDictionary(source => source.ObserveAll((key, value) => value > 0));

    [TestMethod]
    public void DictionaryAny() =>
        ObserveDictionary(source => source.ObserveAny());

    [TestMethod]
    public void DictionaryAnyWithPredicate() =>
        ObserveDictionary(source => source.ObserveAny((key, value) => value > 1));

    [TestMethod]
    public void DictionaryConcurrently() =>
        ObserveDictionary(source => source.ObserveConcurrently());

    [TestMethod]
    public void DictionaryCount() =>
        ObserveDictionary(source => source.ObserveCount());

    [TestMethod]
    public void DictionaryFirst() =>
        ObserveDictionary(source => source.ObserveFirst());

    [TestMethod]
    public void DictionaryLast() =>
        ObserveDictionary(source => source.ObserveLast());

    [TestMethod]
    public void DictionaryLastOrDefault() =>
        ObserveDictionary(source => source.ObserveLastOrDefault());

    [TestMethod]
    public void DictionarySelect() =>
        ObserveDictionary(source => source.ObserveSelect((key, value) => key, (key, value) => value * 2));

    [TestMethod]
    public void DictionaryToCollection() =>
        ObserveDictionary(source => source.ObserveToCollection((key, value) => value * 2));

    [TestMethod]
    public void DictionaryUsingSynchronizationContext() =>
        ObserveDictionary(source => source.ObserveUsingSynchronizationContext(synchronizationContext));

    [TestMethod]
    public void DictionaryUsingSynchronizationContextEventually() =>
        ObserveDictionary(source => source.ObserveUsingSynchronizationContextEventually(synchronizationContext));

    [TestMethod]
    public void DictionaryValueFor() =>
        ObserveDictionary(source => source.ObserveValueFor("a"));

    [TestMethod]
    public void DictionaryWhere() =>
        ObserveDictionary(source => source.ObserveWhere((key, value) => value > 1));

    [TestMethod]
    public void DisposedChainIsNotRetainedByItsObserver()
    {
        var collectionObserver = CollectionObserverHelpers.Create();
        var person = BuildAndDisposeChain(collectionObserver);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.IsFalse(person.IsAlive, "an element outlived every query over it while their observer lived on");
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ScalarTransform()
    {
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<int>(Enumerable.Range(1, 5))))
        using (var count = sourceQuery.ObserveCount())
            AssertSharedAndReleased(count, () => count.ObserveTransform(n => n * 2));
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }
}
