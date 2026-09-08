namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class SharedRegistration
{
    [TestMethod]
    public void BothMechanismsRegisterOnceForAnObjectReadAtTwoProperties()
    {
        var graphLog = new SubscriptionLog();
        var graphSubject = new Recorded(graphLog);
        var graphObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        var fastLog = new SubscriptionLog();
        var fastSubject = new Recorded(fastLog);
        var fastObserver = new ExpressionObserver();
        using (graphObserver.Observe(subject => subject.Rank + subject.Score, graphSubject))
        using (fastObserver.Observe(subject => subject.Rank + subject.Score, fastSubject))
        {
            Assert.AreEqual(1, graphLog.Attachments().Count, $"graph: [{string.Join(", ", graphLog.Attachments())}]");
            Assert.AreEqual(1, fastLog.Attachments().Count, $"fast: [{string.Join(", ", fastLog.Attachments())}]");
        }
        Assert.AreEqual(0, graphLog.Outstanding, "the graph did not detach everything it attached");
        Assert.AreEqual(0, fastLog.Outstanding, "the fast path did not detach everything it attached");
        Assert.AreEqual(0, graphObserver.CachedObservableExpressions);
        Assert.AreEqual(0, fastObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void EitherPropertyOfASharedRegistrationStillNotifies()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1, Score = 2 };
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(subject => subject.Rank + subject.Score, subject))
        {
            Assert.AreEqual(1, log.Attachments().Count, $"fast: [{string.Join(", ", log.Attachments())}]");
            Assert.AreEqual(3, expr.Evaluation.Result);
            subject.Rank = 10;
            Assert.AreEqual(12, expr.Evaluation.Result);
            subject.Score = 20;
            Assert.AreEqual(30, expr.Evaluation.Result);
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void AChangeToAPropertyNoSubscriptionNamedAnnouncesNothing()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1, Score = 2 };
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(subject => subject.Rank * 2, subject))
        {
            var announcements = 0;
            void propertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(IObservableExpression<object?>.Evaluation))
                    ++announcements;
            }

            expr.PropertyChanged += propertyChanged;
            subject.Score = 20;
            Assert.AreEqual(0, announcements, "the fast path announced a change to a property no subscription named");
            subject.Rank = 3;
            Assert.AreEqual(1, announcements, "the fast path did not announce a change to the property its subscription named");
            expr.PropertyChanged -= propertyChanged;
            Assert.AreEqual(6, expr.Evaluation.Result);
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }
}
