namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Holds what an object observed by several observations sees of their registrations with it, and what each observation sees of the others, as observations of the object join and leave
/// </summary>
[TestClass]
public class SourceRegistration
{
    static ExpressionObserver Create(bool useDirectSubscription) =>
        new(new ExpressionObserverOptions { UseDirectSubscription = useDirectSubscription });

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference ObserveAndLeave(ExpressionObserver observer, Recorded subject, List<IDisposable> remaining)
    {
        var leaving = observer.Observe(s => s.Rank * 2, subject);
        remaining.Add(observer.Observe(s => s.Rank * 3, subject));
        leaving.Dispose();
        return new WeakReference(leaving);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnObjectIsRegisteredWithOnceWhileObservationsOfItJoinAndLeave(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = Create(useDirectSubscription);
        var first = observer.Observe(s => s.Rank * 2, subject);
        var second = observer.Observe(s => s.Rank * 3, subject);
        first.Dispose();
        var third = observer.Observe(s => s.Rank * 5, subject);
        Assert.AreEqual(1, log.Attachments().Count, $"the object was registered with more than once: [{string.Join(", ", log.Attachments())}]");
        Assert.AreEqual(1, log.Outstanding);
        subject.Rank = 2;
        Assert.AreEqual(6, second.Evaluation.Result);
        Assert.AreEqual(10, third.Evaluation.Result);
        Assert.AreEqual(2, first.Evaluation.Result);
        second.Dispose();
        subject.Rank = 3;
        Assert.AreEqual(15, third.Evaluation.Result);
        Assert.AreEqual(1, log.Attachments().Count, $"the object was registered with more than once: [{string.Join(", ", log.Attachments())}]");
        third.Dispose();
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnObjectObservedAgainOnceEveryObservationLeftIsRegisteredWithAfresh(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = Create(useDirectSubscription);
        observer.Observe(s => s.Rank * 2, subject).Dispose();
        Assert.AreEqual(0, log.Outstanding);
        using (var again = observer.Observe(s => s.Rank * 3, subject))
        {
            Assert.AreEqual(2, log.Attachments().Count, $"[{string.Join(", ", log.Attachments())}]");
            Assert.AreEqual(1, log.Outstanding);
            subject.Rank = 4;
            Assert.AreEqual(12, again.Evaluation.Result);
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ObservationsOfOneObjectAreNotifiedInTheOrderTheyJoined(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = Create(useDirectSubscription);
        var leaving = observer.Observe(s => s.Rank * 2, subject);
        using var second = observer.Observe(s => s.Rank * 3, subject);
        leaving.Dispose();
        using var third = observer.Observe(s => s.Rank * 5, subject);
        using var fourth = observer.Observe(s => s.Rank * 7, subject);
        var order = new List<string>();
        second.PropertyChanged += (sender, e) => order.Add("second");
        third.PropertyChanged += (sender, e) => order.Add("third");
        fourth.PropertyChanged += (sender, e) => order.Add("fourth");
        subject.Rank = 2;
        CollectionAssert.AreEqual(new[] { "second", "third", "fourth" }, order, string.Join(", ", order));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnObservationWhichLeftASharedObjectIsNotKeptAlive(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = Create(useDirectSubscription);
        var remaining = new List<IDisposable>();
        var left = ObserveAndLeave(observer, subject, remaining);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.IsFalse(left.IsAlive, "an observation which left was kept alive while another observation of the same object remained");
        remaining[0].Dispose();
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ACollectionObservedByTwoObservationsIsRegisteredWithOnce(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var items = new RecordedCollection(log);
        var subject = new Recorded(log);
        var observer = Create(useDirectSubscription);
        var first = observer.Observe(s => items, subject);
        using var second = observer.Observe(s => items, subject);
        var firstAnnouncements = 0;
        var secondAnnouncements = 0;
        first.PropertyChanged += (sender, e) => ++firstAnnouncements;
        second.PropertyChanged += (sender, e) => ++secondAnnouncements;
        items.Announce();
        Assert.AreEqual(1, firstAnnouncements);
        Assert.AreEqual(1, secondAnnouncements);
        first.Dispose();
        items.Announce();
        Assert.AreEqual(1, firstAnnouncements);
        Assert.AreEqual(2, secondAnnouncements);
        Assert.AreEqual(1, log.Attachments().Count, $"[{string.Join(", ", log.Attachments())}]");
        Assert.AreEqual(1, log.Outstanding);
        second.Dispose();
        Assert.AreEqual(0, log.Outstanding);
    }
}
