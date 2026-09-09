namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Pins what the graph does with a property whose change notifications the observer has been told to ignore, which is the premise a narrowing of the analyzer's refusal of them would rest on
/// </summary>
/// <remarks>
/// The analyzer refuses any expression reading such a property outright. If the graph attaches nothing for one, then planning nothing is agreement rather than approximation, and the refusal is conservatism which costs an application registering many ignored properties every expression that touches one
/// </remarks>
[TestClass]
public class IgnoredNotifications
{
    static readonly PropertyInfo rankProperty = typeof(Recorded).GetProperty(nameof(Recorded.Rank))!;

    static ExpressionObserverOptions Ignoring(bool useDirectSubscription)
    {
        var options = new ExpressionObserverOptions { UseDirectSubscription = useDirectSubscription };
        options.AddIgnoredPropertyChangeNotification(rankProperty);
        return options;
    }

    [TestMethod]
    public void AnIgnoredPropertyIsRefusedByTheAnalyzer()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = new ExpressionObserver(Ignoring(true));
        using (var expression = observer.Observe(s => s.Rank > 0, subject))
        {
            Assert.IsTrue(expression.Evaluation.Result);
            Assert.AreNotEqual(0, observer.CachedObservableExpressions, "the analyzer admitted a read of an ignored property");
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    public void TheGraphAttachesNothingForAnIgnoredProperty()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = new ExpressionObserver(Ignoring(false));
        using (var expression = observer.Observe(s => s.Rank > 0, subject))
        {
            Assert.IsTrue(expression.Evaluation.Result);
            CollectionAssert.AreEqual(Array.Empty<string>(), log.Attachments().ToArray(), $"the graph attached [{string.Join(", ", log.Attachments())}] for a property it was told to ignore");
            subject.Rank = -1;
            Assert.IsTrue(expression.Evaluation.Result, "the graph followed a property it was told to ignore");
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    public void TheGraphStillAttachesForAPropertyItWasNotToldToIgnore()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Score = 1 };
        var observer = new ExpressionObserver(Ignoring(false));
        using (var expression = observer.Observe(s => s.Score > 0, subject))
        {
            Assert.IsTrue(expression.Evaluation.Result);
            Assert.AreNotEqual(0, log.Attachments().Count, "the control attached nothing, so the test above proves nothing");
            subject.Score = -1;
            Assert.IsFalse(expression.Evaluation.Result);
        }
        Assert.AreEqual(0, log.Outstanding);
    }
}
