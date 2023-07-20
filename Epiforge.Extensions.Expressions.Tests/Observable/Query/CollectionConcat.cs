namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionConcat
{
    [TestMethod]
    public void SourceManipulation()
    {
        var left = TestPerson.CreatePeopleCollection();
        var right = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var leftQuery = collectionObserver.ObserveReadOnlyList(left))
        using (var rightQuery = collectionObserver.ObserveReadOnlyList(right))
        {
            using (var concatQuery = leftQuery.ObserveConcat(rightQuery))
            {
                Assert.IsNull(concatQuery.OperationFault);
                Assert.AreEqual(28, concatQuery.Count);
                left.RemoveAt(0);
                Assert.AreEqual(27, concatQuery.Count);
                right.RemoveRange(12, 2);
                Assert.AreEqual(25, concatQuery.Count);
                left[0] = left[0];
                left.ReplaceRange(0, 2, left.GetRange(0, 2));
                left.Add(left[0]);
                Assert.AreEqual(26, concatQuery.Count);
                left.Reset(Enumerable.Empty<TestPerson>());
                Assert.AreEqual(12, concatQuery.Count);
                right[0] = right[0];
                right.ReplaceRange(0, 2, right.GetRange(0, 2));
                right.Add(right[0]);
                Assert.AreEqual(13, concatQuery.Count);
                right.Reset(Enumerable.Empty<TestPerson>());
                Assert.AreEqual(0, concatQuery.Count);
            }
            Assert.AreEqual(0, leftQuery.CachedObservableQueries);
            Assert.AreEqual(0, rightQuery.CachedObservableQueries);
            Assert.AreEqual(2, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SourceManipulationLeftDoesNotNotify()
    {
        var left = TestPerson.MakePeople();
        var right = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var leftQuery = collectionObserver.ObserveEnumerable(left))
        using (var rightQuery = collectionObserver.ObserveReadOnlyList(right))
        {
            using (var concatQuery = leftQuery.ObserveConcat(rightQuery))
            {
                Assert.IsNull(concatQuery.OperationFault);
                Assert.AreEqual(28, concatQuery.Count);
                right[0] = right[0];
                right.ReplaceRange(0, 2, right.GetRange(0, 2));
                right.Add(right[0]);
                Assert.AreEqual(29, concatQuery.Count);
                right.Reset(Enumerable.Empty<TestPerson>());
                Assert.AreEqual(14, concatQuery.Count);
            }
            Assert.AreEqual(0, leftQuery.CachedObservableQueries);
            Assert.AreEqual(0, rightQuery.CachedObservableQueries);
            Assert.AreEqual(2, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SourceManipulationRightDoesNotNotify()
    {
        var left = TestPerson.CreatePeopleCollection();
        var right = TestPerson.MakePeople();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var leftQuery = collectionObserver.ObserveReadOnlyList(left))
        using (var rightQuery = collectionObserver.ObserveEnumerable(right))
        {
            using (var concatQuery = leftQuery.ObserveConcat(rightQuery))
            {
                Assert.IsNull(concatQuery.OperationFault);
                Assert.AreEqual(28, concatQuery.Count);
                left.RemoveAt(0);
                Assert.AreEqual(27, concatQuery.Count);
                left[0] = left[0];
                left.ReplaceRange(0, 2, left.GetRange(0, 2));
                left.Add(left[0]);
                Assert.AreEqual(28, concatQuery.Count);
                left.Reset(System.Linq.Enumerable.Empty<TestPerson>());
                Assert.AreEqual(14, concatQuery.Count);
            }
            Assert.AreEqual(0, leftQuery.CachedObservableQueries);
            Assert.AreEqual(0, rightQuery.CachedObservableQueries);
            Assert.AreEqual(2, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
