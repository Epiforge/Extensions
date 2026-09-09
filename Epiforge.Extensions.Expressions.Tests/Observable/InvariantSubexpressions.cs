namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// How many times a subexpression whose every operand is unchanging is evaluated over the life of an observation, which for a call producing a disposable value is also how many of those values are made and discarded
/// </summary>
/// <remarks>
/// The graph gives every subexpression a node holding its last value, so a call whose operands cannot change is made once and its value disposed of once, however many times the observation is re-evaluated. The fast path holds such a call in a slot resolved by the first evaluation which reads it, which is why it may be admitted at all: a value which never moves needs no rule for what to do when it moves. These pin the count on both mechanisms, so that admitting such a call cannot quietly turn one construction into one per notification
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

        /// <summary>
        /// The same query taking something which can change, which makes the call one neither mechanism may hold
        /// </summary>
        [return: DisposeWhenDiscarded]
        public ValueQuery OpenAt(Recorded subject, int rank)
        {
            ++Opened;
            return new ValueQuery(this, subject);
        }

        [return: DisposeWhenDiscarded]
        public ValueQuery OpenFaulted(Recorded subject)
        {
            ++Opened;
            throw new InvalidOperationException("no query could be made");
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

    [TestMethod]
    public void AnInvariantCallWhoseValueIsDisposedTakesTheFastPath()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var source = new ValueSource();
        var observer = ExpressionObserverHelpers.Create();
        using (var expression = observer.Observe(s => (int)source.Open(s).Value!, subject))
        {
            Assert.AreEqual(0, observer.CachedObservableExpressions, "the analyzer refused a call nothing it reads can change");
            Assert.AreEqual(1, expression.Evaluation.Result);
            subject.Rank = 4;
            Assert.AreEqual(4, expression.Evaluation.Result);
        }
        Assert.AreEqual(1, source.Opened);
        Assert.AreEqual(1, source.Closed);
        Assert.AreEqual(0, log.Outstanding);
    }

    /// <summary>
    /// A call whose argument can change, which the graph re-evaluates and whose previous value it disposes of as the next replaces it, and which the fast path therefore still refuses
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ACallWhoseArgumentCanChangeIsRefused(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1, Score = 1 };
        var source = new ValueSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expression = observer.Observe(s => (int)source.OpenAt(s, s.Score).Value!, subject))
        {
            Assert.AreNotEqual(0, observer.CachedObservableExpressions, "a call whose argument can change was admitted");
            Assert.AreEqual(1, expression.Evaluation.Result);
            subject.Score = 2;
            subject.Score = 3;
            Assert.IsTrue(source.Opened > 1, "the graph did not make the call again when its argument changed");
        }
        Assert.AreEqual(source.Opened, source.Closed, "every query made was not disposed of exactly once");
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    /// <summary>
    /// A held call in a branch which is not taken, which neither mechanism makes until an evaluation reaches it and neither makes again once it has
    /// </summary>
    /// <remarks>
    /// How many are made by the evaluation which first reaches the branch is not asserted here, because the two mechanisms differ and the difference is the graph's: it makes the call twice, for the reason the row below this one records. What both must do is make none before the branch is taken and none again after
    /// </remarks>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnInvariantCallInABranchNotTakenIsNotMade(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 5, Score = 0 };
        var source = new ValueSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expression = observer.Observe(s => s.Score > 0 ? (int)source.Open(s).Value! : -1, subject))
        {
            Assert.AreEqual(-1, expression.Evaluation.Result);
            Assert.AreEqual(0, source.Opened, "a query was made for a branch which had not been taken");
            subject.Score = 1;
            Assert.AreEqual(5, expression.Evaluation.Result);
            var reaching = source.Opened;
            Assert.AreNotEqual(0, reaching, "taking the branch made no query at all");
            subject.Rank = 9;
            Assert.AreEqual(9, expression.Evaluation.Result);
            subject.Rank = 11;
            Assert.AreEqual(11, expression.Evaluation.Result);
            Assert.AreEqual(reaching, source.Opened, "a query was made again after the branch had already been taken");
        }
        Assert.AreEqual(source.Opened, source.Closed, "every query made was not disposed of exactly once");
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    /// <summary>
    /// The evaluation which first reaches a branch holding a call makes exactly one, which is the count the shape requires
    /// </summary>
    [TestMethod]
    public void ReachingABranchWithAnInvariantCallMakesOneQueryOnTheFastPath()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 5, Score = 0 };
        var source = new ValueSource();
        var observer = ExpressionObserverHelpers.Create();
        using (var expression = observer.Observe(s => s.Score > 0 ? (int)source.Open(s).Value! : -1, subject))
        {
            Assert.AreEqual(0, observer.CachedObservableExpressions, "the analyzer refused the shape this test exists to cover");
            subject.Score = 1;
            Assert.AreEqual(5, expression.Evaluation.Result);
            Assert.AreEqual(1, source.Opened, "reaching the branch did not make exactly one query");
        }
        Assert.AreEqual(1, source.Closed);
        Assert.AreEqual(0, log.Outstanding);
    }

    /// <summary>
    /// A deferred call reached through something deferred along with it, which the graph once made twice the first time a branch reached it and now makes once
    /// </summary>
    /// <remarks>
    /// The call's evaluation begins by reading the evaluation of the object it is made on, and reading a deferred node's evaluation both resolves that node and announces that its value changed. The announcement reaches this same call, which is a dependent of that object, and used to re-enter the call's evaluation; the inner evaluation completed against the now resolved object and the outer one, still on its first line, then did the same work again. Both produced the same value, which is why nothing reported it, and one query was made and discarded for nothing. An evaluation in progress now declines to be re-entered, having not yet read what it depends on and being about to read exactly what the announcement was telling it
    /// </remarks>
    [TestMethod]
    public void TheGraphMakesADeferredCallOnceWhenWhatItIsReachedThroughIsAlsoDeferred()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 5, Score = 0 };
        var source = new ValueSource();
        var observer = ExpressionObserverHelpers.Create(false);
        using (var expression = observer.Observe(s => s.Score > 0 ? (int)source.Open(s).Value! : -1, subject))
        {
            subject.Score = 1;
            Assert.AreEqual(5, expression.Evaluation.Result);
            Assert.AreEqual(1, source.Opened, "the graph made a deferred call more than once the first time a branch reached it");
            Assert.AreEqual(0, source.Closed, "a query was disposed of while the observation was still using it");
        }
        Assert.AreEqual(1, source.Closed);
        Assert.AreEqual(0, log.Outstanding);
    }

    /// <summary>
    /// The same call not deferred, where the graph makes it once, which is what identifies deferral as what the row above turns on
    /// </summary>
    [TestMethod]
    public void TheGraphMakesACallWhichIsNotDeferredOnce()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 5 };
        var source = new ValueSource();
        var observer = ExpressionObserverHelpers.Create(false);
        using (var expression = observer.Observe(s => (int)source.Open(s).Value!, subject))
            Assert.AreEqual(5, expression.Evaluation.Result);
        Assert.AreEqual(1, source.Opened);
        Assert.AreEqual(1, source.Closed);
        Assert.AreEqual(0, log.Outstanding);
    }

    /// <summary>
    /// A held call which throws, which is made once and faults the observation every time after rather than being tried again
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnInvariantCallWhichThrowsIsMadeOnce(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var source = new ValueSource();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expression = observer.Observe(s => s.Rank + (int)source.OpenFaulted(s).Value!, subject))
        {
            Assert.IsInstanceOfType<InvalidOperationException>(expression.Evaluation.Fault);
            Assert.AreEqual(1, source.Opened);
            subject.Rank = 2;
            Assert.IsInstanceOfType<InvalidOperationException>(expression.Evaluation.Fault);
            subject.Rank = 3;
            Assert.IsInstanceOfType<InvalidOperationException>(expression.Evaluation.Fault);
            Assert.AreEqual(1, source.Opened, "the call was made again after it had already thrown");
        }
        Assert.AreEqual(0, source.Closed, "a query which was never made was disposed of");
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    /// <summary>
    /// Two observations of one expression, which the graph serves from one cached node and therefore one query
    /// </summary>
    /// <remarks>
    /// This is recorded rather than required to agree, because the fast path holds its values per observation and has no cache to share them through. What it must not do is make more than one query per observation, which the rows above pin
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
