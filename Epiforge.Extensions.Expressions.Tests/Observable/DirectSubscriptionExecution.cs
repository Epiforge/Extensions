namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class DirectSubscriptionExecution
{
    [TestMethod]
    public void ChangeIsObservedAndAnnouncedOnce()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 3 };
        var observer = new ExpressionObserver();
        var announcements = 0;
        using (var expr = observer.Observe(s => s.Rank * 2, subject))
        {
            Assert.AreEqual(6, expr.Evaluation.Result);
            expr.PropertyChanged += (sender, e) => ++announcements;
            subject.Rank = 5;
            Assert.AreEqual(10, expr.Evaluation.Result);
            Assert.AreEqual(1, announcements);
            subject.Rank = 5;
            Assert.AreEqual(1, announcements);
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    public void DisablingTheOptionBuildsTheGraphInstead()
    {
        var subject = new Recorded(new SubscriptionLog()) { Rank = 3 };
        var observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        using (var expr = observer.Observe(s => s.Rank * 2, subject))
        {
            Assert.AreEqual(6, expr.Evaluation.Result);
            Assert.AreNotEqual(0, observer.CachedObservableExpressions);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void EligibleObservationCachesNoNodes()
    {
        var subject = new Recorded(new SubscriptionLog()) { Rank = 3 };
        var observer = new ExpressionObserver();
        using (observer.Observe(s => s.Rank * 2, subject))
            Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void FaultIsLocalizedToTheObservationWhichThrew()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 3 };
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(s => 10 / s.Rank, subject))
        {
            Assert.AreEqual(3, expr.Evaluation.Result);
            subject.Rank = 0;
            Assert.IsInstanceOfType<DivideByZeroException>(expr.Evaluation.Fault);
            subject.Rank = 2;
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.AreEqual(5, expr.Evaluation.Result);
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnUnchangedFaultIsNotAnnouncedAgain(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1, Score = 0 };
        var observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = useDirectSubscription });
        var announcements = 0;
        using (var expr = observer.Observe(s => s.Rank / s.Score, subject))
        {
            Assert.IsInstanceOfType<DivideByZeroException>(expr.Evaluation.Fault);
            expr.PropertyChanged += (sender, e) => ++announcements;
            subject.Rank = 2;
            subject.Rank = 3;
            Assert.AreEqual(0, announcements);
            subject.Score = 1;
            Assert.AreEqual(1, announcements);
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.AreEqual(3, expr.Evaluation.Result);
            subject.Score = 0;
            Assert.AreEqual(2, announcements);
            Assert.IsInstanceOfType<DivideByZeroException>(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    public void IneligibleExpressionStillBuildsTheGraph()
    {
        var subject = new Recorded(new SubscriptionLog()) { Rank = 3 };
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(s => s.Rank > 0 ? s.Rank : s.Score, subject))
        {
            Assert.AreEqual(3, expr.Evaluation.Result);
            Assert.AreNotEqual(0, observer.CachedObservableExpressions);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void ObservationsOfOneLambdaDoNotCrossTalk()
    {
        var log = new SubscriptionLog();
        var observer = new ExpressionObserver();
        var subjects = new Recorded[5];
        var observations = new IObservableExpression<Recorded, int>[5];
        var announcements = new int[5];
        for (var i = 0; i < 5; ++i)
        {
            subjects[i] = new Recorded(log) { Rank = i };
            observations[i] = observer.Observe(s => s.Rank * 2, subjects[i]);
        }
        for (var i = 0; i < 5; ++i)
        {
            var index = i;
            observations[i].PropertyChanged += (sender, e) => ++announcements[index];
        }
        subjects[2].Rank = 9;
        for (var i = 0; i < 5; ++i)
        {
            Assert.AreEqual(i == 2 ? 1 : 0, announcements[i]);
            Assert.AreEqual(i == 2 ? 18 : i * 2, observations[i].Evaluation.Result);
        }
        for (var i = 0; i < 5; ++i)
            observations[i].Dispose();
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    public void SharedClosureSourceIsSubscribedOnce()
    {
        var log = new SubscriptionLog();
        var threshold = new Recorded(log) { Rank = 2 };
        var observer = new ExpressionObserver();
        var subjects = new Recorded[5];
        var observations = new IObservableExpression<Recorded, bool>[5];
        for (var i = 0; i < 5; ++i)
        {
            subjects[i] = new Recorded(log) { Rank = i };
            observations[i] = observer.Observe(s => s.Rank > threshold.Rank, subjects[i]);
        }
        Assert.AreEqual(6, log.Attachments().Count);
        var announcements = 0;
        for (var i = 0; i < 5; ++i)
            observations[i].PropertyChanged += (sender, e) => ++announcements;
        threshold.Rank = 4;
        Assert.AreEqual(2, announcements);
        for (var i = 0; i < 5; ++i)
            observations[i].Dispose();
        Assert.AreEqual(0, log.Outstanding);
    }
}
