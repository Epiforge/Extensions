namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Covers the reduction of invocations of literal lambdas, which is what lets the analyzer see expressions it would otherwise refuse whole
/// </summary>
/// <remarks>
/// The first two tests were written before the reduction existed, to settle whether the graph observes an invocation the same way it observes the expression that invocation reduces to. They are kept because that premise is what the rest of this rests on and it should fail loudly if it ever stops holding
/// </remarks>
[TestClass]
public class InvocationReduction
{
    static readonly PropertyInfo rankProperty = typeof(Recorded).GetProperty(nameof(Recorded.Rank))!;

    static readonly Expression<Func<int, int>> absolute = value => Math.Abs(value);
    static readonly Expression<Func<int, int>> doubled = value => value + value;
    static readonly Expression<Func<int, int>> negated = value => -value;

    static void AssertAlike(Expression<Func<Recorded, bool>> invocationForm, Expression<Func<Recorded, bool>> reducedForm)
    {
        var invokedLog = new SubscriptionLog();
        var invokedSubject = new Recorded(invokedLog) { Rank = 0 };
        var reducedLog = new SubscriptionLog();
        var reducedSubject = new Recorded(reducedLog) { Rank = 0 };
        var observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        var invokedAnnouncements = 0;
        var reducedAnnouncements = 0;
        using (var invoked = observer.Observe(invocationForm, invokedSubject))
        using (var reduced = observer.Observe(reducedForm, reducedSubject))
        {
            invoked.PropertyChanged += (sender, e) => ++invokedAnnouncements;
            reduced.PropertyChanged += (sender, e) => ++reducedAnnouncements;
            Assert.AreEqual(reduced.Evaluation.Result, invoked.Evaluation.Result, "the two forms did not agree before anything changed");
            CollectionAssert.AreEqual(reducedLog.Attachments().ToArray(), invokedLog.Attachments().ToArray(), $"reduced: [{string.Join(", ", reducedLog.Attachments())}]; invoked: [{string.Join(", ", invokedLog.Attachments())}]");
            invokedSubject.Rank = -5;
            reducedSubject.Rank = -5;
            Assert.AreEqual(reduced.Evaluation.Result, invoked.Evaluation.Result, "the two forms did not agree after the change");
            Assert.AreEqual(reducedAnnouncements, invokedAnnouncements, "the two forms did not announce alike");
            CollectionAssert.AreEqual(reducedLog.Attachments().ToArray(), invokedLog.Attachments().ToArray(), "after the change");
        }
        Assert.AreEqual(0, invokedLog.Outstanding, "the invocation did not detach everything it attached");
        Assert.AreEqual(0, reducedLog.Outstanding, "the reduction did not detach everything it attached");
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    /// <summary>
    /// Runs one expression on both mechanisms and requires them to agree on the value, on what they subscribe to and on how often they announce
    /// </summary>
    /// <remarks>
    /// The check on the expression cache is what tells the two rows apart: a direct observation is not cached, so a shape the analyzer quietly refused would otherwise pass this by comparing the graph against itself
    /// </remarks>
    static void AssertMechanismsAgree(Expression<Func<Recorded, bool>> form, bool servedByTheFastPath)
    {
        var graphLog = new SubscriptionLog();
        var graphSubject = new Recorded(graphLog) { Rank = 0 };
        var fastLog = new SubscriptionLog();
        var fastSubject = new Recorded(fastLog) { Rank = 0 };
        var graphObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        var fastObserver = new ExpressionObserver();
        var graphAnnouncements = 0;
        var fastAnnouncements = 0;
        using (var graph = graphObserver.Observe(form, graphSubject))
        using (var fast = fastObserver.Observe(form, fastSubject))
        {
            graph.PropertyChanged += (sender, e) => ++graphAnnouncements;
            fast.PropertyChanged += (sender, e) => ++fastAnnouncements;
            if (servedByTheFastPath)
                Assert.AreEqual(0, fastObserver.CachedObservableExpressions, "the analyzer refused the shape this test exists to cover");
            else
                Assert.AreNotEqual(0, fastObserver.CachedObservableExpressions, "the analyzer admitted a shape this test expects it to refuse");
            Assert.AreEqual(graph.Evaluation.Result, fast.Evaluation.Result, "the mechanisms did not agree before anything changed");
            CollectionAssert.AreEqual(graphLog.Attachments().ToArray(), fastLog.Attachments().ToArray(), $"graph: [{string.Join(", ", graphLog.Attachments())}]; fast: [{string.Join(", ", fastLog.Attachments())}]");
            graphSubject.Rank = -5;
            fastSubject.Rank = -5;
            Assert.AreEqual(graph.Evaluation.Result, fast.Evaluation.Result, "the mechanisms did not agree after the change");
            Assert.AreEqual(graphAnnouncements, fastAnnouncements, "the mechanisms did not announce alike");
            CollectionAssert.AreEqual(graphLog.Attachments().ToArray(), fastLog.Attachments().ToArray(), "after the change");
        }
        Assert.AreEqual(0, graphLog.Outstanding, "the graph did not detach everything it attached");
        Assert.AreEqual(0, fastLog.Outstanding, "the fast path did not detach everything it attached");
    }

    static Expression<Func<Recorded, bool>> Invocation(Expression operand)
    {
        var subject = Expression.Parameter(typeof(Recorded), "s");
        return Expression.Lambda<Func<Recorded, bool>>(Expression.GreaterThan(Expression.Invoke(operand, Expression.MakeMemberAccess(subject, rankProperty)), Expression.Constant(0)), subject);
    }

    static Expression<Func<Recorded, bool>> NestedInvocation()
    {
        var subject = Expression.Parameter(typeof(Recorded), "s");
        return Expression.Lambda<Func<Recorded, bool>>(Expression.GreaterThan(Expression.Invoke(absolute, Expression.Invoke(negated, Expression.MakeMemberAccess(subject, rankProperty))), Expression.Constant(0)), subject);
    }

    [TestMethod]
    public void AnInvocationOfADelegateIsRefused() =>
        AssertMechanismsAgree(Invocation(Expression.Constant((Func<int, int>)Math.Abs)), false);

    [TestMethod]
    public void AnInvocationOfALiteralLambdaIsServedByTheFastPath() =>
        AssertMechanismsAgree(Invocation(absolute), true);

    [TestMethod]
    public void AnInvocationWhoseParameterIsReadTwiceIsRefused() =>
        AssertMechanismsAgree(Invocation(doubled), false);

    [TestMethod]
    public void NestedInvocationsAreServedByTheFastPath() =>
        AssertMechanismsAgree(NestedInvocation(), true);

    [TestMethod]
    public void TheGraphSubscribesAlikeForAnInvocationAndItsReduction() =>
        AssertAlike(Invocation(absolute), s => Math.Abs(s.Rank) > 0);

    [TestMethod]
    public void TheGraphSubscribesAlikeForNestedInvocationsAndTheirReduction() =>
        AssertAlike(NestedInvocation(), s => Math.Abs(-s.Rank) > 0);
}
