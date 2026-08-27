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
