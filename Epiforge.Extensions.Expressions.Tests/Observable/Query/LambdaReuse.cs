namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Covers whether an operator observed over one source after another, with the same lambda each time, hands the observer the same expression trees each time, which is what lets the observer optimize, analyze and compile them once rather than once per source
/// </summary>
/// <remarks>
/// The observer's caches of optimized and compiled lambdas match by reference, so a tree an operator builds anew for each call misses them every time, whatever the caller does. The observer here is given an optimizer which records every tree it has not been handed before; the first source may hand it as many as the operator needs, and the two after it must hand it none
/// </remarks>
[TestClass]
public class LambdaReuse
{
    static readonly Expression<Func<int, IComparable>> ascending = n => n;
    static readonly Expression<Func<int, int>> doubled = n => n * 2;
    static readonly Expression<Func<int, int>> identity = n => n;
    static readonly Expression<Func<int, bool>> isPositive = n => n > 0;
    static readonly Expression<Func<string, int, string>> keyOf = (key, value) => key;
    static readonly Expression<Func<int, int>> parity = n => n % 2;
    static readonly Expression<Func<int, IEnumerable<int>>> single = n => new[] { n };
    static readonly Expression<Func<string, int, int>> valueDoubled = (key, value) => value * 2;
    static readonly Expression<Func<string, int, bool>> valueIsPositive = (key, value) => value > 0;

