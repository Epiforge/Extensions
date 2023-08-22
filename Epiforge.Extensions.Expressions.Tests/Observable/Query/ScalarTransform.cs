namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class ScalarTransform
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
                using (var transformQuery = countQuery.ObserveTransform(count => (long)count))
                {
                    Assert.IsNull(transformQuery.Evaluation.Fault);
                    Assert.AreEqual(2, transformQuery.Evaluation.Result);
                    source[0].Name = "Johnny";
                    Assert.AreEqual(1, transformQuery.Evaluation.Result);
                    source[1].Name = "Emilia";
                    Assert.AreEqual(1, transformQuery.Evaluation.Result);
                    source[12].Name = "Jack";
                    Assert.AreEqual(2, transformQuery.Evaluation.Result);
                    using var transformQuery2 = countQuery.ObserveTransform(count => (long)count);
                    Assert.AreSame(transformQuery, transformQuery2);
                    Assert.AreEqual(1, countQuery.CachedObservableQueries);
                }
                Assert.AreEqual(2, sourceQuery.CachedObservableQueries);
                Assert.AreEqual(3, collectionObserver.CachedObservableQueries);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
