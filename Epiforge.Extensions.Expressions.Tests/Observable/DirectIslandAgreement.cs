namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// What each mechanism reports for an expression refused at its root whose operands could each be observed directly, which the graph observes whole and which the default observer may host as direct islands inside the graph
/// </summary>
/// <remarks>
/// Every row observes the same expression with an observer which uses direct subscription and with one which does not, and requires the two transcripts to be the same: values, faults, announcements, how many leases were made and disposed of, and what was attached. Until islands exist both observers build the graph, so these rows pin what the graph does now and what hosting must not change
/// </remarks>
[TestClass]
public class DirectIslandAgreement
{
    public sealed class LeaseLedger
    {
        public int Closed;
        public int Opened;
    }

    /// <summary>
    /// A value the observer disposes of when it is replaced, constructed from an argument which can change, which is what refuses an expression at its root
    /// </summary>
    public sealed class Lease :
        IDisposable
    {
        public Lease(LeaseLedger ledger, int value)
        {
            this.ledger = ledger;
            Value = value;
            ++ledger.Opened;
        }

        readonly LeaseLedger ledger;

        public int Value { get; }

        public void Dispose() =>
            ++ledger.Closed;

        public override string ToString() =>
            $"lease {Value}";
    }

    sealed class Transcript(bool useDirectSubscription)
    {
        readonly List<string> entries = [];

        internal readonly SubscriptionLog Log = new();
        internal readonly LeaseLedger Ledger = new();
        internal readonly ExpressionObserver Observer = ExpressionObserverHelpers.Create(useDirectSubscription);

        internal IReadOnlyList<string> Entries =>
            entries;

        internal void Note<TResult>(string step, IObservableExpression<TResult> expression, int announcements) =>
            entries.Add($"{step}: {Describe(expression.Evaluation)}, announced {announcements}, opened {Ledger.Opened}, closed {Ledger.Closed}, attached [{string.Join(", ", Log.Attachments())}]");

        internal void NoteEnd() =>
            entries.Add($"end: opened {Ledger.Opened}, closed {Ledger.Closed}, outstanding {Log.Outstanding}");

        static string Describe<TResult>((Exception? Fault, TResult Result) evaluation) =>
            evaluation.Fault is { } fault ? $"! {fault.GetType().Name}" : $"= {evaluation.Result?.ToString() ?? "null"}";
    }

    static int Counting(INotifyPropertyChanged expression, ref int announcements, Action change)
    {
        var counted = 0;
        void Count(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Evaluation")
                ++counted;
        }
        expression.PropertyChanged += Count;
        try
        {
            change();
        }
        finally
        {
            expression.PropertyChanged -= Count;
        }
        announcements += counted;
        return announcements;
    }

    static void AssertAgreement(Func<bool, Transcript> run)
    {
        var graph = run(false);
        var fast = run(true);
        CollectionAssert.AreEqual(graph.Entries.ToArray(), fast.Entries.ToArray(), $"the mechanisms disagree{Environment.NewLine}graph:{Environment.NewLine}{string.Join(Environment.NewLine, graph.Entries)}{Environment.NewLine}default:{Environment.NewLine}{string.Join(Environment.NewLine, fast.Entries)}");
    }

    static Transcript Operands(bool useDirectSubscription)
    {
        var transcript = new Transcript(useDirectSubscription);
        var ledger = transcript.Ledger;
        var subject = new Recorded(transcript.Log) { Rank = 1, Score = 2, Next = new Recorded(transcript.Log) { Rank = 3 } };
        var announcements = 0;
        using (var expression = transcript.Observer.Observe(r => new Lease(ledger, r.Rank + r.Score * r.Next!.Rank), subject))
        {
            transcript.Note("built", expression, announcements);
            Counting(expression, ref announcements, () => subject.Rank = 4);
            transcript.Note("rank", expression, announcements);
            Counting(expression, ref announcements, () => subject.Next!.Rank = 5);
            transcript.Note("next rank", expression, announcements);
            Counting(expression, ref announcements, () => subject.Next = new Recorded(transcript.Log) { Rank = 6 });
            transcript.Note("next moved", expression, announcements);
            Counting(expression, ref announcements, () => subject.Tag = "unread");
            transcript.Note("unread", expression, announcements);
            Counting(expression, ref announcements, () => subject.Score = 0);
            transcript.Note("score", expression, announcements);
        }
        transcript.NoteEnd();
        return transcript;
    }

    static Transcript Faults(bool useDirectSubscription)
    {
        var transcript = new Transcript(useDirectSubscription);
        var ledger = transcript.Ledger;
        var subject = new Recorded(transcript.Log) { Rank = 1, Score = 2 };
        var announcements = 0;
        using (var expression = transcript.Observer.Observe(r => new Lease(ledger, r.Next!.Rank + r.Score), subject))
        {
            transcript.Note("built without next", expression, announcements);
            Counting(expression, ref announcements, () => subject.Score = 3);
            transcript.Note("score while faulted", expression, announcements);
            Counting(expression, ref announcements, () => subject.Next = new Recorded(transcript.Log) { Rank = 4 });
            transcript.Note("next", expression, announcements);
            Counting(expression, ref announcements, () => subject.Next = null);
            transcript.Note("next removed", expression, announcements);
        }
        transcript.NoteEnd();
        return transcript;
    }

