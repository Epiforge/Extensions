namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionAll
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new RangeObservableCollection<int>(Enumerable.Range(1, 10).Select(i => i * 3));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var allQuery = sourceQuery.ObserveAll(i => i % 3 == 0))
            {
                Assert.IsNull(allQuery.Evaluation.Fault);
                Assert.IsTrue(allQuery.Evaluation.Result);
                source[0] = 2;
                Assert.IsFalse(allQuery.Evaluation.Result);
                source.RemoveAt(0);
                Assert.IsTrue(allQuery.Evaluation.Result);
                --source[0];
                Assert.IsFalse(allQuery.Evaluation.Result);
                source.Clear();
                Assert.IsTrue(allQuery.Evaluation.Result);
                source.Add(7);
                Assert.IsFalse(allQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
