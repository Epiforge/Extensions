namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionCount
{
    [TestMethod]
    public void ElementResultChanges()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var countQuery = sourceQuery.ObserveCount(p => p.Name!.Length == 4))
            {
                Assert.IsNull(countQuery.Evaluation.Fault);
                Assert.AreEqual(2, countQuery.Evaluation.Result);
                source[0].Name = "Johnny";
                Assert.AreEqual(1, countQuery.Evaluation.Result);
                source[1].Name = "Emilia";
                Assert.AreEqual(1, countQuery.Evaluation.Result);
                source[12].Name = "Jack";
                Assert.AreEqual(2, countQuery.Evaluation.Result);
                using var countQuery2 = sourceQuery.ObserveCount(p => p.Name!.Length == 4);
                Assert.AreSame(countQuery, countQuery2);
                Assert.AreEqual(2, sourceQuery.CachedObservableQueries);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ElementsAdded()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var countQuery = sourceQuery.ObserveCount(p => p.Name!.Length == 4))
            {
                Assert.IsNull(countQuery.Evaluation.Fault);
                Assert.AreEqual(2, countQuery.Evaluation.Result);
                source.Add(new TestPerson("Jack"));
                Assert.AreEqual(3, countQuery.Evaluation.Result);
                source.Add(new TestPerson("Chuck"));
                Assert.AreEqual(3, countQuery.Evaluation.Result);
                source.AddRange(new TestPerson[] { new TestPerson("Jill"), new TestPerson("Nick") });
                Assert.AreEqual(5, countQuery.Evaluation.Result);
                source.AddRange(new TestPerson[] { new TestPerson("Clint"), new TestPerson("Harry") });
                Assert.AreEqual(5, countQuery.Evaluation.Result);
                source.AddRange(new TestPerson[] { new TestPerson("Dana"), new TestPerson("Ray") });
                Assert.AreEqual(6, countQuery.Evaluation.Result);
                source[14] = new TestPerson("Tony");
                Assert.AreEqual(6, countQuery.Evaluation.Result);
                using var countQuery2 = sourceQuery.ObserveCount(p => p.Name!.Length == 4);
                Assert.AreSame(countQuery, countQuery2);
                Assert.AreEqual(2, sourceQuery.CachedObservableQueries);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ElementsRemoved()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var countQuery = sourceQuery.ObserveCount(p => p.Name!.Length == 5))
            {
                Assert.IsNull(countQuery.Evaluation.Fault);
                Assert.AreEqual(6, countQuery.Evaluation.Result);
                source.RemoveAt(1);
                Assert.AreEqual(5, countQuery.Evaluation.Result);
                source.RemoveAt(0);
                Assert.AreEqual(5, countQuery.Evaluation.Result);
                source.RemoveRange(9, 2);
                Assert.AreEqual(3, countQuery.Evaluation.Result);
                source.RemoveRange(8, 2);
                Assert.AreEqual(2, countQuery.Evaluation.Result);
                using var countQuery2 = sourceQuery.ObserveCount(p => p.Name!.Length == 5);
                Assert.AreSame(countQuery, countQuery2);
                Assert.AreEqual(2, sourceQuery.CachedObservableQueries);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