    static Transcript DeferredBranch(bool useDirectSubscription)
    {
        var transcript = new Transcript(useDirectSubscription);
        var ledger = transcript.Ledger;
        var subject = new Recorded(transcript.Log) { Rank = 1, Score = 0, Next = new Recorded(transcript.Log) { Rank = 2 } };
        var announcements = 0;
        using (var expression = transcript.Observer.Observe(r => r.Score > 0 ? new Lease(ledger, r.Rank * r.Next!.Rank) : null, subject))
        {
            transcript.Note("built untaken", expression, announcements);
            Counting(expression, ref announcements, () => subject.Rank = 3);
            transcript.Note("rank while untaken", expression, announcements);
            Counting(expression, ref announcements, () => subject.Score = 1);
            transcript.Note("taken", expression, announcements);
            Counting(expression, ref announcements, () => subject.Next!.Rank = 4);
            transcript.Note("next rank", expression, announcements);
            Counting(expression, ref announcements, () => subject.Score = 0);
            transcript.Note("untaken again", expression, announcements);
            Counting(expression, ref announcements, () => subject.Rank = 5);
            transcript.Note("rank while untaken again", expression, announcements);
        }
        transcript.NoteEnd();
        return transcript;
    }

    static Transcript Shared(bool useDirectSubscription)
    {
        var transcript = new Transcript(useDirectSubscription);
        var ledger = transcript.Ledger;
        var subject = new Recorded(transcript.Log) { Rank = 1, Score = 2 };
        Expression<Func<Recorded, Lease>> leasing = r => new Lease(ledger, r.Rank + r.Score);
        var announcements = 0;
        using (var earlier = transcript.Observer.Observe(leasing, subject))
        {
            transcript.Note("earlier", earlier, announcements);
            using (var later = transcript.Observer.Observe(leasing, subject))
            {
                transcript.Note("later", later, announcements);
                Counting(later, ref announcements, () => subject.Rank = 4);
                transcript.Note("later after rank", later, announcements);
                transcript.Note("earlier after rank", earlier, announcements);
            }
            transcript.Note("earlier alone", earlier, announcements);
        }
        transcript.NoteEnd();
        return transcript;
    }

    static Transcript Reduced(bool useDirectSubscription)
    {
        var transcript = new Transcript(useDirectSubscription);
        var ledger = transcript.Ledger;
        var subject = new Recorded(transcript.Log) { Rank = 2, Score = 3 };
        Expression<Func<int, int>> square = x => x * x;
        Expression<Func<int, int>> once = x => x + 1;
        var parameter = Expression.Parameter(typeof(Recorded), "r");
        var operand = Expression.Add(Expression.Invoke(square, Expression.Property(parameter, nameof(Recorded.Rank))), Expression.Invoke(once, Expression.Property(parameter, nameof(Recorded.Score))));
        var leaseConstructor = typeof(Lease).GetConstructor([typeof(LeaseLedger), typeof(int)])!;
        var leasing = Expression.Lambda<Func<Recorded, Lease>>(Expression.New(leaseConstructor, Expression.Constant(ledger), operand), parameter);
        var announcements = 0;
        using (var expression = transcript.Observer.Observe(leasing, subject))
        {
            transcript.Note("built", expression, announcements);
            Counting(expression, ref announcements, () => subject.Rank = 5);
            transcript.Note("rank", expression, announcements);
            Counting(expression, ref announcements, () => subject.Score = -6);
            transcript.Note("score", expression, announcements);
        }
        transcript.NoteEnd();
        return transcript;
    }

    [TestMethod]
    public void TheMechanismsAgreeAboutOperandsUnderARefusedRoot() =>
        AssertAgreement(Operands);

    [TestMethod]
    public void TheMechanismsAgreeAboutAnOperandWhichFaults() =>
        AssertAgreement(Faults);

    [TestMethod]
    public void TheMechanismsAgreeAboutAnOperandInABranchNotYetTaken() =>
        AssertAgreement(DeferredBranch);

    [TestMethod]
    public void TheMechanismsAgreeAboutTwoObservationsOfOneRefusedExpression() =>
        AssertAgreement(Shared);

    [TestMethod]
    public void TheMechanismsAgreeAboutAnOperandTheReducerRewrites() =>
        AssertAgreement(Reduced);

    /// <summary>
    /// An expression refused at its root whose operand is large enough to be worth hosting, which the default observer observes with fewer graph nodes than the graph builds for it
    /// </summary>
    [TestMethod]
    public void ALargeOperandUnderARefusedRootIsHostedDirectly()
    {
        var log = new SubscriptionLog();
        var ledger = new LeaseLedger();
        var subject = new Recorded(log) { Rank = 1, Score = 2, Next = new Recorded(log) { Rank = 3, Score = 4 } };
        Expression<Func<Recorded, Lease>> leasing = r => new Lease(ledger, r.Rank * r.Score + r.Next!.Rank * r.Next!.Score - r.Rank);
        var graph = ExpressionObserverHelpers.Create(false);
        int graphNodes;
        using (graph.Observe(leasing, subject))
            graphNodes = graph.CachedObservableExpressions;
        var fast = ExpressionObserverHelpers.Create(true);
        using (var expression = fast.Observe(leasing, subject))
        {
            Assert.AreEqual(13, expression.Evaluation.Result.Value);
            Assert.IsTrue(fast.CachedObservableExpressions < graphNodes, $"the default observer built {fast.CachedObservableExpressions} graph nodes where the graph builds {graphNodes}, so it hosted nothing directly");
        }
        Assert.AreEqual(ledger.Opened, ledger.Closed);
        Assert.AreEqual(0, log.Outstanding);
    }
}
