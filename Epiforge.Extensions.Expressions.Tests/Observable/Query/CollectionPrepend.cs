namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionPrepend
{
    [TestMethod]
    public void SourceManipulation()
    {
        var left = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var leftQuery = collectionObserver.ObserveReadOnlyList(left))
        {
            using (var prependQuery = leftQuery.ObservePrepend(new("Zac")))
            {
                Assert.IsNull(prependQuery.OperationFault);
                Assert.AreEqual(15, prependQuery.Count);
                Assert.AreEqual("Zac", prependQuery[0].Name);
                left.RemoveAt(0);
                Assert.AreEqual(14, prependQuery.Count);
                Assert.AreEqual("Zac", prependQuery[0].Name);
                left[0] = left[0];
                left.ReplaceRange(0, 2, left.GetRange(0, 2));
                left.Add(left[0]);
                Assert.AreEqual(15, prependQuery.Count);
                Assert.AreEqual("Zac", prependQuery[0].Name);
                left.Reset([]);
                Assert.AreEqual(1, prependQuery.Count);
                Assert.AreEqual("Zac", prependQuery[0].Name);
            }
            Assert.AreEqual(0, leftQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
