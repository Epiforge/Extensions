namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Covers when a query synchronized with a context delivers a change of its source: before the change returns when it is kept consistent, and only once the context runs what was posted to it when it is kept eventually consistent
/// </summary>
/// <remarks>
/// The context here runs what is sent to it at once and holds what is posted to it until the test releases it, which is what distinguishes the two ways of synchronizing. A context which runs both on one thread cannot tell them apart when the test reads through the query, because the read is marshalled to the same thread and waits behind the delivery
/// </remarks>
[TestClass]
public class SynchronizationContextDelivery
{
    sealed class HeldSynchronizationContext :
        SynchronizationContext
    {
        readonly Queue<(SendOrPostCallback callback, object? state)> held = new();

        public override void Post(SendOrPostCallback d, object? state) =>
            held.Enqueue((d, state));

        public void RunHeld()
        {
            while (held.TryDequeue(out var posted))
                posted.callback(posted.state);
        }

        public override void Send(SendOrPostCallback d, object? state) =>
            d(state);
    }

    [TestMethod]
    public void CollectionDeliversBeforeTheChangeReturns()
    {
        var source = new ObservableRangeCollection<int>([1, 2]);
        var collectionObserver = CollectionObserverHelpers.Create();
        var synchronizationContext = new HeldSynchronizationContext();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var query = sourceQuery.ObserveUsingSynchronizationContext(synchronizationContext);
        synchronizationContext.RunHeld();
        var changes = 0;
        query.CollectionChanged += (_, _) => ++changes;
        source.Add(3);
        Assert.AreEqual(1, changes);
    }

    [TestMethod]
    public void CollectionEventuallyDeliversWhenTheContextRuns()
    {
        var source = new ObservableRangeCollection<int>([1, 2]);
        var collectionObserver = CollectionObserverHelpers.Create();
        var synchronizationContext = new HeldSynchronizationContext();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var query = sourceQuery.ObserveUsingSynchronizationContextEventually(synchronizationContext);
        synchronizationContext.RunHeld();
        var changes = 0;
        query.CollectionChanged += (_, _) => ++changes;
        source.Add(3);
        Assert.AreEqual(0, changes);
        synchronizationContext.RunHeld();
        Assert.AreEqual(1, changes);
    }

    [TestMethod]
    public void DictionaryDeliversBeforeTheChangeReturns()
    {
        var source = new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1 });
        var collectionObserver = CollectionObserverHelpers.Create();
        var synchronizationContext = new HeldSynchronizationContext();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var query = sourceQuery.ObserveUsingSynchronizationContext(synchronizationContext);
        synchronizationContext.RunHeld();
        var changes = 0;
        ((INotifyDictionaryChanged<string, int>)query).DictionaryChanged += (_, _) => ++changes;
        source.Add("b", 2);
        Assert.AreEqual(1, changes);
    }

    [TestMethod]
    public void DictionaryEventuallyDeliversWhenTheContextRuns()
    {
        var source = new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1 });
        var collectionObserver = CollectionObserverHelpers.Create();
        var synchronizationContext = new HeldSynchronizationContext();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var query = sourceQuery.ObserveUsingSynchronizationContextEventually(synchronizationContext);
        synchronizationContext.RunHeld();
        var changes = 0;
        ((INotifyDictionaryChanged<string, int>)query).DictionaryChanged += (_, _) => ++changes;
        source.Add("b", 2);
        Assert.AreEqual(0, changes);
        synchronizationContext.RunHeld();
        Assert.AreEqual(1, changes);
    }
}