    static void AssertCollectionOperatorReusesItsTrees(Func<IObservableCollectionQuery<int>, IDisposable> observe) =>
        AssertReusesItsTrees(collectionObserver => collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))), observe);

    static void AssertDictionaryOperatorReusesItsTrees(Func<IObservableDictionaryQuery<string, int>, IDisposable> observe) =>
        AssertReusesItsTrees(collectionObserver => collectionObserver.ObserveReadOnlyDictionary(new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 })), observe);

    static void AssertReusesItsTrees<TSourceQuery>(Func<CollectionObserver, TSourceQuery> observeSource, Func<TSourceQuery, IDisposable> observe)
        where TSourceQuery : IDisposable
    {
        var handed = new List<Expression>();
        var seen = new HashSet<Expression>(ReferenceEqualityComparer.Instance);
        var collectionObserver = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions
        {
            Optimizer = expression =>
            {
                if (seen.Add(expression))
                    handed.Add(expression);
                return ExpressionOptimizer.tryVisit(expression);
            }
        }));
        var handedForFirstSource = 0;
        for (var i = 0; i < 3; ++i)
        {
            using (var sourceQuery = observeSource(collectionObserver))
                observe(sourceQuery).Dispose();
            if (i == 0)
                handedForFirstSource = handed.Count;
        }
        Assert.AreEqual(handedForFirstSource, handed.Count, $"observing two more sources handed the optimizer {handed.Count - handedForFirstSource} trees it had not seen: {string.Join("; ", handed.Skip(handedForFirstSource))}");
    }

    [TestMethod]
    public void CollectionAll() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveAll(isPositive));

    [TestMethod]
    public void CollectionAnyWithPredicate() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveAny(isPositive));

    [TestMethod]
    public void CollectionAverage() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveAverage());

    [TestMethod]
    public void CollectionAverageWithSelector() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveAverage(doubled));

    [TestMethod]
    public void CollectionCast() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveCast<object>());

    [TestMethod]
    public void CollectionCountWithPredicate() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveCount(isPositive));

    [TestMethod]
    public void CollectionDistinct() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveDistinct());

    [TestMethod]
    public void CollectionDistinctWithComparer() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveDistinct(EqualityComparer<int>.Default));

    [TestMethod]
    public void CollectionFirstWithPredicate() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveFirst(isPositive));

    [TestMethod]
    public void CollectionGroupBy() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveGroupBy(parity));

    [TestMethod]
    public void CollectionMaxBy() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveMaxBy(parity));

    [TestMethod]
    public void CollectionMaxByWithComparer() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveMaxBy(parity, Comparer<int>.Default));

    [TestMethod]
    public void CollectionMaxWithSelector() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveMax(doubled));

    [TestMethod]
    public void CollectionMinBy() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveMinBy(parity));

    [TestMethod]
    public void CollectionMinByWithComparer() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveMinBy(parity, Comparer<int>.Default));

    [TestMethod]
    public void CollectionOfType() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveOfType<object>());

    [TestMethod]
    public void CollectionOrderBy() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveOrderBy(ascending));

    [TestMethod]
    public void CollectionRank() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveRank(2, ascending, false));

    [TestMethod]
    public void CollectionSelect() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveSelect(doubled));

    [TestMethod]
    public void CollectionSelectMany() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveSelectMany(single));

    [TestMethod]
    public void CollectionSum() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveSum());

    [TestMethod]
    public void CollectionSumWithSelector() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveSum(doubled));

    [TestMethod]
    public void CollectionToDictionary() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveToDictionary(identity));

    [TestMethod]
    public void CollectionToDictionaryWithComparer() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveToDictionary(identity, EqualityComparer<int>.Default));

    [TestMethod]
    public void CollectionToDictionaryWithValueSelector() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveToDictionary(identity, doubled));

    [TestMethod]
    public void CollectionToLookup() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveToLookup(parity));

    [TestMethod]
    public void CollectionWhere() =>
        AssertCollectionOperatorReusesItsTrees(source => source.ObserveWhere(isPositive));

    [TestMethod]
    public void DictionaryAll() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveAll(valueIsPositive));

    [TestMethod]
    public void DictionaryAnyWithPredicate() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveAny(valueIsPositive));

    [TestMethod]
    public void DictionaryAverage() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveAverage());

    [TestMethod]
    public void DictionaryAverageWithSelector() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveAverage(valueDoubled));

    [TestMethod]
    public void DictionaryCast() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveCast<object>());

    [TestMethod]
    public void DictionaryCountWithPredicate() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveCount(valueIsPositive));

    [TestMethod]
    public void DictionaryFirstOrDefaultWithPredicate() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveFirstOrDefault(valueIsPositive));

    [TestMethod]
    public void DictionaryFirstWithPredicate() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveFirst(valueIsPositive));

    [TestMethod]
    public void DictionaryFirstWithPredicateAndComparer() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveFirst(valueIsPositive, StringComparer.Ordinal));

    [TestMethod]
    public void DictionaryLastOrDefaultWithPredicate() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveLastOrDefault(valueIsPositive));

    [TestMethod]
    public void DictionaryLastWithPredicate() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveLast(valueIsPositive));

    [TestMethod]
    public void DictionaryMax() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveMax());

    [TestMethod]
    public void DictionaryMaxWithSelector() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveMax(valueDoubled));

    [TestMethod]
    public void DictionaryMin() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveMin());

    [TestMethod]
    public void DictionaryMinWithSelector() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveMin(valueDoubled));

    [TestMethod]
    public void DictionaryOfType() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveOfType<object>());

    [TestMethod]
    public void DictionarySelect() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveSelect(keyOf, valueDoubled));

    [TestMethod]
    public void DictionarySingleOrDefaultWithPredicate() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveSingleOrDefault(valueIsPositive));

    [TestMethod]
    public void DictionarySingleWithPredicate() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveSingle(valueIsPositive));

    [TestMethod]
    public void DictionarySum() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveSum());

    [TestMethod]
    public void DictionarySumWithSelector() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveSum(valueDoubled));

    [TestMethod]
    public void DictionaryToCollection() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveToCollection());

    [TestMethod]
    public void DictionaryToCollectionWithSelector() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveToCollection(valueDoubled));

    [TestMethod]
    public void DictionaryWhere() =>
        AssertDictionaryOperatorReusesItsTrees(source => source.ObserveWhere(valueIsPositive));
}
