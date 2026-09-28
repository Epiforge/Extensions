namespace Epiforge.Extensions.Comparisons.Tests;

/// <summary>
/// What happens to a filtered view, and to a handler of its announcements, when one thread changes an element while a handler is still handling the view's announcement of another thread's change
/// </summary>
/// <remarks>
/// The view keeps elements ranked above zero, of four ranked -1, -1, 1 and -1. One thread ranks the first element 5, and a handler of the view's announcements holds that thread inside the announcement; another thread then ranks the second and fourth 5 and the third -1. The handler, once let go, reads the view. A library which applies changes one at a time either keeps the second thread waiting until the handler returns or lets it go and applies its changes afterward, and either way the handler reads the view its announcement described; one which does not lets the second thread's changes into the view underneath the handler. A thread held on a lock cannot be told from one which is merely slow, so a second thread not finished two seconds into the hold is taken as waiting
/// </remarks>
[TestClass]
public class ChangesFromAnotherThread
{
    static readonly TimeSpan patience = TimeSpan.FromSeconds(10);
    static readonly Expression<Func<Person, bool>> positive = person => person.Rank > 0;
    static readonly Func<Person, bool> positiveDelegate = positive.Compile();
    static readonly TimeSpan whileHeld = TimeSpan.FromSeconds(2);

    sealed record Outcome(bool OtherFinishedWhileHeld, string HandlerRead, string Final);

    [TestMethod]
    public void DynamicDataKeepsTheOtherThreadWaitingUntilTheHandlerReturns()
    {
        var people = Person.Many(-1, -1, 1, -1);
        using var subscription = people.ToObservableChangeSet().AutoRefresh(person => person.Rank).Filter(positiveDelegate).Bind(out var view).Subscribe();
        var outcome = Race(people, view, () => Person.Names(view));
        Assert.IsFalse(outcome.OtherFinishedWhileHeld);
        Assert.AreEqual("P0,P2", outcome.HandlerRead);
        Assert.AreEqual("P0,P1,P3", outcome.Final);
    }

    [TestMethod]
    public void ExpressionsKeepsTheOtherThreadWaitingUntilTheHandlerReturns()
    {
        var people = Person.Many(-1, -1, 1, -1);
        using var observation = new CollectionObserver().ObserveReadOnlyList(people);
        using var view = observation.ObserveWhere(positive);
        var outcome = Race(people, view, () => Person.Names(view));
        Assert.IsFalse(outcome.OtherFinishedWhileHeld);
        Assert.AreEqual("P0,P2", outcome.HandlerRead);
        Assert.AreEqual("P0,P1,P3", outcome.Final);
    }

    [TestMethod]
    public void NmfChangesTheViewUnderneathTheHandler()
    {
        var people = Person.Many(-1, -1, 1, -1);
        using var view = ((IEnumerable<Person>)people).WithUpdates().Where(positive);
        view.Successors.SetDummy();
        var outcome = Race(people, (INotifyCollectionChanged)view, () => Person.Names(view));
        Assert.IsTrue(outcome.OtherFinishedWhileHeld);
        Assert.AreEqual("P0,P1,P3", outcome.HandlerRead);
        Assert.AreEqual("P0,P1,P3", outcome.Final);
    }

    [TestMethod]
    public void ObservableComputationsLetsTheOtherThreadGoAndAppliesItsChangesOnceTheHandlerReturns()
    {
        var people = Person.Many(-1, -1, 1, -1);
        using var consumer = new OcConsumer();
        var view = people.Filtering(positive).For(consumer);
        var outcome = Race(people, view, () => Person.Names(view));
        Assert.IsTrue(outcome.OtherFinishedWhileHeld);
        Assert.AreEqual("P0,P2", outcome.HandlerRead);
        Assert.AreEqual("P0,P1,P3", outcome.Final);
    }

    static Outcome Race(ObservableCollection<Person> people, INotifyCollectionChanged view, Func<string> read)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var armed = 1;
        string? handlerRead = null;
        view.CollectionChanged += (sender, e) =>
        {
            if (Interlocked.Exchange(ref armed, 0) != 1)
                return;
            entered.Set();
            release.Wait();
            handlerRead = read();
        };
        var first = Task.Factory.StartNew(() => people[0].Rank = 5, TaskCreationOptions.LongRunning);
        Assert.IsTrue(entered.Wait(patience), "the first change was never announced");
        var other = Task.Factory.StartNew(() =>
        {
            people[1].Rank = 5;
            people[3].Rank = 5;
            people[2].Rank = -1;
        }, TaskCreationOptions.LongRunning);
        var otherFinishedWhileHeld = other.Wait(whileHeld);
        release.Set();
        Assert.IsTrue(first.Wait(patience), "the first change never finished");
        Assert.IsTrue(other.Wait(patience), "the other thread's changes never finished");
        return new(otherFinishedWhileHeld, handlerRead!, read());
    }
}
