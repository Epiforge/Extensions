namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// An invocation of a literal lambda under an operand whose evaluation is deferred, observed on the graph and then disposed of
/// </summary>
/// <remarks>
/// The graph's node for such an invocation evaluates its arguments to build the body, and an argument whose own evaluation was deferred announces when it is first evaluated. The node once subscribed to its arguments before building the body, so that announcement reached it part way through building the body and built it a second time, and the body was released only once when the observation was disposed of, leaving it in the observer's cache and attached to whatever it read
/// </remarks>
[TestClass]
public class DeferredInvocationRelease
{
    static ExpressionObserver Graph() =>
        new(new ExpressionObserverOptions { UseDirectSubscription = false });

    static InvocationExpression Negated(Expression argument)
    {
        var v = Expression.Parameter(typeof(int), "v");
        return Expression.Invoke(Expression.Lambda(Expression.Negate(v), v), argument);
    }

    [TestMethod]
    public void AnInvocationInABranchReleasesItsBody()
    {
        var s = Expression.Parameter(typeof(Recorded), "s");
        var score = Expression.Property(s, nameof(Recorded.Score));
        var lambda = Expression.Lambda<Func<Recorded, int>>(Expression.Condition(Expression.GreaterThan(score, Expression.Constant(0)), score, Negated(Expression.Property(s, nameof(Recorded.Rank)))), s);
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 3, Score = 5 };
        var observer = Graph();
        using (var expression = observer.Observe(lambda, subject))
        {
            Assert.AreEqual(5, expression.Evaluation.Result);
            subject.Score = 0;
            Assert.AreEqual(-3, expression.Evaluation.Result);
            subject.Rank = 4;
            Assert.AreEqual(-4, expression.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions, "the graph kept nodes after the observation was disposed");
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    [TestMethod]
    public void AnInvocationInAHandlerReleasesItsBody()
    {
        var s = Expression.Parameter(typeof(Recorded), "s");
        var body = Expression.Divide(Expression.Constant(10), Expression.Property(s, nameof(Recorded.Score)));
        var lambda = Expression.Lambda<Func<Recorded, int>>(Expression.TryCatch(body, Expression.Catch(typeof(Exception), Negated(Expression.Property(s, nameof(Recorded.Rank))))), s);
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 3, Score = 0 };
        var observer = Graph();
        using (var expression = observer.Observe(lambda, subject))
        {
            Assert.AreEqual(-3, expression.Evaluation.Result);
            subject.Rank = 4;
            Assert.AreEqual(-4, expression.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions, "the graph kept nodes after the observation was disposed");
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }
}
