namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class QueryNotificationOrdering
{
    [TestMethod]
    [Timeout(120000)]
    public void ConcurrentElementChangesDeliverOrderedNotifications()
    {
        var threadCount = Math.Max(8, Environment.ProcessorCount * 2);
        const int peoplePerThread = 25;
        const int iterations = 100;

        var people = Enumerable.Range(0, threadCount * peoplePerThread).Select(_ => new TestPerson("A")).ToList();
        var source = new ObservableRangeCollection<TestPerson>(people);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length == 4))
            {
                var mirror = new List<TestPerson>();
                Exception? mirrorFault = null;
                string? mirrorFaultContext = null;
                var notificationsDelivered = 0;
                void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
                {
                    try
                    {
                        ++notificationsDelivered;
                        switch (e.Action)
                        {
                            case NotifyCollectionChangedAction.Add:
                                Assert.AreEqual(1, e.NewItems!.Count);
                                mirror.Insert(e.NewStartingIndex, (TestPerson)e.NewItems[0]!);
                                break;
                            case NotifyCollectionChangedAction.Remove:
                                Assert.AreEqual(1, e.OldItems!.Count);
                                Assert.AreSame(e.OldItems[0], mirror[e.OldStartingIndex]);
                                mirror.RemoveAt(e.OldStartingIndex);
                                break;
                            default:
                                throw new InvalidOperationException($"unexpected action {e.Action}");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (mirrorFault is null)
                        {
                            mirrorFault = ex;
                            mirrorFaultContext = $"after {notificationsDelivered} notifications, {e.Action} of {e.OldItems?.Count ?? e.NewItems?.Count ?? 0} at old index {e.OldStartingIndex} / new index {e.NewStartingIndex} against a mirror of {mirror.Count}: {ex.GetType().Name}: {ex.Message}";
                        }
                    }
                }
                whereQuery.CollectionChanged += collectionChanged;
                Parallel.For(0, threadCount, new ParallelOptions { MaxDegreeOfParallelism = threadCount }, thread =>
                {
                    var owned = people.Skip(thread * peoplePerThread).Take(peoplePerThread).ToList();
                    for (var iteration = 0; iteration < iterations; ++iteration)
                    {
                        foreach (var person in owned)
                            person.Name = (iteration + thread) % 2 == 0 ? "Bill" : "A";
                        if (iteration % 8 == thread % 8)
                            Thread.Yield();
                    }
                    for (var i = 0; i < owned.Count; ++i)
                        owned[i].Name = i % 2 == 0 ? "Bill" : "A";
                });
                whereQuery.CollectionChanged -= collectionChanged;
                Assert.IsNull(mirrorFault, mirrorFaultContext);
                var settled = whereQuery.ToList();
                Assert.AreEqual(settled.Count, mirror.Count, $"after {notificationsDelivered} notifications the query settled at {settled.Count} and the mirror at {mirror.Count}");
                CollectionAssert.AreEqual(settled, mirror);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
