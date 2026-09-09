namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// How many times a subexpression whose every operand is unchanging is evaluated over the life of an observation, which for a call producing a disposable value is also how many of those values are made and discarded
/// </summary>
/// <remarks>
/// The graph gives every subexpression a node holding its last value, so a call whose operands cannot change is invoked once and its value is disposed of once, however many times the observation is re-evaluated. These pin that count on both mechanisms, so that admitting such a call to the fast path cannot quietly turn one construction into one per notification
/// </remarks>
[TestClass]
public class InvariantSubexpressions
{
    public sealed class ValueQuery :
        IDisposable,
        INotifyPropertyChanged
    {
        internal ValueQuery(ValueSource source, Recorded subject)
        {
            this.source = source;
            this.subject = subject;
            subject.PropertyChanged += SubjectPropertyChanged;
        }

        readonly ValueSource source;
        readonly Recorded subject;

        public object? Value =>
            subject.Rank;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Dispose()
        {
            subject.PropertyChanged -= SubjectPropertyChanged;
            PropertyChanged = null;
            ++source.Closed;
        }

        void SubjectPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }

    public sealed class ValueSource
    {
        public int Closed;
        public int Opened;

        [return: DisposeWhenDiscarded]
        public ValueQuery Open(Recorded subject)
        {
            ++Opened;
            return new ValueQuery(this, subject);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnInvariantCallIsMadeOnceHoweverOftenTheObservationIsReevaluated(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var source = new ValueSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        var announcements = 0;
        using (var expression = observer.Observe(s => (int)source.Open(s).Value!, subject))
        {
            expression.PropertyChanged += (sender, e) => ++announcements;
            Assert.AreEqual(1, expression.Evaluation.Result);
            Assert.AreEqual(1, source.Opened, "the observation was built by making more than one query");
            for (var rank = 2; rank <= 6; ++rank)
            {
                subject.Rank = rank;
                Assert.AreEqual(rank, expression.Evaluation.Result, "the observation did not follow the value it was reading");
            }
            Assert.AreEqual(5, announcements, "the observation did not announce once per change");
            Assert.AreEqual(1, source.Opened, "a query was made again after the observation had already been built");
            Assert.AreEqual(0, source.Closed, "the query was disposed of while the observation was still using it");
        }
        Assert.AreEqual(1, source.Closed, "the observation did not dispose of the one query it made exactly once");
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    /// <summary>
    /// Two observations of one expression, which the graph serves from one cached node and therefore one query
    /// </summary>
    /// <remarks>
    /// This is recorded rather than required to agree, because the fast path holds its values per observation and has no cache to share them through. What it must not do is make more than one query per observation, which the row above pins
    /// </remarks>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TwoObservationsOfOneExpressionMakeNoMoreThanOneQueryEach(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var source = new ValueSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        Expression<Func<Recorded, int>> reading = s => (int)source.Open(s).Value!;
        using (var first = observer.Observe(reading, subject))
        using (var second = observer.Observe(reading, subject))
        {
            Assert.AreEqual(1, first.Evaluation.Result);
            Assert.AreEqual(1, second.Evaluation.Result);
            Assert.IsTrue(source.Opened is 1 or 2, $"two observations of one expression made {source.Opened} queries");
            subject.Rank = 4;
            Assert.AreEqual(4, first.Evaluation.Result);
            Assert.AreEqual(4, second.Evaluation.Result);
            Assert.IsTrue(source.Opened is 1 or 2, $"two observations of one expression made {source.Opened} queries after a change");
        }
        Assert.AreEqual(source.Opened, source.Closed, "every query made was not disposed of exactly once");
        Assert.AreEqual(0, log.Outstanding, "something the observations attached is still attached");
    }
}
