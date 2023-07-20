namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionSum
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new RangeObservableCollection<int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var sumQuery = sourceQuery.ObserveSum())
            {
                Assert.IsNull(sumQuery.Evaluation.Fault);
                Assert.AreEqual(0, sumQuery.Evaluation.Result);
                source.Add(1);
                Assert.IsNull(sumQuery.Evaluation.Fault);
                Assert.AreEqual(1, sumQuery.Evaluation.Result);
                source.AddRange(System.Linq.Enumerable.Range(2, 3));
                Assert.AreEqual(10, sumQuery.Evaluation.Result);
                source.RemoveRange(0, 2);
                Assert.AreEqual(7, sumQuery.Evaluation.Result);
                source.RemoveAt(0);
                Assert.AreEqual(4, sumQuery.Evaluation.Result);
                source.Reset(Enumerable.Range(1, 3));
                Assert.AreEqual(6, sumQuery.Evaluation.Result);
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
            using (var sumQuery = sourceQuery.ObserveSum(person => person.Name!.Length))
            {
                Assert.IsNull(sumQuery.Evaluation.Fault);
                Assert.AreEqual(74, sumQuery.Evaluation.Result);
                source.Add(source[0]);
                Assert.AreEqual(78, sumQuery.Evaluation.Result);
                source[0].Name = "Johnny";
                Assert.AreEqual(82, sumQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
