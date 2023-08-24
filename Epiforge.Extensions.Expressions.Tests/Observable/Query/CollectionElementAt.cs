namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionElementAt
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var elementAtQuery = sourceQuery.ObserveElementAt(9))
            {
                Assert.IsNull(elementAtQuery.Evaluation.Fault);
                Assert.AreEqual(9, elementAtQuery.Evaluation.Result);
                source.Remove(9);
                Assert.IsInstanceOfType<IndexOutOfRangeException>(elementAtQuery.Evaluation.Fault);
                Assert.AreEqual(0, elementAtQuery.Evaluation.Result);
                source.Add(30);
                Assert.IsNull(elementAtQuery.Evaluation.Fault);
                Assert.AreEqual(30, elementAtQuery.Evaluation.Result);
                source.Insert(9, 15);
                Assert.AreEqual(15, elementAtQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
