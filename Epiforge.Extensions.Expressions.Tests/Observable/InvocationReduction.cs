namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Pins the premise a widening of the analyzer to invocations would rest on: that reducing an invocation of a literal lambda yields an expression the graph observes the same way it observes the invocation
/// </summary>
/// <remarks>
/// Written before any change to the analyzer. The graph already reduces this shape in ObservableInvocationExpression, but it substitutes each argument's evaluated value while a reduction performed before analysis substitutes the argument expression itself, and those are not obviously the same subscription set. If they are not, the widening is an approximation rather than an agreement and should not be made
/// </remarks>
[TestClass]
public class InvocationReduction
{
    static readonly PropertyInfo rankProperty = typeof(Recorded).GetProperty(nameof(Recorded.Rank))!;

    static readonly Expression<Func<int, int>> absolute = value => Math.Abs(value);
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

    static Expression<Func<Recorded, bool>> Invocation(bool nested)
    {
        var subject = Expression.Parameter(typeof(Recorded), "s");
        Expression value = Expression.MakeMemberAccess(subject, rankProperty);
        if (nested)
            value = Expression.Invoke(negated, value);
        value = Expression.Invoke(absolute, value);
        return Expression.Lambda<Func<Recorded, bool>>(Expression.GreaterThan(value, Expression.Constant(0)), subject);
    }

    [TestMethod]
    public void TheGraphSubscribesAlikeForAnInvocationAndItsReduction() =>
        AssertAlike(Invocation(false), s => Math.Abs(s.Rank) > 0);

    [TestMethod]
    public void TheGraphSubscribesAlikeForNestedInvocationsAndTheirReduction() =>
        AssertAlike(Invocation(true), s => Math.Abs(-s.Rank) > 0);
}
