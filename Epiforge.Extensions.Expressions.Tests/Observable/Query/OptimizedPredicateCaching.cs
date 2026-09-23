namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Covers operators whose predicate the optimizer rewrites, which must leave their source's cache as they found it once disposed, and must yield a query which follows its source when observed again
/// </summary>
/// <remarks>
/// A query cached under one key and removed under another stays cached after its last observation is disposed, and the next identical observation is handed the disposed query back. Every predicate here is one the optimizer rewrites, which each test checks first so that it cannot pass by the optimizer leaving its predicate alone. The collection operators are the controls: they cache and remove under the same key
/// </remarks>
[TestClass]
public class OptimizedPredicateCaching
{
    static readonly Expression<Func<int, bool>> always = n => true | n > 0;
    static readonly Expression<Func<string, int, bool>> alwaysForValue = (key, value) => true | value > 0;
    static readonly Expression<Func<int, bool>> never = n => false & n > 0;
    static readonly Expression<Func<string, int, bool>> neverForValue = (key, value) => false & value > 0;

    static void AssertRewritten(Expression predicate) =>
        Assert.AreNotEqual(predicate.ToString(), ExpressionOptimizer.tryVisit(predicate).ToString(), $"the optimizer left {predicate} alone, so this test cannot say anything");

    [TestMethod]
    public void CollectionAllLeavesNothingCachedOnceDisposed()
    {
        AssertRewritten(never);
        var source = new ObservableRangeCollection<int>([1]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using (var all = sourceQuery.ObserveAll(never))
            Assert.IsFalse(all.Evaluation.Result);
        Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
    }

    [TestMethod]
    public void CollectionAllObservedAgainFollowsItsSource()
    {
        AssertRewritten(never);
        var source = new ObservableRangeCollection<int>([1]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        sourceQuery.ObserveAll(never).Dispose();
        using var all = sourceQuery.ObserveAll(never);
        Assert.IsFalse(all.Evaluation.Result);
        source.Clear();
        Assert.IsTrue(all.Evaluation.Result, "an observation made after an identical one was disposed did not follow its source");
    }

    [TestMethod]
    public void CollectionAnyLeavesNothingCachedOnceDisposed()
    {
        AssertRewritten(always);
        var source = new ObservableRangeCollection<int>([1]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using (var any = sourceQuery.ObserveAny(always))
            Assert.IsTrue(any.Evaluation.Result);
        Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
    }

    [TestMethod]
    public void CollectionAnyObservedAgainFollowsItsSource()
    {
        AssertRewritten(always);
        var source = new ObservableRangeCollection<int>([1]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        sourceQuery.ObserveAny(always).Dispose();
        using var any = sourceQuery.ObserveAny(always);
        Assert.IsTrue(any.Evaluation.Result);
        source.Clear();
        Assert.IsFalse(any.Evaluation.Result, "an observation made after an identical one was disposed did not follow its source");
    }

    [TestMethod]
    public void DictionaryAllLeavesNothingCachedOnceDisposed()
    {
        AssertRewritten(neverForValue);
        var source = new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1 });
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using (var all = sourceQuery.ObserveAll(neverForValue))
            Assert.IsFalse(all.Evaluation.Result);
        Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
    }

    [TestMethod]
    public void DictionaryAllObservedAgainFollowsItsSource()
    {
        AssertRewritten(neverForValue);
        var source = new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1 });
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        sourceQuery.ObserveAll(neverForValue).Dispose();
        using var all = sourceQuery.ObserveAll(neverForValue);
        Assert.IsFalse(all.Evaluation.Result);
        source.Remove("a");
        Assert.IsTrue(all.Evaluation.Result, "an observation made after an identical one was disposed did not follow its source");
    }

    [TestMethod]
    public void DictionaryAnyLeavesNothingCachedOnceDisposed()
    {
        AssertRewritten(alwaysForValue);
        var source = new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1 });
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using (var any = sourceQuery.ObserveAny(alwaysForValue))
            Assert.IsTrue(any.Evaluation.Result);
        Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
    }

    [TestMethod]
    public void DictionaryAnyObservedAgainFollowsItsSource()
    {
        AssertRewritten(alwaysForValue);
        var source = new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1 });
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        sourceQuery.ObserveAny(alwaysForValue).Dispose();
        using var any = sourceQuery.ObserveAny(alwaysForValue);
        Assert.IsTrue(any.Evaluation.Result);
        source.Remove("a");
        Assert.IsFalse(any.Evaluation.Result, "an observation made after an identical one was disposed did not follow its source");
    }
}
