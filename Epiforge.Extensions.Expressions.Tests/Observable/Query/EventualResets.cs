namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Queries kept eventually consistent with their source which are told the source was reset and are then told of a later change before they have caught up
/// </summary>
/// <remarks>
/// A reset says only that the source is now whatever it is. When a query catches up with it later, the source may already hold the change that follows the reset, and a query which reads the source at that moment and then applies the following change takes that change twice. Each row holds the query back, by holding its lock, its callback or its context, while the source is reset and changed again, so the interleaving is forced rather than waited for
/// </remarks>
[TestClass]
public class EventualResets
{
    static readonly TimeSpan patience = TimeSpan.FromSeconds(10);

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

    static void AssertCatchesUp(IObservableCollectionQuery<int> query, int[] expected)
    {
        if (!SpinWait.SpinUntil(() => query.SequenceEqual(expected), patience))
            Assert.AreEqual(string.Join(",", expected), string.Join(",", query), "the query never caught up with its source");
    }

    [TestMethod]
    public void ACallbackQueryResetBehindALaterChangeTakesItOnce()
    {
        var source = new ObservableRangeCollection<int>([1, 2]);
        var callbackAccess = new object();
        void synchronizationCallback(IEnumerable collection, object context, Action accessMethod, bool writeAccess)
        {
            lock (callbackAccess)
                accessMethod();
        }
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(source);
        using var query = observed.ObserveUsingSynchronizationCallbackEventually(new object(), synchronizationCallback);
        AssertCatchesUp(query, [1, 2]);
        lock (callbackAccess)
        {
            source.Add(3);
            source.Reset([7, 8]);
            source.Add(9);
        }
        AssertCatchesUp(query, [7, 8, 9]);
    }

    [TestMethod]
    public void AContextQueryResetBehindALaterChangeTakesItOnce()
    {
        var source = new ObservableRangeCollection<int>([1, 2]);
        var synchronizationContext = new HeldSynchronizationContext();
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(source);
        using var query = observed.ObserveUsingSynchronizationContextEventually(synchronizationContext);
        synchronizationContext.RunHeld();
        source.Reset([7, 8]);
        source.Add(9);
        synchronizationContext.RunHeld();
        CollectionAssert.AreEqual(new[] { 7, 8, 9 }, query.ToList(), $"the query holds {string.Join(",", query)}");
    }

    [TestMethod]
    public void ADictionaryContextQueryResetBehindALaterChangeTakesItOnce()
    {
        var source = new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1 });
        var synchronizationContext = new HeldSynchronizationContext();
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyDictionary(source);
        using var query = observed.ObserveUsingSynchronizationContextEventually(synchronizationContext);
        synchronizationContext.RunHeld();
        source.Reset(new Dictionary<string, int> { ["b"] = 2 });
        source.Add("c", 3);
        synchronizationContext.RunHeld();
        CollectionAssert.AreEquivalent(new[] { "b=2", "c=3" }, query.Select(entry => $"{entry.Key}={entry.Value}").ToList(), $"the query holds {string.Join(",", query)}");
    }

    [TestMethod]
    public void ASyncRootQueryResetBehindALaterChangeTakesItOnce()
    {
        var source = new ObservableRangeCollection<int>([1, 2]);
        var syncRoot = new object();
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(source);
        using var query = observed.ObserveUsingSyncRootEventually(syncRoot);
        AssertCatchesUp(query, [1, 2]);
        lock (syncRoot)
        {
            source.Add(3);
            source.Reset([7, 8]);
            source.Add(9);
        }
        AssertCatchesUp(query, [7, 8, 9]);
    }
}
