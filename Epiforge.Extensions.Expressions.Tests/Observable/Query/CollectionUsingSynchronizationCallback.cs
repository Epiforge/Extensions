namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionUsingSynchronizationCallback
{
    [TestMethod]
    public void OperationFaultFlowsFromSource()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length == 4))
            {
                var context = new object();
                void synchronizationCallback(IEnumerable enumerable, object context, Action accessMethod, bool writeAccess) =>
                    accessMethod();
                using (var usingSynchronizationCallbackQuery = whereQuery.ObserveUsingSynchronizationCallback(context, synchronizationCallback))
                {
                    var operationFaultChanging = 0;
                    var operationFaultChanged = 0;
                    usingSynchronizationCallbackQuery.PropertyChanging += (sender, e) =>
                    {
                        if (e.PropertyName == nameof(usingSynchronizationCallbackQuery.OperationFault))
                        {
                            Assert.IsNull(usingSynchronizationCallbackQuery.OperationFault);
                            ++operationFaultChanging;
                        }
                    };
                    usingSynchronizationCallbackQuery.PropertyChanged += (sender, e) =>
                    {
                        if (e.PropertyName == nameof(usingSynchronizationCallbackQuery.OperationFault))
                            ++operationFaultChanged;
                    };
                    Assert.IsNull(usingSynchronizationCallbackQuery.OperationFault);
                    source.Add(new TestPerson());
                    Assert.IsNotNull(usingSynchronizationCallbackQuery.OperationFault);
                    Assert.IsTrue(operationFaultChanging > 0);
                    Assert.IsTrue(operationFaultChanged > 0);
                }
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableRangeCollection<int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            var context = new object();
            var readerWriterLock = new ReaderWriterLockSlim();
            void synchronizationCallback(IEnumerable enumerable, object context, Action accessMethod, bool writeAccess)
            {
                if (writeAccess)
                {
                    readerWriterLock.EnterWriteLock();
                    try
                    {
                        accessMethod();
                    }
                    finally
                    {
                        readerWriterLock.ExitWriteLock();
                    }
                }
                else
                {
                    readerWriterLock.EnterReadLock();
                    try
                    {
                        accessMethod();
                    }
                    finally
                    {
                        readerWriterLock.ExitReadLock();
                    }
                }
            }
            using (var usingSynchronizationCallbackQuery = sourceQuery.ObserveUsingSynchronizationCallback(context, synchronizationCallback))
            {
                source.Add(1);
                synchronizationCallback(usingSynchronizationCallbackQuery, context, () => Assert.AreEqual("1", string.Join(",", usingSynchronizationCallbackQuery)), false);
                source.AddRange(Enumerable.Range(2, 2));
                synchronizationCallback(usingSynchronizationCallbackQuery, context, () => Assert.AreEqual("1,2,3", string.Join(",", usingSynchronizationCallbackQuery)), false);
                source.InsertRange(2, Enumerable.Range(4, 2));
                synchronizationCallback(usingSynchronizationCallbackQuery, context, () => Assert.AreEqual("1,2,4,5,3", string.Join(",", usingSynchronizationCallbackQuery)), false);
                source.ReplaceRange(2, 3, Enumerable.Range(3, 2));
                synchronizationCallback(usingSynchronizationCallbackQuery, context, () => Assert.AreEqual("1,2,3,4", string.Join(",", usingSynchronizationCallbackQuery)), false);
                source.ReplaceRange(2, 2, Enumerable.Range(3, 3));
                synchronizationCallback(usingSynchronizationCallbackQuery, context, () => Assert.AreEqual("1,2,3,4,5", string.Join(",", usingSynchronizationCallbackQuery)), false);
                source.RemoveRange(3, 2);
                synchronizationCallback(usingSynchronizationCallbackQuery, context, () => Assert.AreEqual("1,2,3", string.Join(",", usingSynchronizationCallbackQuery)), false);
                source.Reset(Enumerable.Range(1, 5));
                synchronizationCallback(usingSynchronizationCallbackQuery, context, () => Assert.AreEqual("1,2,3,4,5", string.Join(",", usingSynchronizationCallbackQuery)), false);
                source.MoveRange(3, 0, 2);
                synchronizationCallback(usingSynchronizationCallbackQuery, context, () =>
                {
                    Assert.AreEqual("4,5,1,2,3", string.Join(",", usingSynchronizationCallbackQuery));
                    Assert.AreEqual(5, usingSynchronizationCallbackQuery.Count);
                    Assert.AreEqual(1, usingSynchronizationCallbackQuery[2]);
                }, false);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
