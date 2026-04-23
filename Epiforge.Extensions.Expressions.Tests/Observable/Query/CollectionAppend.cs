namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionAppend
{
    [TestMethod]
    public void SourceManipulation()
    {
        var left = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var leftQuery = collectionObserver.ObserveReadOnlyList(left))
        {
            using (var appendQuery = leftQuery.ObserveAppend(new("Zac")))
            {
                Assert.IsNull(appendQuery.OperationFault);
                Assert.AreEqual(15, appendQuery.Count);
                Assert.AreEqual("Zac", appendQuery[14].Name);
                left.RemoveAt(0);
                Assert.AreEqual(14, appendQuery.Count);
                Assert.AreEqual("Zac", appendQuery[13].Name);
                left[0] = left[0];
                left.ReplaceRange(0, 2, left.GetRange(0, 2));
                left.Add(left[0]);
                Assert.AreEqual(15, appendQuery.Count);
                Assert.AreEqual("Zac", appendQuery[14].Name);
                left.Reset([]);
                Assert.AreEqual(1, appendQuery.Count);
                Assert.AreEqual("Zac", appendQuery[0].Name);
            }
            Assert.AreEqual(0, leftQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
