namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionMin
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableRangeCollection<int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var minQuery = sourceQuery.ObserveMin())
            {
                Assert.IsNotNull(minQuery.Evaluation.Fault);
                Assert.AreEqual(0, minQuery.Evaluation.Result);
                source.Add(1);
                Assert.IsNull(minQuery.Evaluation.Fault);
                Assert.AreEqual(1, minQuery.Evaluation.Result);
                source.AddRange(Enumerable.Range(2, 3));
                Assert.AreEqual(1, minQuery.Evaluation.Result);
                source.RemoveRange(0, 2);
                Assert.AreEqual(3, minQuery.Evaluation.Result);
                source.RemoveAt(0);
                Assert.AreEqual(4, minQuery.Evaluation.Result);
                source.Reset(Enumerable.Range(1, 3));
                Assert.AreEqual(1, minQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SourceManipulation()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var minQuery = sourceQuery.ObserveMin(p => p.Name!.Length))
            {
                Assert.IsNull(minQuery.Evaluation.Fault);
                Assert.AreEqual(3, minQuery.Evaluation.Result);
                source.Add(source[0]);
                Assert.AreEqual(3, minQuery.Evaluation.Result);
                source[0].Name = "J";
                Assert.AreEqual(1, minQuery.Evaluation.Result);
                source[0].Name = "John";
                Assert.AreEqual(3, minQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
