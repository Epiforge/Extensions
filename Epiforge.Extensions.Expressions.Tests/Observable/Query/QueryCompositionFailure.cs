namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class QueryCompositionFailure
{
    static CollectionObserver CreateObserverRejecting(string marker) =>
        new(new ExpressionObserver(new ExpressionObserverOptions
        {
            Optimizer = expression => expression.ToString().Contains(marker, StringComparison.Ordinal) ? throw new InvalidOperationException(marker) : expression
        }));

    [TestMethod]
    public void DistinctReleasesItsGroupByWhenItsSelectFails()
    {
        var source = new ObservableRangeCollection<int>([1, 2, 2, 3]);
        var collectionObserver = CreateObserverRejecting(".Key");
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            Assert.ThrowsException<InvalidOperationException>(() => sourceQuery.ObserveDistinct(EqualityComparer<int>.Default));
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
