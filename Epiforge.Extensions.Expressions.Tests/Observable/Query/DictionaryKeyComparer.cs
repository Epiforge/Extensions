namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryKeyComparer
{
    static ObservableDictionary<string, TestPerson> CreateCaseInsensitiveSource() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "alpha", new TestPerson("Charles") },
            { "beta", new TestPerson("Emily") },
            { "gamma", new TestPerson("Ben") }
        };

    static IReadOnlyDictionary<string, TestPerson> CreateCaseInsensitiveSource(string kind)
    {
        var pairs = CreateCaseInsensitiveSource().ToList();
        var byOrdinal = new Dictionary<string, TestPerson>(pairs);
        var byComparison = Comparer<string>.Create((x, y) => string.Compare(x, y, StringComparison.OrdinalIgnoreCase));
        return kind switch
        {
            "dictionary" => new Dictionary<string, TestPerson>(pairs, StringComparer.OrdinalIgnoreCase),
            "concurrent dictionary" => new ConcurrentDictionary<string, TestPerson>(pairs, StringComparer.OrdinalIgnoreCase),
            "immutable dictionary" => ImmutableDictionary.CreateRange(StringComparer.OrdinalIgnoreCase, pairs),
#if IS_NET_8_0_OR_GREATER
            "frozen dictionary" => pairs.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
#endif
            "sorted dictionary" => new SortedDictionary<string, TestPerson>(byOrdinal, StringComparer.OrdinalIgnoreCase),
            "sorted dictionary by a comparison" => new SortedDictionary<string, TestPerson>(byOrdinal, byComparison),
            "sorted list" => new SortedList<string, TestPerson>(byOrdinal, StringComparer.OrdinalIgnoreCase),
            "immutable sorted dictionary" => ImmutableSortedDictionary.CreateRange(StringComparer.OrdinalIgnoreCase, pairs),
            "observable dictionary" => new ObservableDictionary<string, TestPerson>(byOrdinal, StringComparer.OrdinalIgnoreCase),
            "observable concurrent dictionary" => new ObservableConcurrentDictionary<string, TestPerson>(pairs, StringComparer.OrdinalIgnoreCase),
            "observable sorted dictionary" => new ObservableSortedDictionary<string, TestPerson>(byOrdinal, StringComparer.OrdinalIgnoreCase),
            "observable sorted dictionary by a comparison" => new ObservableSortedDictionary<string, TestPerson>(byOrdinal, byComparison),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    [TestMethod]
    public void CastAndTypeFilterLookUpKeysByTheSourceKeyComparer()
    {
        var source = CreateCaseInsensitiveSource();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var castQuery = sourceQuery.ObserveCast<object>())
            using (var typeFilterQuery = sourceQuery.ObserveOfType<TestPerson>())
            using (var castToPersonQuery = sourceQuery.ObserveCast<TestPerson>())
            using (var whereOfCastQuery = castToPersonQuery.ObserveWhere((key, value) => value.Name!.Length > 3))
            {
                Assert.IsTrue(castQuery.ContainsKey("ALPHA"), "the cast did not match a key differing only in case");
                Assert.AreSame(source["alpha"], castQuery["Alpha"]);
                Assert.IsTrue(typeFilterQuery.ContainsKey("ALPHA"), "the type filter did not match a key differing only in case");
                Assert.AreSame(source["alpha"], typeFilterQuery["Alpha"]);
                Assert.IsTrue(whereOfCastQuery.ContainsKey("ALPHA"), "a filter of the cast did not match a key differing only in case");
                source["ALPHA"] = new TestPerson("Bridget");
                Assert.AreEqual(3, castQuery.Count, "replacing a value through a differently cased key added a key to the cast");
                Assert.AreSame(source["alpha"], castQuery["ALPHA"]);
                Assert.AreEqual(3, typeFilterQuery.Count, "replacing a value through a differently cased key added a key to the type filter");
                Assert.AreSame(source["alpha"], typeFilterQuery["ALPHA"]);
                source.Remove("Alpha");
                Assert.AreEqual(2, castQuery.Count);
                Assert.IsFalse(castQuery.ContainsKey("alpha"));
                Assert.AreEqual(2, typeFilterQuery.Count);
                Assert.IsFalse(typeFilterQuery.ContainsKey("alpha"));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ConcurrentlyAndSynchronizationContextQueriesLookUpKeysByTheSourceKeyComparer()
    {
        var source = CreateCaseInsensitiveSource();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using var synchronizationContext = new AsyncSynchronizationContext();
            using (var concurrentQuery = sourceQuery.ObserveConcurrently())
            using (var synchronizationContextQuery = sourceQuery.ObserveUsingSynchronizationContext(synchronizationContext))
            using (var synchronizationContextEventuallyQuery = sourceQuery.ObserveUsingSynchronizationContextEventually(synchronizationContext))
            {
                Assert.IsTrue(concurrentQuery.ContainsKey("ALPHA"), "the concurrent dictionary did not match a key differing only in case");
                Assert.AreSame(source["alpha"], concurrentQuery["Alpha"]);
                Assert.IsTrue(synchronizationContextQuery.ContainsKey("ALPHA"), "the dictionary kept on a synchronization context did not match a key differing only in case");
                Assert.AreSame(source["alpha"], synchronizationContextQuery["Alpha"]);
                Assert.IsTrue(synchronizationContextEventuallyQuery.ContainsKey("ALPHA"), "the dictionary eventually kept on a synchronization context did not match a key differing only in case");
                Assert.AreSame(source["alpha"], synchronizationContextEventuallyQuery["Alpha"]);
                source["ALPHA"] = new TestPerson("Bridget");
                Assert.AreEqual(3, concurrentQuery.Count, "replacing a value through a differently cased key added a key to the concurrent dictionary");
                Assert.AreSame(source["alpha"], concurrentQuery["ALPHA"]);
                Assert.AreEqual(3, synchronizationContextQuery.Count, "replacing a value through a differently cased key added a key to the dictionary kept on a synchronization context");
                Assert.AreSame(source["alpha"], synchronizationContextQuery["ALPHA"]);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void FilterOfAProjectionLooksUpKeysByTheComparerGivenTheProjection()
    {
        var source = CreateCaseInsensitiveSource();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var selectQuery = sourceQuery.ObserveSelect((key, value) => key, (key, value) => value, StringComparer.OrdinalIgnoreCase))
            using (var whereQuery = selectQuery.ObserveWhere((key, value) => value.Name!.Length > 3))
            {
                Assert.IsTrue(selectQuery.ContainsKey("ALPHA"));
                Assert.IsTrue(whereQuery.ContainsKey("ALPHA"), "a filter of the projection did not match a key differing only in case");
                Assert.AreSame(source["alpha"], whereQuery["Alpha"]);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    [DataRow("dictionary")]
    [DataRow("concurrent dictionary")]
    [DataRow("immutable dictionary")]
#if IS_NET_8_0_OR_GREATER
    [DataRow("frozen dictionary")]
#endif
    [DataRow("sorted dictionary")]
    [DataRow("sorted dictionary by a comparison")]
    [DataRow("sorted list")]
    [DataRow("immutable sorted dictionary")]
    [DataRow("observable dictionary")]
    [DataRow("observable concurrent dictionary")]
    [DataRow("observable sorted dictionary")]
    [DataRow("observable sorted dictionary by a comparison")]
    public void FiltersLookUpKeysByTheComparerOfTheirSource(string kind)
    {
        var source = CreateCaseInsensitiveSource(kind);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere((key, value) => value.Name!.Length > 3))
            {
                Assert.IsTrue(sourceQuery.ContainsKey("ALPHA"), "the source did not match a key differing only in case");
                Assert.AreSame(source["alpha"], sourceQuery["Alpha"]);
                Assert.AreEqual(2, whereQuery.Count);
                Assert.IsTrue(whereQuery.ContainsKey("ALPHA"), "the filter did not match a key differing only in case");
                Assert.AreSame(source["alpha"], whereQuery["Alpha"]);
                Assert.IsFalse(whereQuery.ContainsKey("GAMMA"));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SelectCarriesTheSourceKeyComparerIntoItsClaimants()
    {
        var source = CreateCaseInsensitiveSource();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var selectQuery = sourceQuery.ObserveSelect((key, value) => value.Name!.Length, (key, value) => key))
            {
                Assert.AreEqual(3, selectQuery.Count);
                Assert.IsNull(selectQuery.OperationFault);
                source["ALPHA"] = new TestPerson("Jo");
                Assert.AreEqual(3, selectQuery.Count, "replacing a value through a differently cased key left the original projection in place");
                Assert.IsNull(selectQuery.OperationFault, "replacing a value through a differently cased key claimed a projected key twice");
                Assert.IsTrue(selectQuery.ContainsKey(2));
                Assert.IsFalse(selectQuery.ContainsKey(7));
                source.Remove("Alpha");
                Assert.AreEqual(2, selectQuery.Count);
                Assert.IsFalse(selectQuery.ContainsKey(2));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ToCollectionCarriesTheSourceKeyComparerIntoItsPositions()
    {
        var source = CreateCaseInsensitiveSource();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var toCollectionQuery = sourceQuery.ObserveToCollection((key, value) => value.Name!.Length))
            {
                Assert.AreEqual(3, toCollectionQuery.Count);
                source["GAMMA"] = new TestPerson("Nanette");
                Assert.AreEqual(3, toCollectionQuery.Count);
                CollectionAssert.AreEquivalent(new[] { 7, 5, 7 }, toCollectionQuery.ToList());
                source.Remove("Gamma");
                Assert.AreEqual(2, toCollectionQuery.Count);
                CollectionAssert.AreEquivalent(new[] { 7, 5 }, toCollectionQuery.ToList());
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void WhereCarriesTheSourceKeyComparerIntoItsResult()
    {
        var source = CreateCaseInsensitiveSource();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere((key, value) => value.Name!.Length > 3))
            {
                Assert.AreEqual(2, whereQuery.Count);
                Assert.IsTrue(whereQuery.ContainsKey("ALPHA"), "the filtered dictionary did not match a key differing only in case");
                Assert.AreSame(source["alpha"], whereQuery["Alpha"]);
                source["ALPHA"] = new TestPerson("Jo");
                Assert.AreEqual(1, whereQuery.Count, "replacing a value through a differently cased key did not update the filter");
                Assert.IsFalse(whereQuery.ContainsKey("alpha"));
                source["Alpha"] = new TestPerson("Bridget");
                Assert.AreEqual(2, whereQuery.Count);
                Assert.AreSame(source["ALPHA"], whereQuery["alpha"]);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
