namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Expressions which read a property of a value produced by a held subexpression, or pass one to an operator taking its operands by reference, in a position where another operand has already been evaluated
/// </summary>
/// <remarks>
/// The fast path resolves a held subexpression inside a try, and the expression compiler moves the operands evaluated before a try into temporaries. It refuses to do that for a property read whose instance is a value type other than a primitive and for an operator taking its operands by reference, so such an expression once threw when it was observed rather than producing a value or a fault. The graph is unaffected and is the model here; the controls are reads the compiler accepts in the same position, a field and an instance call
/// </remarks>
[TestClass]
public class HeldValueTypeOperands
{
    public readonly struct Minutes(long value)
    {
        public readonly long Value = value;

        public static Minutes operator %(in Minutes dividend, in Minutes divisor) =>
            new(dividend.Value % divisor.Value);

        public static Minutes operator -(in Minutes minutes) =>
            new(-minutes.Value);
    }

    public sealed class Reading :
        IDisposable,
        INotifyPropertyChanged
    {
        internal Reading(ReadingSource source, Recorded subject, bool score)
        {
            this.score = score;
            this.source = source;
            this.subject = subject;
            subject.PropertyChanged += SubjectPropertyChanged;
        }

        readonly bool score;
        readonly ReadingSource source;
        readonly Recorded subject;

        public object? Count =>
            new Minutes(Read());

        public object? Span =>
            TimeSpan.FromMinutes(Read());

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Dispose()
        {
            subject.PropertyChanged -= SubjectPropertyChanged;
            PropertyChanged = null;
            ++source.Closed;
        }

        int Read() =>
            score ? subject.Score : subject.Rank;

        void SubjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Span)));
        }
    }

    public sealed class ReadingSource
    {
        public int Closed;
        public int Opened;

        [return: DisposeWhenDiscarded]
        public Reading Rank(Recorded subject)
        {
            ++Opened;
            return new Reading(this, subject, false);
        }

        [return: DisposeWhenDiscarded]
        public Reading Score(Recorded subject)
        {
            ++Opened;
            return new Reading(this, subject, true);
        }
    }

    static readonly MethodInfo minutesModulus = typeof(Minutes).GetMethod("op_Modulus")!;
    static readonly MethodInfo minutesNegation = typeof(Minutes).GetMethod("op_UnaryNegation")!;

    static void AssertMechanism(ExpressionObserver observer, bool useDirectSubscription)
    {
        if (useDirectSubscription)
            Assert.AreEqual(0, observer.CachedObservableExpressions, "the fast path declined the shape this test exists to cover");
    }

    static void AssertReleased(ReadingSource source, SubscriptionLog log)
    {
        Assert.AreEqual(2, source.Opened, "the observation did not make exactly one reading for each operand");
        Assert.AreEqual(2, source.Closed, "the observation did not dispose of each reading it made exactly once");
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    static Expression<Func<Recorded, TResult>> Combine<TFirst, TSecond, TResult>(Expression<Func<Recorded, TFirst>> first, Expression<Func<Recorded, TSecond>> second, Func<Expression, Expression, Expression> body)
    {
        var subject = Expression.Parameter(typeof(Recorded), "subject");
        return Expression.Lambda<Func<Recorded, TResult>>(body(Expression.Invoke(first, subject), Expression.Invoke(second, subject)), subject);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void APropertyOfAHeldSpanReadAfterAnotherOperand(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 100, Score = 30 };
        var source = new ReadingSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expression = observer.Observe(s => ((TimeSpan)source.Rank(s).Span!).Ticks % ((TimeSpan)source.Score(s).Span!).Ticks, subject))
        {
            AssertMechanism(observer, useDirectSubscription);
            Assert.AreEqual(TimeSpan.FromMinutes(10).Ticks, expression.Evaluation.Result);
            subject.Score = 40;
            Assert.AreEqual(TimeSpan.FromMinutes(20).Ticks, expression.Evaluation.Result);
            subject.Rank = 130;
            Assert.AreEqual(TimeSpan.FromMinutes(10).Ticks, expression.Evaluation.Result);
        }
        AssertReleased(source, log);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void APropertyOfAHeldSpanPassedAsASecondArgument(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 10, Score = 20 };
        var source = new ReadingSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expression = observer.Observe(s => Math.Max(((TimeSpan)source.Rank(s).Span!).Ticks, ((TimeSpan)source.Score(s).Span!).Ticks), subject))
        {
            AssertMechanism(observer, useDirectSubscription);
            Assert.AreEqual(TimeSpan.FromMinutes(20).Ticks, expression.Evaluation.Result);
            subject.Rank = 30;
            Assert.AreEqual(TimeSpan.FromMinutes(30).Ticks, expression.Evaluation.Result);
        }
        AssertReleased(source, log);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void APropertyOfAHeldSpanInADeferredBranch(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 0, Score = 30 };
        var source = new ReadingSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expression = observer.Observe(s => s.Rank > 0 ? ((TimeSpan)source.Rank(s).Span!).Ticks % ((TimeSpan)source.Score(s).Span!).Ticks : -1L, subject))
        {
            AssertMechanism(observer, useDirectSubscription);
            Assert.AreEqual(-1L, expression.Evaluation.Result);
            Assert.AreEqual(0, source.Opened, "a reading was made for a branch which had not been taken");
            subject.Rank = 100;
            Assert.AreEqual(TimeSpan.FromMinutes(10).Ticks, expression.Evaluation.Result);
            subject.Score = 40;
            Assert.AreEqual(TimeSpan.FromMinutes(20).Ticks, expression.Evaluation.Result);
        }
        AssertReleased(source, log);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AHeldSpanOfZeroAfterAnotherOperandFaults(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 100, Score = 0 };
        var source = new ReadingSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expression = observer.Observe(s => ((TimeSpan)source.Rank(s).Span!).Ticks % ((TimeSpan)source.Score(s).Span!).Ticks, subject))
        {
            AssertMechanism(observer, useDirectSubscription);
            Assert.IsInstanceOfType<DivideByZeroException>(expression.Evaluation.Fault);
            subject.Score = 30;
            Assert.IsNull(expression.Evaluation.Fault);
            Assert.AreEqual(TimeSpan.FromMinutes(10).Ticks, expression.Evaluation.Result);
        }
        AssertReleased(source, log);
    }

    /// <summary>
    /// The shape of the formula which found this: a literal lambda taking two spans, invoked with two operands each reading a held value
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TheRatioOfTwoHeldSpansThroughAnInvokedLambda(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 90, Score = 60 };
        var source = new ReadingSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        Expression<Func<TimeSpan, TimeSpan, double>> ratio = (dividend, divisor) => (double)dividend.Ticks / divisor.Ticks;
        var observed = Combine<TimeSpan, TimeSpan, double>(s => (TimeSpan)source.Rank(s).Span!, s => (TimeSpan)source.Score(s).Span!, (first, second) => Expression.Invoke(ratio, first, second));
        using (var expression = observer.Observe(observed, subject))
        {
            AssertMechanism(observer, useDirectSubscription);
            Assert.AreEqual(1.5, expression.Evaluation.Result);
            subject.Score = 45;
            Assert.AreEqual(2.0, expression.Evaluation.Result);
            subject.Score = 0;
            Assert.AreEqual(double.PositiveInfinity, expression.Evaluation.Result);
        }
        AssertReleased(source, log);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AHeldValueAsTheSecondOperandOfABinaryOperatorTakingItsOperandsByReference(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 100, Score = 30 };
        var source = new ReadingSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        var observed = Combine<Minutes, Minutes, long>(s => (Minutes)source.Rank(s).Count!, s => (Minutes)source.Score(s).Count!, (first, second) => Expression.Field(Expression.Modulo(first, second, minutesModulus), nameof(Minutes.Value)));
        using (var expression = observer.Observe(observed, subject))
        {
            AssertMechanism(observer, useDirectSubscription);
            Assert.AreEqual(10L, expression.Evaluation.Result);
            subject.Score = 40;
            Assert.AreEqual(20L, expression.Evaluation.Result);
        }
        AssertReleased(source, log);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AHeldValueAsTheOperandOfAUnaryOperatorTakingItByReferenceAfterAnotherOperand(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 100, Score = 30 };
        var source = new ReadingSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        var observed = Combine<Minutes, Minutes, long>(s => (Minutes)source.Rank(s).Count!, s => (Minutes)source.Score(s).Count!, (first, second) => Expression.Add(Expression.Field(first, nameof(Minutes.Value)), Expression.Field(Expression.Negate(second, minutesNegation), nameof(Minutes.Value))));
        using (var expression = observer.Observe(observed, subject))
        {
            AssertMechanism(observer, useDirectSubscription);
            Assert.AreEqual(70L, expression.Evaluation.Result);
            subject.Score = 40;
            Assert.AreEqual(60L, expression.Evaluation.Result);
        }
        AssertReleased(source, log);
    }

    /// <summary>
    /// A field of a held value in the same position, which the compiler already accepted because reading a field has no side effect
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AFieldOfAHeldValueReadAfterAnotherOperand(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 100, Score = 30 };
        var source = new ReadingSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expression = observer.Observe(s => ((Minutes)source.Rank(s).Count!).Value % ((Minutes)source.Score(s).Count!).Value, subject))
        {
            AssertMechanism(observer, useDirectSubscription);
            Assert.AreEqual(10L, expression.Evaluation.Result);
            subject.Score = 40;
            Assert.AreEqual(20L, expression.Evaluation.Result);
        }
        AssertReleased(source, log);
    }

    /// <summary>
    /// An instance call on a held value in the same position, which the compiler already accepted by taking the value's address rather than refusing
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnInstanceCallOnAHeldSpanAfterAnotherOperand(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 100, Score = 30 };
        var source = new ReadingSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expression = observer.Observe(s => ((TimeSpan)source.Rank(s).Span!).CompareTo(TimeSpan.Zero) + ((TimeSpan)source.Score(s).Span!).CompareTo(TimeSpan.FromMinutes(40)), subject))
        {
            AssertMechanism(observer, useDirectSubscription);
            Assert.AreEqual(0, expression.Evaluation.Result);
            subject.Score = 50;
            Assert.AreEqual(2, expression.Evaluation.Result);
        }
        AssertReleased(source, log);
    }
}
