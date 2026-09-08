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

    static string Describe<TResult>(IObservableExpression<Recorded, TResult> expression) =>
        expression.Evaluation.Fault is { } fault ? $"!{fault.GetType().Name}" : expression.Evaluation.Result?.ToString() ?? "<null>";

    /// <summary>
    /// Runs one expression under both mechanisms over identical worlds, comparing after every step what each has evaluated to, which objects each is attached to, and how often each has announced; the number of handlers is not compared, the fast path keying a subscription by property name where the graph keys by event
    /// </summary>
    static void AssertMechanismsAgree<TResult>(Func<SubscriptionLog, (Recorded Subject, Expression<Func<Recorded, TResult>> Lambda)> build, params Action<Recorded>[] steps)
    {
        var graphLog = new SubscriptionLog();
        var (graphSubject, graphLambda) = build(graphLog);
        var graphObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        var fastLog = new SubscriptionLog();
        var (fastSubject, fastLambda) = build(fastLog);
        var fastObserver = new ExpressionObserver();
        using (var graphExpression = graphObserver.Observe(graphLambda, graphSubject))
        using (var fastExpression = fastObserver.Observe(fastLambda, fastSubject))
        {
            Assert.AreEqual(0, fastObserver.CachedObservableExpressions, $"the fast path declined {fastLambda} and fell back to the graph, which makes this comparison vacuous");
            var graphNotifications = 0;
            var fastNotifications = 0;
            graphExpression.PropertyChanged += (sender, e) => ++graphNotifications;
            fastExpression.PropertyChanged += (sender, e) => ++fastNotifications;
            for (var step = 0; step <= steps.Length; ++step)
            {
                if (step > 0)
                {
                    steps[step - 1](graphSubject);
                    steps[step - 1](fastSubject);
                }
                Assert.AreEqual(Describe(graphExpression), Describe(fastExpression), $"step {step}: evaluation diverged");
                CollectionAssert.AreEqual(graphLog.Attachments().Distinct().ToArray(), fastLog.Attachments().Distinct().ToArray(), $"step {step}: graph: [{string.Join(", ", graphLog.Attachments())}]; fast: [{string.Join(", ", fastLog.Attachments())}]");
                Assert.AreEqual(graphNotifications, fastNotifications, $"step {step}: notification count diverged");
            }
        }
        Assert.AreEqual(0, graphLog.Outstanding, "the graph did not detach everything it attached");
        Assert.AreEqual(0, fastLog.Outstanding, "the fast path did not detach everything it attached");
        Assert.AreEqual(0, graphObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void BothMechanismsFollowAChainAsItsIntermediateIsReplaced() =>
        AssertMechanismsAgree(log => (new Recorded(log) { Next = new Recorded(log) { Rank = 1 } }, (Expression<Func<Recorded, int>>)(s => s.Next!.Rank)),
            subject => subject.Next!.Rank = 5,
            subject => subject.Next = new Recorded(subject.Log) { Rank = 2 },
            subject => subject.Next!.Rank = 7,
            subject => subject.Next = null,
            subject => subject.Next = new Recorded(subject.Log) { Rank = 3 });

    [TestMethod]
    public void BothMechanismsFollowAThreeLinkChain() =>
        AssertMechanismsAgree(log => (new Recorded(log) { Next = new Recorded(log) { Next = new Recorded(log) { Rank = 1 } } }, (Expression<Func<Recorded, int>>)(s => s.Next!.Next!.Rank)),
            subject => subject.Next!.Next!.Rank = 4,
            subject => subject.Next!.Next = new Recorded(subject.Log) { Rank = 6 },
            subject => subject.Next = new Recorded(subject.Log) { Next = new Recorded(subject.Log) { Rank = 8 } },
            subject => subject.Next!.Next!.Rank = 9,
            subject => subject.Next!.Next = null);

    [TestMethod]
    public void BothMechanismsFollowAChainReadTwiceThroughOneIntermediate() =>
        AssertMechanismsAgree(log => (new Recorded(log) { Next = new Recorded(log) { Rank = 1, Score = 2 } }, (Expression<Func<Recorded, int>>)(s => s.Next!.Rank + s.Next!.Score)),
            subject => subject.Next!.Score = 5,
            subject => subject.Next = new Recorded(subject.Log) { Rank = 3, Score = 4 },
            subject => subject.Next!.Rank = 9);

    [TestMethod]
    public void BothMechanismsAttachToAChainOnlyWhenTheBranchReadingItIsTaken() =>
        AssertMechanismsAgree(log => (new Recorded(log) { Next = new Recorded(log) { Rank = 1 } }, (Expression<Func<Recorded, int>>)(s => s.Rank > 0 ? s.Next!.Rank : 0)),
            subject => subject.Rank = 1,
            subject => subject.Next = new Recorded(subject.Log) { Rank = 4 },
            subject => subject.Rank = 0);

    [TestMethod]
    public void BothMechanismsHoldOneSubscriptionPerObjectOfAChainReadAtTwoProperties()
    {
        var graphLog = new SubscriptionLog();
        var graphSubject = new Recorded(graphLog) { Next = new Recorded(graphLog) { Rank = 1, Score = 2 } };
        var graphObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        var fastLog = new SubscriptionLog();
        var fastSubject = new Recorded(fastLog) { Next = new Recorded(fastLog) { Rank = 1, Score = 2 } };
        var fastObserver = new ExpressionObserver();
        using (var graphExpression = graphObserver.Observe(s => s.Next!.Rank + s.Next!.Score, graphSubject))
        using (var fastExpression = fastObserver.Observe(s => s.Next!.Rank + s.Next!.Score, fastSubject))
        {
            Assert.AreEqual(0, fastObserver.CachedObservableExpressions, "the fast path declined the shape this test exists to price");
            Assert.AreEqual(3, graphExpression.Evaluation.Result);
            Assert.AreEqual(3, fastExpression.Evaluation.Result);
            Assert.AreEqual(2, graphLog.Outstanding, $"graph: [{string.Join(", ", graphLog.Attachments())}]");
            Assert.AreEqual(2, fastLog.Outstanding, $"the fast path held one subscription per property until it began sharing one registration per object and event as the graph does; fast: [{string.Join(", ", fastLog.Attachments())}]");
        }
        Assert.AreEqual(0, graphLog.Outstanding);
        Assert.AreEqual(0, fastLog.Outstanding);
        Assert.AreEqual(0, graphObserver.CachedObservableExpressions);
    }
}
