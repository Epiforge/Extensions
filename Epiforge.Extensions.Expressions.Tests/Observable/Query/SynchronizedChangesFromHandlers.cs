namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// A change made to a sequence from within a handler of another query's announcement, which the readme promises reaches a query synchronized with a lock or a callback only once that announcement is over
/// </summary>
[TestClass]
public class SynchronizedChangesFromHandlers
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AChangeMadeWithinAHandlerOfAnotherQuerysAnnouncementReachesASynchronizedQueryOnceThatAnnouncementIsOver(bool throughACallback)
    {
        var syncRoot = new object();
        var observer = CollectionObserverHelpers.Create();
        var announcing = new ObservableRangeCollection<int>([1]);
        var changed = new ObservableRangeCollection<int>([1]);
        var observedAnnouncing = observer.ObserveReadOnlyList(announcing);
        var observedChanged = observer.ObserveReadOnlyList(changed);
        var synchronized = throughACallback
            ? observedChanged.ObserveUsingSynchronizationCallback(syncRoot, (collection, context, accessMethod, writeAccess) =>
            {
                lock (context)
                    accessMethod();
            })
            : observedChanged.ObserveUsingSyncRoot(syncRoot);
        int? countWithinTheAnnouncement = null;
        observedAnnouncing.CollectionChanged += (sender, e) =>
        {
            changed.Add(2);
            lock (syncRoot)
                countWithinTheAnnouncement = synchronized.Count;
        };
        announcing.Add(2);
        int countAfterTheAnnouncement;
        lock (syncRoot)
            countAfterTheAnnouncement = synchronized.Count;
        synchronized.Dispose();
        observedChanged.Dispose();
        observedAnnouncing.Dispose();
        Assert.AreEqual(1, countWithinTheAnnouncement, "the change reached the synchronized query while the announcement it was made within was still going on");
        Assert.AreEqual(2, countAfterTheAnnouncement, "the change never reached the synchronized query");
    }
}
