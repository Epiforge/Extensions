namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionIndividualChanges
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new RangeObservableCollection<int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var individualChangeQuery = sourceQuery.ObserveIndividualChanges())
            {
                var changeNotifications = new List<NotifyCollectionChangedEventArgs>();
                void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e) =>
                    changeNotifications!.Add(e);
                individualChangeQuery.CollectionChanged += collectionChangedHandler;
                source.Add(1);
                Assert.AreEqual("1", string.Join(",", individualChangeQuery));
                Assert.AreEqual(1, changeNotifications.Count);
                source.AddRange(Enumerable.Range(2, 2));
                Assert.AreEqual("1,2,3", string.Join(",", individualChangeQuery));
                Assert.AreEqual(3, changeNotifications.Count);
                source.InsertRange(2, Enumerable.Range(4, 2));
                Assert.AreEqual("1,2,4,5,3", string.Join(",", individualChangeQuery));
                Assert.AreEqual(5, changeNotifications.Count);
                source.ReplaceRange(2, 3, Enumerable.Range(3, 2));
                Assert.AreEqual("1,2,3,4", string.Join(",", individualChangeQuery));
                Assert.AreEqual(8, changeNotifications.Count);
                source.ReplaceRange(2, 2, Enumerable.Range(3, 3));
                Assert.AreEqual("1,2,3,4,5", string.Join(",", individualChangeQuery));
                Assert.AreEqual(11, changeNotifications.Count);
                source.RemoveRange(3, 2);
                Assert.AreEqual("1,2,3", string.Join(",", individualChangeQuery));
                Assert.AreEqual(13, changeNotifications.Count);
                source.Reset(Enumerable.Range(1, 5));
                Assert.AreEqual("1,2,3,4,5", string.Join(",", individualChangeQuery));
                Assert.AreEqual(14, changeNotifications.Count);
                source.MoveRange(3, 0, 2);
                Assert.AreEqual("4,5,1,2,3", string.Join(",", individualChangeQuery));
                Assert.AreEqual(16, changeNotifications.Count);
                Assert.AreEqual(5, individualChangeQuery.Count);
                Assert.AreEqual(1, individualChangeQuery[2]);
                individualChangeQuery.CollectionChanged -= collectionChangedHandler;
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
