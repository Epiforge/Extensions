namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Expressions containing a try whose catch blocks give a value in place of a fault, which the graph evaluates like a short-circuiting operator: the body first, and a handler only once the body has faulted and that handler's block is the first whose type the fault is
/// </summary>
/// <remarks>
/// C# cannot write a try in an expression lambda, so these are built with the expression API, in the shape a formula engine emits for a function giving a fallback while its value is faulted: two blocks letting faults which concern the system rather than the data pass by rethrowing them, and a last block catching everything else
/// </remarks>
[TestClass]
public class TryExpressions
{
    public sealed class Calls
    {
        public int Fallbacks;

        public int Fallback(int value)
        {
            ++Fallbacks;
            return value;
        }

        public static int Raise(int kind) =>
            kind switch
            {
                1 => throw new ObjectDisposedException(nameof(Calls)),
                2 => throw new OperationCanceledException(),
                3 => throw new InvalidOperationException(),
                4 => throw new FormatException(),
                5 => throw new ArgumentOutOfRangeException(nameof(kind)),
                6 => throw new OverflowException(),
                _ => kind
            };
    }

    sealed class ParameterReplacer(ParameterExpression parameter, Expression replacement) :
        ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == parameter ? replacement : base.VisitParameter(node);
    }

    static readonly MethodInfo raise = typeof(Calls).GetMethod(nameof(Calls.Raise))!;

    static void AssertMechanism(ExpressionObserver observer, bool useDirectSubscription)
    {
        if (useDirectSubscription)
            Assert.AreEqual(0, observer.CachedObservableExpressions, "the fast path declined the shape this test exists to cover");
    }

    static Expression<Func<Recorded, T>> IfFault<T>(Expression<Func<Recorded, T>> value, Expression<Func<Recorded, T>> fallback)
    {
        var subject = value.Parameters[0];
        return Expression.Lambda<Func<Recorded, T>>(IfFault(value.Body, new ParameterReplacer(fallback.Parameters[0], subject).Visit(fallback.Body)), subject);
    }

    static TryExpression IfFault(Expression body, Expression fallback) =>
        Expression.TryCatch
        (
            body,
            Expression.Catch(typeof(OperationCanceledException), Expression.Rethrow(body.Type)),
            Expression.Catch(typeof(ObjectDisposedException), Expression.Rethrow(body.Type)),
            Expression.Catch(typeof(Exception), fallback)
        );

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AFaultedBodyGivesTheFallbackAndARecoveredBodyGivesItsValue(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 7, Score = 0 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expression = observer.Observe(IfFault<int>(s => 10 / s.Score, s => -s.Rank), subject))
        {
            AssertMechanism(observer, useDirectSubscription);
            var changes = 0;
            expression.PropertyChanged += (sender, e) => ++changes;
            Assert.IsNull(expression.Evaluation.Fault);
            Assert.AreEqual(-7, expression.Evaluation.Result);
            subject.Rank = 8;
            Assert.AreEqual(-8, expression.Evaluation.Result);
            subject.Score = 5;
            Assert.AreEqual(2, expression.Evaluation.Result);
            subject.Rank = 9;
            Assert.AreEqual(2, expression.Evaluation.Result);
            subject.Score = 0;
            Assert.AreEqual(-9, expression.Evaluation.Result);
            Assert.AreEqual(3, changes, "the observation announced a different number of changes than its value made");
        }
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ABodyWithAValueNeverEvaluatesTheFallback(bool useDirectSubscription)
    {
        var subject = new Recorded(new SubscriptionLog()) { Rank = 1, Score = 2 };
        var calls = new Calls();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(IfFault<int>(s => 10 / s.Score, s => calls.Fallback(s.Rank)), subject);
        AssertMechanism(observer, useDirectSubscription);
        Assert.AreEqual(5, expression.Evaluation.Result);
        subject.Score = 5;
        subject.Rank = 3;
        Assert.AreEqual(2, expression.Evaluation.Result);
        Assert.AreEqual(0, calls.Fallbacks, "the fallback was evaluated while the body had a value");
        subject.Score = 0;
        Assert.AreEqual(3, expression.Evaluation.Result);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SystemFaultsSpreadAndDataFaultsAreHandled(bool useDirectSubscription)
    {
        var subject = new Recorded(new SubscriptionLog()) { Score = 0 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(IfFault<int>(s => Calls.Raise(s.Score), s => -1), subject);
        AssertMechanism(observer, useDirectSubscription);
        Assert.AreEqual(0, expression.Evaluation.Result);
        subject.Score = 1;
        Assert.IsInstanceOfType<ObjectDisposedException>(expression.Evaluation.Fault);
        subject.Score = 2;
        Assert.IsInstanceOfType<OperationCanceledException>(expression.Evaluation.Fault);
        subject.Score = 3;
        Assert.IsNull(expression.Evaluation.Fault);
        Assert.AreEqual(-1, expression.Evaluation.Result);
        subject.Score = 1;
        Assert.IsInstanceOfType<ObjectDisposedException>(expression.Evaluation.Fault);
        subject.Score = 9;
        Assert.IsNull(expression.Evaluation.Fault);
        Assert.AreEqual(9, expression.Evaluation.Result);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AFaultInTheFallbackSpreads(bool useDirectSubscription)
    {
        var subject = new Recorded(new SubscriptionLog()) { Rank = 0, Score = 4 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(IfFault<int>(s => Calls.Raise(s.Score), s => 10 / s.Rank), subject);
        AssertMechanism(observer, useDirectSubscription);
        Assert.IsInstanceOfType<DivideByZeroException>(expression.Evaluation.Fault);
        subject.Rank = 5;
        Assert.IsNull(expression.Evaluation.Fault);
        Assert.AreEqual(2, expression.Evaluation.Result);
        subject.Score = 7;
        Assert.AreEqual(7, expression.Evaluation.Result);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ABodyWhoseFaultChangesTypeReselectsItsHandler(bool useDirectSubscription)
    {
        var s = Expression.Parameter(typeof(Recorded), "s");
        var lambda = Expression.Lambda<Func<Recorded, int>>
        (
            Expression.TryCatch
            (
                Expression.Call(raise, Expression.Property(s, nameof(Recorded.Score))),
                Expression.Catch(typeof(ArgumentException), Expression.Constant(-1)),
                Expression.Catch(typeof(ArithmeticException), Expression.Constant(-2))
            ),
            s
        );
        var subject = new Recorded(new SubscriptionLog()) { Score = 5 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(lambda, subject);
        AssertMechanism(observer, useDirectSubscription);
        Assert.AreEqual(-1, expression.Evaluation.Result);
        subject.Score = 6;
        Assert.AreEqual(-2, expression.Evaluation.Result);
        subject.Score = 4;
        Assert.IsInstanceOfType<FormatException>(expression.Evaluation.Fault, "a fault no block matches did not spread");
        subject.Score = 8;
        Assert.IsNull(expression.Evaluation.Fault);
        Assert.AreEqual(8, expression.Evaluation.Result);
    }

    /// <summary>
    /// A property of a value type read from what a try produced, after another operand has been evaluated, which the expression compiler refuses to arrange for itself
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void APropertyOfAHandledSpanReadAfterAnotherOperand(bool useDirectSubscription)
    {
        var s = Expression.Parameter(typeof(Recorded), "s");
        var span = Expression.Call(typeof(TimeSpan).GetMethod(nameof(TimeSpan.FromMinutes), [typeof(double)])!, Expression.Convert(Expression.Divide(Expression.Constant(60), Expression.Property(s, nameof(Recorded.Score))), typeof(double)));
        var handled = Expression.TryCatch(span, Expression.Catch(typeof(Exception), Expression.Constant(TimeSpan.FromMinutes(1))));
        var lambda = Expression.Lambda<Func<Recorded, int>>(Expression.Add(Expression.Property(s, nameof(Recorded.Rank)), Expression.Property(handled, nameof(TimeSpan.Minutes))), s);
        var subject = new Recorded(new SubscriptionLog()) { Rank = 100, Score = 0 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(lambda, subject);
        AssertMechanism(observer, useDirectSubscription);
        Assert.AreEqual(101, expression.Evaluation.Result);
        subject.Score = 4;
        Assert.AreEqual(115, expression.Evaluation.Result);
    }

    /// <summary>
    /// The shape a formula engine's fallback takes around its data: a held reading whose value the observer disposes of, inside an invoked literal lambda, inside the try
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AHeldReadingInsideTheBodyIsMadeOnceAndDisposedOnce(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Score = 0 };
        var source = new HeldValueTypeOperands.ReadingSource();
        Expression<Func<TimeSpan, int>> perMinute = span => 600 / span.Minutes;
        Expression<Func<Recorded, TimeSpan>> reading = s => (TimeSpan)source.Score(s).Span!;
        var lambda = Expression.Lambda<Func<Recorded, int>>(IfFault(Expression.Invoke(perMinute, reading.Body), Expression.Constant(-1)), reading.Parameters[0]);
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expression = observer.Observe(lambda, subject))
        {
            AssertMechanism(observer, useDirectSubscription);
            Assert.AreEqual(-1, expression.Evaluation.Result);
            subject.Score = 30;
            Assert.AreEqual(20, expression.Evaluation.Result);
            subject.Score = 0;
            Assert.AreEqual(-1, expression.Evaluation.Result);
        }
        Assert.AreEqual(1, source.Opened, "the observation did not make exactly one reading");
        Assert.AreEqual(1, source.Closed, "the observation did not dispose of its reading exactly once");
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AHandlerTakesItsSubscriptionsWhenAFaultFirstSelectsIt(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var other = new Recorded(log) { Rank = 3 };
        var subject = new Recorded(log) { Score = 2 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expression = observer.Observe(IfFault<int>(s => 10 / s.Score, s => other.Rank), subject))
        {
            AssertMechanism(observer, useDirectSubscription);
            Assert.AreEqual(1, log.Attachments().Count, "the observation attached to the handler before the body faulted");
            subject.Score = 0;
            Assert.AreEqual(3, expression.Evaluation.Result);
            Assert.AreEqual(2, log.Attachments().Count, "the observation did not attach to the handler once the body faulted");
            other.Rank = 4;
            Assert.AreEqual(4, expression.Evaluation.Result);
        }
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ATryInABranchNotYetTaken(bool useDirectSubscription)
    {
        var s = Expression.Parameter(typeof(Recorded), "s");
        var body = Expression.Divide(Expression.Constant(10), Expression.Property(s, nameof(Recorded.Score)));
        var lambda = Expression.Lambda<Func<Recorded, int>>(Expression.Condition(Expression.GreaterThan(Expression.Property(s, nameof(Recorded.Rank)), Expression.Constant(0)), IfFault(body, Expression.Constant(-1)), Expression.Constant(0)), s);
        var subject = new Recorded(new SubscriptionLog()) { Rank = 0, Score = 0 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(lambda, subject);
        AssertMechanism(observer, useDirectSubscription);
        Assert.AreEqual(0, expression.Evaluation.Result);
        subject.Rank = 1;
        Assert.AreEqual(-1, expression.Evaluation.Result);
        subject.Score = 2;
        Assert.AreEqual(5, expression.Evaluation.Result);
        subject.Rank = 0;
        Assert.AreEqual(0, expression.Evaluation.Result);
    }

    [TestMethod]
    public void TheGraphSharesOneNodeBetweenObservationsOfOneTry()
    {
        var subject = new Recorded(new SubscriptionLog()) { Rank = 7, Score = 0 };
        var observer = ExpressionObserverHelpers.Create(false);
        var lambda = IfFault<int>(s => 10 / s.Score, s => -s.Rank);
        var first = observer.Observe(lambda, subject);
        var cached = observer.CachedObservableExpressions;
        using (var second = observer.Observe(IfFault<int>(s => 10 / s.Score, s => -s.Rank), subject))
        {
            Assert.AreEqual(cached, observer.CachedObservableExpressions, "a second observation of an equal try built nodes of its own");
            first.Dispose();
            subject.Score = 5;
            Assert.AreEqual(2, second.Evaluation.Result);
            subject.Score = 0;
            Assert.AreEqual(-7, second.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WhatHasNoMeaningInAnObservationIsRefused(bool useDirectSubscription)
    {
        var s = Expression.Parameter(typeof(Recorded), "s");
        var body = Expression.Property(s, nameof(Recorded.Score));
        TryExpression[] refused =
        [
            Expression.TryCatchFinally(body, Expression.Empty(), Expression.Catch(typeof(Exception), Expression.Constant(0))),
            Expression.TryFinally(body, Expression.Empty()),
            Expression.TryFault(body, Expression.Empty()),
            Expression.TryCatch(body, Expression.Catch(Expression.Variable(typeof(Exception), "fault"), Expression.Constant(0))),
            Expression.TryCatch(body, Expression.Catch(typeof(Exception), Expression.Constant(0), Expression.Constant(true))),
            Expression.TryCatch(body, Expression.Catch(typeof(Exception), Expression.Condition(Expression.Constant(true), Expression.Rethrow(typeof(int)), Expression.Constant(0))))
        ];
        foreach (var tryExpression in refused)
        {
            var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
            Assert.ThrowsExactly<NotSupportedException>(() => observer.Observe(Expression.Lambda<Func<Recorded, int>>(tryExpression, s), new Recorded(new SubscriptionLog())));
            Assert.AreEqual(0, observer.CachedObservableExpressions);
        }
    }
}
