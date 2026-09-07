namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class NotifyingChainAttachment
{
    static string Describe(SubscriptionLog log) =>
        $"graph: [{string.Join(", ", log.Attachments())}]";

    static ExpressionObserver Graph() =>
        new(new ExpressionObserverOptions { UseDirectSubscription = false });

    [TestMethod]
    public void TheGraphAttachesToEveryLinkOfAChainItCanReach()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Next = new Recorded(log) { Rank = 1 } };
        var observer = Graph();
        using (var expr = observer.Observe(s => s.Next!.Rank, subject))
        {
            Assert.AreEqual(1, expr.Evaluation.Result);
            Assert.AreEqual(2, log.Outstanding, Describe(log));
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void TheGraphMovesItsSubscriptionWhenTheIntermediateIsReplaced()
    {
        var log = new SubscriptionLog();
        var first = new Recorded(log) { Rank = 1 };
        var subject = new Recorded(log) { Next = first };
        var observer = Graph();
        using (var expr = observer.Observe(s => s.Next!.Rank, subject))
        {
            Assert.AreEqual(1, expr.Evaluation.Result);
            Assert.AreEqual(2, log.Outstanding, Describe(log));
            var second = new Recorded(log) { Rank = 2 };
            subject.Next = second;
            Assert.AreEqual(2, expr.Evaluation.Result);
            Assert.AreEqual(2, log.Outstanding, $"the graph did not release the intermediate it left; {Describe(log)}");
            Assert.AreEqual(3, log.Attachments().Count, $"the graph did not attach to the intermediate it moved to; {Describe(log)}");
            first.Rank = 9;
            Assert.AreEqual(2, expr.Evaluation.Result, "the graph still hears from the intermediate it left");
            second.Rank = 3;
            Assert.AreEqual(3, expr.Evaluation.Result, "the graph does not hear from the intermediate it moved to");
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void TheGraphReleasesTheIntermediateWhenItBecomesNull()
    {
        var log = new SubscriptionLog();
        var first = new Recorded(log) { Rank = 1 };
        var subject = new Recorded(log) { Next = first };
        var observer = Graph();
        using (var expr = observer.Observe(s => s.Next!.Rank, subject))
        {
            Assert.AreEqual(2, log.Outstanding, Describe(log));
            subject.Next = null;
            Assert.IsInstanceOfType<NullReferenceException>(expr.Evaluation.Fault);
            Assert.AreEqual(1, log.Outstanding, $"the graph held its subscription to an intermediate the chain no longer reaches; {Describe(log)}");
            subject.Next = first;
            Assert.AreEqual(1, expr.Evaluation.Result);
            Assert.AreEqual(2, log.Outstanding, $"the graph did not attach again when the chain reached the intermediate a second time; {Describe(log)}");
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void TheGraphMovesTheTailWhenTheMiddleOfAThreeLinkChainIsReplaced()
    {
        var log = new SubscriptionLog();
        var tail = new Recorded(log) { Rank = 1 };
        var middle = new Recorded(log) { Next = tail };
        var subject = new Recorded(log) { Next = middle };
        var observer = Graph();
        using (var expr = observer.Observe(s => s.Next!.Next!.Rank, subject))
        {
            Assert.AreEqual(1, expr.Evaluation.Result);
            Assert.AreEqual(3, log.Outstanding, Describe(log));
            var newTail = new Recorded(log) { Rank = 2 };
            var newMiddle = new Recorded(log) { Next = newTail };
            subject.Next = newMiddle;
            Assert.AreEqual(2, expr.Evaluation.Result);
            Assert.AreEqual(3, log.Outstanding, $"the graph did not release both links it left; {Describe(log)}");
            tail.Rank = 9;
            Assert.AreEqual(2, expr.Evaluation.Result, "the graph still hears from the tail it left");
            newTail.Rank = 3;
            Assert.AreEqual(3, expr.Evaluation.Result, "the graph does not hear from the tail it moved to");
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void TheGraphKeepsOneHandlerWhereTwoChainsShareAnIntermediate()
    {
        var log = new SubscriptionLog();
        var shared = new Recorded(log) { Rank = 1, Score = 2 };
        var subject = new Recorded(log) { Next = shared };
        var observer = Graph();
        using (var expr = observer.Observe(s => s.Next!.Rank + s.Next!.Score, subject))
        {
            Assert.AreEqual(3, expr.Evaluation.Result);
            Assert.AreEqual(2, log.Outstanding, $"the graph placed more than one handler per object; {Describe(log)}");
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void TheFastPathStillRefusesAChainThroughSomethingWhichCanNotify()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Next = new Recorded(log) { Rank = 1 } };
        var analysis = new Epiforge.Extensions.Expressions.Observable.DirectSubscriptionAnalyzer().Analyze(((Expression<Func<Recorded, int>>)(s => s.Next!.Rank)).Body);
        Assert.IsFalse(analysis.IsEligible, analysis.ToString());
        Assert.AreEqual(DirectSubscriptionIneligibility.ChangeableMemberTarget, analysis.Ineligibility);
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(s => s.Next!.Rank, subject))
        {
            Assert.AreEqual(1, expr.Evaluation.Result);
            Assert.AreNotEqual(0, observer.CachedObservableExpressions, "the fast path took a shape this test exists to say it refuses");
        }
        Assert.AreEqual(0, log.Outstanding);
    }
}
