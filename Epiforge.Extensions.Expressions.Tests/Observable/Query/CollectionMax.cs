namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionMax
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableRangeCollection<int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var maxQuery = sourceQuery.ObserveMax())
            {
                Assert.IsNotNull(maxQuery.Evaluation.Fault);
                Assert.AreEqual(0, maxQuery.Evaluation.Result);
                source.Add(1);
                Assert.IsNull(maxQuery.Evaluation.Fault);
                Assert.AreEqual(1, maxQuery.Evaluation.Result);
                source.AddRange(Enumerable.Range(2, 3));
                Assert.AreEqual(4, maxQuery.Evaluation.Result);
                source.RemoveRange(0, 2);
                Assert.AreEqual(4, maxQuery.Evaluation.Result);
                source.RemoveAt(1);
                Assert.AreEqual(3, maxQuery.Evaluation.Result);
                source.Reset(Enumerable.Range(2, 3));
                Assert.AreEqual(4, maxQuery.Evaluation.Result);
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
            using (var maxQuery = sourceQuery.ObserveMax(p => p.Name!.Length))
            {
                Assert.IsNull(maxQuery.Evaluation.Fault);
                Assert.AreEqual(7, maxQuery.Evaluation.Result);
                source.Add(source[0]);
                Assert.AreEqual(7, maxQuery.Evaluation.Result);
                source[0].Name = "Johnathon";
                Assert.AreEqual(9, maxQuery.Evaluation.Result);
                source[0].Name = "John";
                Assert.AreEqual(7, maxQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
