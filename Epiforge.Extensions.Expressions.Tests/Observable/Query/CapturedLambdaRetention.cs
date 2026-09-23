namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Covers what the queries keep of a lambda once every query observing it has been disposed, which must be nothing, since a lambda holds whatever it captured
/// </summary>
/// <remarks>
/// Each test builds its observer, its source and its query in a method which is not inlined, so that nothing it made is still referenced from the test's frame, disposes all of them, and then asks whether the object its lambda captured survives a full collection. Anything a query derives from a lambda and keeps in a static field keeps the capture alive with it
/// </remarks>
[TestClass]
public class CapturedLambdaRetention
{
    sealed class Captured
    {
        public int Value { get; set; }
    }

    static void AssertCollected(WeakReference captured)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.IsFalse(captured.IsAlive, "what a lambda captured outlived every query observing it");
    }

    [TestMethod]
    public void CollectionAll() =>
        AssertCollected(ObserveCollection((source, captured) => source.ObserveAll(n => n > captured.Value)));

    [TestMethod]
    public void CollectionGroupBy() =>
        AssertCollected(ObserveCollection((source, captured) => source.ObserveGroupBy(n => n % 2 + captured.Value)));

    [TestMethod]
    public void CollectionMaxBy() =>
        AssertCollected(ObserveCollection((source, captured) => source.ObserveMaxBy(n => n + captured.Value)));

    [TestMethod]
    public void CollectionOrderBy() =>
        AssertCollected(ObserveCollection((source, captured) => source.ObserveOrderBy(n => n + captured.Value)));

    [TestMethod]
    public void CollectionToDictionary() =>
        AssertCollected(ObserveCollection((source, captured) => source.ObserveToDictionary(n => n + captured.Value)));

    [TestMethod]
    public void CollectionToLookup() =>
        AssertCollected(ObserveCollection((source, captured) => source.ObserveToLookup(n => n % 2 + captured.Value)));

    [TestMethod]
    public void DictionarySelect() =>
        AssertCollected(ObserveDictionary((source, captured) => source.ObserveSelect((key, value) => key, (key, value) => value + captured.Value)));

    [TestMethod]
    public void DictionaryWhere() =>
        AssertCollected(ObserveDictionary((source, captured) => source.ObserveWhere((key, value) => value > captured.Value)));

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference ObserveCollection(Func<IObservableCollectionQuery<int>, Captured, IDisposable> observe)
    {
        var captured = new Captured();
        var collectionObserver = new CollectionObserver(new ExpressionObserver());
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))))
            observe(sourceQuery, captured).Dispose();
        return new(captured);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference ObserveDictionary(Func<IObservableDictionaryQuery<string, int>, Captured, IDisposable> observe)
    {
        var captured = new Captured();
        var collectionObserver = new CollectionObserver(new ExpressionObserver());
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 })))
            observe(sourceQuery, captured).Dispose();
        return new(captured);
    }
}
