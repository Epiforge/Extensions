namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class QueryCopying
{
    static IObservableCollectionQuery<int> Observe(IObservableCollectionQuery<int> sourceQuery, string query) =>
        query switch
        {
            "filter" => sourceQuery.ObserveWhere(number => number % 2 == 0),
            "projection" => sourceQuery.ObserveSelect(number => number * 10),
            _ => sourceQuery
        };

    static void AssertCopiesAsItEnumerates(IObservableCollectionQuery<int> query)
    {
        var enumerated = new List<int>();
        foreach (var element in query)
            enumerated.Add(element);
        var expected = enumerated.ToArray();
        var array = Enumerable.Repeat(-1, expected.Length + 3).ToArray();
        query.CopyTo(array, 2);
        CollectionAssert.AreEqual(new[] { -1, -1 }.Concat(expected).Append(-1).ToArray(), array);
        var nonGeneric = Enumerable.Repeat(-1, expected.Length + 1).ToArray();
        ((ICollection)query).CopyTo(nonGeneric, 1);
        CollectionAssert.AreEqual(new[] { -1 }.Concat(expected).ToArray(), nonGeneric);
        CollectionAssert.AreEqual(expected, new List<int>(query));
    }

    [TestMethod]
    [DataRow("source")]
    [DataRow("filter")]
    [DataRow("projection")]
    public void ACopyHoldsTheElementsInOrderThroughChanges(string query)
    {
        var source = new ObservableRangeCollection<int>([1, 2, 3, 4, 5, 6]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using var observed = Observe(sourceQuery, query);
            AssertCopiesAsItEnumerates(observed);
            source.Insert(1, 8);
            AssertCopiesAsItEnumerates(observed);
            source.RemoveAt(4);
            AssertCopiesAsItEnumerates(observed);
            source.Move(0, 5);
            AssertCopiesAsItEnumerates(observed);
            source[2] = 12;
            AssertCopiesAsItEnumerates(observed);
            CollectionAssert.AreEqual(query switch
            {
                "filter" => new[] { 8, 2, 12, 6 },
                "projection" => new[] { 80, 20, 120, 50, 60, 10 },
                _ => new[] { 8, 2, 12, 5, 6, 1 }
            }, observed.ToArray());
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    [DataRow("source")]
    [DataRow("filter")]
    [DataRow("projection")]
    public void ACopyIntoTooShortAnArrayFailsAsArraysDo(string query)
    {
        var source = new ObservableRangeCollection<int>([1, 2, 3, 4, 5, 6]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using var observed = Observe(sourceQuery, query);
            Assert.ThrowsExactly<IndexOutOfRangeException>(() => observed.CopyTo(new int[observed.Count], 1));
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ACopyIntoAnArrayOfADerivedTypeFailsAsArraysDo()
    {
        var source = new ObservableRangeCollection<object>(["first", 2]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
            Assert.ThrowsExactly<InvalidCastException>(() => sourceQuery.CopyTo(new string[2], 0));
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
