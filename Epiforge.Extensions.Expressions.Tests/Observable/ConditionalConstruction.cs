namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// A conditional whose branches share a subexpression, constructed on the graph while its test is false
/// </summary>
/// <remarks>
/// An invocation evaluates its arguments when it is constructed, even under a branch whose evaluation is deferred, and an argument shared with the other branch then announces through that branch to the conditional before the conditional has finished constructing. The conditional once subscribed to each branch as it built it, so the announcement arrived while its false branch did not yet exist, and a test which was false read it
/// </remarks>
[TestClass]
public class ConditionalConstruction
{
    [TestMethod]
    public void ABranchSharingAnInvocationArgumentOfTheOtherBranch()
    {
        var s = Expression.Parameter(typeof(Recorded), "s");
        var v = Expression.Parameter(typeof(int), "v");
        var rank = Expression.Property(s, nameof(Recorded.Rank));
        var score = Expression.Property(s, nameof(Recorded.Score));
        var lambda = Expression.Lambda<Func<Recorded, int>>(Expression.Condition(Expression.GreaterThan(score, Expression.Constant(0)), Expression.Subtract(score, rank), Expression.Invoke(Expression.Lambda(Expression.Negate(v), v), rank)), s);
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 3, Score = 0 };
        var observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        using (var expression = observer.Observe(lambda, subject))
        {
            Assert.IsNull(expression.Evaluation.Fault);
            Assert.AreEqual(-3, expression.Evaluation.Result);
            subject.Score = 5;
            Assert.AreEqual(2, expression.Evaluation.Result);
            subject.Rank = 1;
            Assert.AreEqual(4, expression.Evaluation.Result);
            subject.Score = 0;
            Assert.AreEqual(-1, expression.Evaluation.Result);
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }
}
