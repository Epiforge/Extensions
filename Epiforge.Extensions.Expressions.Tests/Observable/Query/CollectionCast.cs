namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionCast
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new RangeObservableCollection<int>(Enumerable.Range(1, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var castQuery = sourceQuery.ObserveCast<object>())
            {
                Assert.IsNull(castQuery.OperationFault);
                Assert.AreEqual(55, castQuery.Cast<int>().Sum());
                Assert.IsInstanceOfType(castQuery[0], typeof(object));
                source[0] += 10;
                Assert.IsNull(castQuery.OperationFault);
                Assert.AreEqual(65, castQuery.Cast<int>().Sum());
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
