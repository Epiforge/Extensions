namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Holds an observation made by subscribing directly to the contract an observation made by a graph of nodes keeps, so that the two mechanisms cannot drift apart in what a consumer sees
/// </summary>
[TestClass]
public class ObservationContract
{
    static ExpressionObserver Create(bool useDirectSubscription) =>
        new(new ExpressionObserverOptions { UseDirectSubscription = useDirectSubscription });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EachRowObservesByTheMechanismItNames(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = Create(useDirectSubscription);
        using (observer.Observe(s => s.Rank * 2, subject))
            Assert.AreEqual(!useDirectSubscription, observer.CachedObservableExpressions > 0, "the row did not observe by the mechanism it names");
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnObservationIsDisposedFromTheMomentItsDisposalBegins(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = Create(useDirectSubscription);
        var observation = observer.Observe(s => s.Rank * 2, subject);
        var events = new List<string>();
        observation.Disposing += (sender, e) => events.Add($"disposing {ReferenceEquals(sender, observation)} {observation.IsDisposed}");
        observation.Disposed += (sender, e) => events.Add($"disposed {ReferenceEquals(sender, observation)} {observation.IsDisposed}");
        Assert.IsFalse(observation.IsDisposed);
        observation.Dispose();
        observation.Dispose();
        CollectionAssert.AreEqual(new[] { "disposing True True", "disposed True True" }, events, string.Join("; ", events));
        Assert.IsTrue(observation.IsDisposed);
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnObservationAnnouncesItsEvaluationChangingAndChangedAroundTheChange(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = Create(useDirectSubscription);
        using (var observation = observer.Observe(s => s.Rank * 2, subject))
        {
            var events = new List<string>();
            observation.PropertyChanging += (sender, e) => events.Add($"changing {ReferenceEquals(sender, observation)} {e.PropertyName} {observation.Evaluation.Result}");
            observation.PropertyChanged += (sender, e) => events.Add($"changed {ReferenceEquals(sender, observation)} {e.PropertyName} {observation.Evaluation.Result}");
            subject.Rank = 5;
            subject.Rank = 5;
            CollectionAssert.AreEqual(new[] { "changing True Evaluation 2", "changed True Evaluation 10" }, events, string.Join("; ", events));
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnObservationReportsWhatItLastAnnouncedUntilItAnnouncesAgain(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = Create(useDirectSubscription);
        using var first = observer.Observe(s => s.Rank * 2, subject);
        using var second = observer.Observe(s => s.Rank * 3, subject);
        var events = new List<string>();
        first.PropertyChanged += (sender, e) => events.Add($"first announced while second reported {second.Evaluation.Result}");
        second.PropertyChanged += (sender, e) => events.Add($"second announced while first reported {first.Evaluation.Result}");
        subject.Rank = 5;
        CollectionAssert.AreEqual(new[] { "first announced while second reported 3", "second announced while first reported 10" }, events, string.Join("; ", events));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnObservationAnnouncesNothingOnceDisposed(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = Create(useDirectSubscription);
        var observation = observer.Observe(s => s.Rank * 2, subject);
        var announcements = 0;
        observation.PropertyChanging += (sender, e) => ++announcements;
        observation.PropertyChanged += (sender, e) => ++announcements;
        observation.Dispose();
        subject.Rank = 5;
        Assert.AreEqual(0, announcements);
        Assert.AreEqual(2, observation.Evaluation.Result);
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AFaultRepeatedByAReevaluationIsNotAnnouncedAgain(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = Create(useDirectSubscription);
        using (var observation = observer.Observe(s => s.Tag!.Length + s.Rank, subject))
        {
            Assert.IsInstanceOfType<NullReferenceException>(observation.Evaluation.Fault);
            var announcements = 0;
            observation.PropertyChanged += (sender, e) => ++announcements;
            subject.Rank = 2;
            Assert.AreEqual(0, announcements, "a fault of the same type and message was announced as a change");
            Assert.IsInstanceOfType<NullReferenceException>(observation.Evaluation.Fault);
            subject.Tag = "ab";
            Assert.AreEqual(1, announcements);
            Assert.IsNull(observation.Evaluation.Fault);
            Assert.AreEqual(4, observation.Evaluation.Result);
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnObservationIdentifiesItsArgumentsAndObserver(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = Create(useDirectSubscription);
        using (var observation = observer.Observe(s => s.Rank * 2, subject))
        {
            Assert.AreSame(subject, observation.Argument);
            Assert.AreSame(observer, observation.Observer);
            var arguments = observation.Arguments;
            Assert.AreEqual(1, arguments.Count);
            Assert.AreSame(subject, arguments[0]);
            Assert.AreSame(arguments, observation.Arguments);
            Assert.IsTrue(observation.ToString()!.EndsWith(".Rank * 2)", StringComparison.Ordinal), observation.ToString());
        }
        Assert.AreEqual(0, log.Outstanding);
    }
}
