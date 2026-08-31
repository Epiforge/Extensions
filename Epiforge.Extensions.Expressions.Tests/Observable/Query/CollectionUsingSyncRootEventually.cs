namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionUsingSyncRootEventually
{
    [TestMethod]
    public async Task SourceManipulationAsync()
    {
        var source = new ObservableRangeCollection<int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            var syncRoot = new object();
            using (var usingSyncRootEventuallyQuery = sourceQuery.ObserveUsingSyncRootEventually(syncRoot))
            {
                using var conditionCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var queryChanges = 0;
                void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
                {
                    ++queryChanges;
                }
                usingSyncRootEventuallyQuery.CollectionChanged += collectionChanged;
                source.Add(1);
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSyncRootEventuallyQuery.Count == 1, conditionCancellation.Token);
                lock (syncRoot)
                    Assert.AreEqual("1", string.Join(",", usingSyncRootEventuallyQuery));
                source.AddRange(Enumerable.Range(2, 2));
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSyncRootEventuallyQuery.Count == 3, conditionCancellation.Token);
                lock (syncRoot)
                    Assert.AreEqual("1,2,3", string.Join(",", usingSyncRootEventuallyQuery));
                source.InsertRange(2, Enumerable.Range(4, 2));
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSyncRootEventuallyQuery.Count == 5, conditionCancellation.Token);
                lock (syncRoot)
                    Assert.AreEqual("1,2,4,5,3", string.Join(",", usingSyncRootEventuallyQuery));
                source.ReplaceRange(2, 3, Enumerable.Range(3, 2));
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSyncRootEventuallyQuery.Count == 4, conditionCancellation.Token);
                lock (syncRoot)
                    Assert.AreEqual("1,2,3,4", string.Join(",", usingSyncRootEventuallyQuery));
                source.ReplaceRange(2, 2, Enumerable.Range(3, 3));
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSyncRootEventuallyQuery.Count == 5, conditionCancellation.Token);
                lock (syncRoot)
                    Assert.AreEqual("1,2,3,4,5", string.Join(",", usingSyncRootEventuallyQuery));
                source.RemoveRange(3, 2);
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSyncRootEventuallyQuery.Count == 3, conditionCancellation.Token);
                lock (syncRoot)
                    Assert.AreEqual("1,2,3", string.Join(",", usingSyncRootEventuallyQuery));
                source.Reset(Enumerable.Range(1, 5));
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSyncRootEventuallyQuery.Count == 5, conditionCancellation.Token);
                lock (syncRoot)
                    Assert.AreEqual("1,2,3,4,5", string.Join(",", usingSyncRootEventuallyQuery));
                source.MoveRange(3, 0, 2);
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSyncRootEventuallyQuery.Count == 5 && usingSyncRootEventuallyQuery[0] == 4, conditionCancellation.Token);
                lock (syncRoot)
                {
                    Assert.AreEqual("4,5,1,2,3", string.Join(",", usingSyncRootEventuallyQuery));
                    Assert.AreEqual(5, usingSyncRootEventuallyQuery.Count);
                    Assert.AreEqual(1, usingSyncRootEventuallyQuery[2]);
                }
                Assert.AreEqual(10, queryChanges, "the eight source operations did not raise ten notifications between them");
                usingSyncRootEventuallyQuery.CollectionChanged -= collectionChanged;
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
