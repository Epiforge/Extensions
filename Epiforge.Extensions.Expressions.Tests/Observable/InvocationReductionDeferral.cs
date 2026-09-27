namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Invocations of literal lambdas whose parameters are read where the body might not evaluate them, which reducing the invocation would change the meaning of
/// </summary>
/// <remarks>
/// An invocation evaluates its arguments before its body, and so does the graph, whose node for one builds the body only once no argument has faulted. Reducing such an invocation substitutes each argument where its parameter is read, which moves an argument read only in a deferred operand to where it may never be evaluated, and one read inside a try to where the try catches its fault. Either way a fault the argument raised would go unreported by the fast path alone. An argument which is a constant or the argument of the observation can raise nothing, so reducing it where it is read changes nothing
/// </remarks>
[TestClass]
public class InvocationReductionDeferral
{
    static void AssertTheGraphWasBuilt(ExpressionObserver observer, bool useDirectSubscription)
    {
        if (useDirectSubscription)
            Assert.AreNotEqual(0, observer.CachedObservableExpressions, "the fast path reduced an invocation whose argument it would have moved");
    }

    static Expression<Func<Recorded, T>> InvokeWithSubject<T>(LambdaExpression lambda, Func<ParameterExpression, Expression[]> arguments)
    {
        var s = Expression.Parameter(typeof(Recorded), "s");
        return Expression.Lambda<Func<Recorded, T>>(Expression.Invoke(lambda, arguments(s)), s);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnArgumentReadOnlyInABranchStillFaults(bool useDirectSubscription)
    {
        Expression<Func<bool, int, int>> pick = (c, x) => c ? x : 0;
        var lambda = InvokeWithSubject<int>(pick, s => [Expression.GreaterThan(Expression.Property(s, nameof(Recorded.Rank)), Expression.Constant(0)), Expression.Divide(Expression.Constant(10), Expression.Property(s, nameof(Recorded.Score)))]);
        var subject = new Recorded(new SubscriptionLog()) { Rank = 0, Score = 0 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(lambda, subject);
        AssertTheGraphWasBuilt(observer, useDirectSubscription);
        Assert.IsInstanceOfType<DivideByZeroException>(expression.Evaluation.Fault);
        subject.Score = 5;
        Assert.IsNull(expression.Evaluation.Fault);
        Assert.AreEqual(0, expression.Evaluation.Result);
        subject.Rank = 1;
        Assert.AreEqual(2, expression.Evaluation.Result);
        subject.Score = 0;
        Assert.IsInstanceOfType<DivideByZeroException>(expression.Evaluation.Fault);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnArgumentReadOnlyAfterAndAlsoStillFaults(bool useDirectSubscription)
    {
        Expression<Func<bool, bool, bool>> both = (c, x) => c && x;
        var lambda = InvokeWithSubject<bool>(both, s => [Expression.GreaterThan(Expression.Property(s, nameof(Recorded.Rank)), Expression.Constant(0)), Expression.GreaterThan(Expression.Divide(Expression.Constant(10), Expression.Property(s, nameof(Recorded.Score))), Expression.Constant(1))]);
        var subject = new Recorded(new SubscriptionLog()) { Rank = 0, Score = 0 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(lambda, subject);
        AssertTheGraphWasBuilt(observer, useDirectSubscription);
        Assert.IsInstanceOfType<DivideByZeroException>(expression.Evaluation.Fault);
        subject.Score = 5;
        Assert.IsNull(expression.Evaluation.Fault);
        Assert.IsFalse(expression.Evaluation.Result);
        subject.Rank = 1;
        Assert.IsTrue(expression.Evaluation.Result);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnArgumentReadOnlyAfterCoalesceStillFaults(bool useDirectSubscription)
    {
        Expression<Func<string?, string?, string?>> first = (t, u) => t ?? u;
        var lambda = InvokeWithSubject<string?>(first, s => [Expression.Property(s, nameof(Recorded.Tag)), Expression.Property(Expression.Property(s, nameof(Recorded.Next)), nameof(Recorded.Tag))]);
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Tag = "tag" };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(lambda, subject);
        AssertTheGraphWasBuilt(observer, useDirectSubscription);
        Assert.IsInstanceOfType<NullReferenceException>(expression.Evaluation.Fault);
        subject.Next = new Recorded(log) { Tag = "next" };
        Assert.IsNull(expression.Evaluation.Fault);
        Assert.AreEqual("tag", expression.Evaluation.Result);
        subject.Tag = null;
        Assert.AreEqual("next", expression.Evaluation.Result);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnArgumentIsNotMovedIntoATry(bool useDirectSubscription)
    {
        var v = Expression.Parameter(typeof(int), "v");
        var guard = Expression.Lambda(Expression.TryCatch(v, Expression.Catch(typeof(Exception), Expression.Constant(-1))), v);
        var lambda = InvokeWithSubject<int>(guard, s => [Expression.Divide(Expression.Constant(10), Expression.Property(s, nameof(Recorded.Score)))]);
        var subject = new Recorded(new SubscriptionLog()) { Score = 0 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(lambda, subject);
        AssertTheGraphWasBuilt(observer, useDirectSubscription);
        Assert.IsInstanceOfType<DivideByZeroException>(expression.Evaluation.Fault, "the try caught a fault its argument raised before the try was entered");
        subject.Score = 5;
        Assert.IsNull(expression.Evaluation.Fault);
        Assert.AreEqual(2, expression.Evaluation.Result);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ArgumentsWhichCannotFaultAreStillReducedWhereverTheyAreRead(bool useDirectSubscription)
    {
        Expression<Func<bool, int, Recorded, int>> pick = (c, x, r) => c ? x + r.Rank : 0;
        var lambda = InvokeWithSubject<int>(pick, s => [Expression.GreaterThan(Expression.Property(s, nameof(Recorded.Score)), Expression.Constant(0)), Expression.Constant(7), s]);
        var subject = new Recorded(new SubscriptionLog()) { Rank = 3, Score = 0 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(lambda, subject);
        if (useDirectSubscription)
            Assert.AreEqual(0, observer.CachedObservableExpressions, "the fast path declined an invocation whose arguments cannot fault");
        Assert.AreEqual(0, expression.Evaluation.Result);
        subject.Score = 1;
        Assert.AreEqual(10, expression.Evaluation.Result);
        subject.Rank = 4;
        Assert.AreEqual(11, expression.Evaluation.Result);
    }
}
