namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionElementAtOrDefault
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new RangeObservableCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var elementAtOrDefaultQuery = sourceQuery.ObserveElementAtOrDefault(9))
            {
                Assert.IsNull(elementAtOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(9, elementAtOrDefaultQuery.Evaluation.Result);
                source.Remove(9);
                Assert.IsNull(elementAtOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, elementAtOrDefaultQuery.Evaluation.Result);
                source.Add(30);
                Assert.IsNull(elementAtOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(30, elementAtOrDefaultQuery.Evaluation.Result);
                source.Insert(9, 15);
                Assert.AreEqual(15, elementAtOrDefaultQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
