namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionUsingSynchronizationContextEventually
{
    [TestMethod]
    public async Task OperationFaultFlowsFromSourceAsync()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length == 4))
            {
                using var synchronizationContext = new AsyncSynchronizationContext();
                using (var usingSynchronizationContextEventuallyQuery = whereQuery.ObserveUsingSynchronizationContextEventually(synchronizationContext))
                {
                    var operationFaultChanged = new AsyncManualResetEvent();
                    var changingObservedTheOldValue = false;
                    usingSynchronizationContextEventuallyQuery.PropertyChanging += (sender, e) =>
                    {
                        if (e.PropertyName == nameof(usingSynchronizationContextEventuallyQuery.OperationFault))
                            changingObservedTheOldValue = usingSynchronizationContextEventuallyQuery.OperationFault is null;
                    };
                    usingSynchronizationContextEventuallyQuery.PropertyChanged += (sender, e) =>
                    {
                        if (e.PropertyName == nameof(usingSynchronizationContextEventuallyQuery.OperationFault))
                            operationFaultChanged.Set();
                    };
                    Assert.IsNull(usingSynchronizationContextEventuallyQuery.OperationFault);
                    var operationFaultChangedTask = operationFaultChanged.WaitAsync();
                    source.Add(new TestPerson());
                    await operationFaultChangedTask;
                    Assert.IsTrue(changingObservedTheOldValue);
                    Assert.IsNotNull(usingSynchronizationContextEventuallyQuery.OperationFault);
                }
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public async Task SourceManipulationAsync()
    {
        var source = new ObservableRangeCollection<int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using var synchronizationContext = new AsyncSynchronizationContext();
            using (var usingSynchronizationContextEventuallyQuery = sourceQuery.ObserveUsingSynchronizationContextEventually(synchronizationContext))
            {
                using var conditionCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var queryChanges = 0;
                void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
                {
                    ++queryChanges;
                }
                usingSynchronizationContextEventuallyQuery.CollectionChanged += collectionChanged;
                source.Add(1);
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSynchronizationContextEventuallyQuery.Count == 1, conditionCancellation.Token);
                Assert.AreEqual("1", string.Join(",", usingSynchronizationContextEventuallyQuery));
                source.AddRange(Enumerable.Range(2, 2));
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSynchronizationContextEventuallyQuery.Count == 3, conditionCancellation.Token);
                Assert.AreEqual("1,2,3", string.Join(",", usingSynchronizationContextEventuallyQuery));
                source.InsertRange(2, Enumerable.Range(4, 2));
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSynchronizationContextEventuallyQuery.Count == 5, conditionCancellation.Token);
                Assert.AreEqual("1,2,4,5,3", string.Join(",", usingSynchronizationContextEventuallyQuery));
                source.ReplaceRange(2, 3, Enumerable.Range(3, 2));
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSynchronizationContextEventuallyQuery.Count == 4, conditionCancellation.Token);
                Assert.AreEqual("1,2,3,4", string.Join(",", usingSynchronizationContextEventuallyQuery));
                source.ReplaceRange(2, 2, Enumerable.Range(3, 3));
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSynchronizationContextEventuallyQuery.Count == 5, conditionCancellation.Token);
                Assert.AreEqual("1,2,3,4,5", string.Join(",", usingSynchronizationContextEventuallyQuery));
                source.RemoveRange(3, 2);
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSynchronizationContextEventuallyQuery.Count == 3, conditionCancellation.Token);
                Assert.AreEqual("1,2,3", string.Join(",", usingSynchronizationContextEventuallyQuery));
                source.Reset(Enumerable.Range(1, 5));
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSynchronizationContextEventuallyQuery.Count == 5, conditionCancellation.Token);
                Assert.AreEqual("1,2,3,4,5", string.Join(",", usingSynchronizationContextEventuallyQuery));
                source.MoveRange(3, 0, 2);
                await collectionObserver.ExpressionObserver.ConditionAsync(() => usingSynchronizationContextEventuallyQuery.Count == 5 && usingSynchronizationContextEventuallyQuery[0] == 4, conditionCancellation.Token);
                Assert.AreEqual("4,5,1,2,3", string.Join(",", usingSynchronizationContextEventuallyQuery));
                Assert.AreEqual(5, usingSynchronizationContextEventuallyQuery.Count);
                Assert.AreEqual(1, usingSynchronizationContextEventuallyQuery[2]);
                Assert.AreEqual(10, queryChanges, "the eight source operations did not raise ten notifications between them");
                usingSynchronizationContextEventuallyQuery.CollectionChanged -= collectionChanged;
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
