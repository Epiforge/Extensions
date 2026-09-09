namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// What each mechanism does with a property whose change notifications the observer has been told to ignore
/// </summary>
/// <remarks>
/// The graph's node for such a property subscribes to nothing, so it reads the property when it is first evaluated and keeps what it read for the life of the observation, however often the property changes afterwards. The analyzer refused every expression reading one, because a fast path with no value of its own would have read it afresh on every evaluation and reported a value the graph had stopped reporting. It is now admitted where nothing it is read through can change, and held: read once, kept, and no subscription planned for it, which is what the graph attaches
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

    static (object? Result, IReadOnlyList<string> Attachments) ReadThroughTheArgument(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = new ExpressionObserver(Ignoring(useDirectSubscription));
        using (var expression = observer.Observe(s => s.Rank > 0, subject))
        {
            Assert.IsTrue(expression.Evaluation.Result, "the two forms did not start alike");
            subject.Rank = -1;
            var reported = expression.Evaluation.Result;
            var attachments = log.Attachments();
            Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached before it was disposed of");
            return (reported, attachments);
        }
    }

    [TestMethod]
    public void AnIgnoredPropertyIsAdmittedWhenNothingItIsReadThroughCanChange()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = new ExpressionObserver(Ignoring(true));
        using (var expression = observer.Observe(s => s.Rank > 0, subject))
        {
            Assert.IsTrue(expression.Evaluation.Result);
            Assert.AreEqual(0, observer.CachedObservableExpressions, "the analyzer refused a read of an ignored property through the observation's own argument");
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    /// <summary>
    /// A read through something which can change, which the graph re-reads when that something moves and keeps between those moves, and which the fast path therefore still refuses
    /// </summary>
    [TestMethod]
    public void AnIgnoredPropertyIsRefusedWhenWhatItIsReadThroughCanChange()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1, Next = new Recorded(log) { Rank = 1 } };
        var observer = new ExpressionObserver(Ignoring(true));
        using (var expression = observer.Observe(s => s.Next!.Rank > 0, subject))
        {
            Assert.IsTrue(expression.Evaluation.Result);
            Assert.AreNotEqual(0, observer.CachedObservableExpressions, "the analyzer admitted a read of an ignored property through something which can change");
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    public void TheMechanismsAgreeAboutAnIgnoredProperty()
    {
        var graph = ReadThroughTheArgument(false);
        var fast = ReadThroughTheArgument(true);
        Assert.AreEqual(true, graph.Result, "the graph followed a property it was told to ignore");
        Assert.AreEqual(graph.Result, fast.Result, "the mechanisms report different values for a property they were told to ignore");
        CollectionAssert.AreEqual(graph.Attachments.ToArray(), fast.Attachments.ToArray(), $"the mechanisms attached differently: the graph [{string.Join(", ", graph.Attachments)}] and the fast path [{string.Join(", ", fast.Attachments)}]");
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

    /// <summary>
    /// The same shape on the fast path, which must keep what it read once rather than reading the property again when something else announces
    /// </summary>
    [TestMethod]
    public void TheFastPathKeepsWhatItReadOnceForAnIgnoredProperty()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1, Score = 1 };
        var observer = new ExpressionObserver(Ignoring(true));
        using (var expression = observer.Observe(s => s.Rank + s.Score, subject))
        {
            Assert.AreEqual(2, expression.Evaluation.Result);
            Assert.AreEqual(0, observer.CachedObservableExpressions, "the analyzer refused the shape this test exists to cover");
            subject.Rank = 100;
            Assert.AreEqual(2, expression.Evaluation.Result, "the fast path read a property it was told to ignore again");
            subject.Score = 2;
            Assert.AreEqual(3, expression.Evaluation.Result, "the fast path did not follow the property it was not told to ignore, or read the ignored one again when the other announced");
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    public void TheGraphKeepsWhatItReadOnceForAnIgnoredProperty()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1, Score = 1 };
        var observer = new ExpressionObserver(Ignoring(false));
        using (var expression = observer.Observe(s => s.Rank + s.Score, subject))
        {
            Assert.AreEqual(2, expression.Evaluation.Result);
            subject.Rank = 100;
            Assert.AreEqual(2, expression.Evaluation.Result, "the graph read a property it was told to ignore again");
            subject.Score = 2;
            Assert.AreEqual(3, expression.Evaluation.Result, "the graph did not follow the property it was not told to ignore, or read the ignored one again when the other announced");
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
