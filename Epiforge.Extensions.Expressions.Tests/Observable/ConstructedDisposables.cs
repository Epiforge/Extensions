namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Pins which of the analyzer's remaining refusals of a value requiring disposal are the case a held slot already solves, and which are a different case wearing the same refusal
/// </summary>
/// <remarks>
/// A method call whose return value is disposed of is admitted when the call is invariant and is then held: made once, kept, disposed once, which is what the graph's node for it does. Three sites still refuse outright — a constructed value, a read of a property registered for disposal, and an indexer read of one — and they do not all deserve the same treatment.
/// A constructor's value is decided by its arguments and nothing else, so invariant arguments mean the value never changes and holding it once agrees with the graph exactly. A property registered for disposal is not that case even when what it is read through cannot change, because the property itself can announce: the graph re-reads it and disposes what it replaced, and a slot filled once would go stale and leak the rest. These rows record the distinction so that the constructed case can be admitted without the other two being admitted alongside it by resemblance
/// </remarks>
[TestClass]
public class ConstructedDisposables
{
    /// <summary>
    /// A value requiring disposal whose construction and disposal are both counted
    /// </summary>
    public sealed class CountedDisposable :
        IDisposable
    {
        internal static int Constructions;
        internal static int Disposals;

        internal static void Reset()
        {
            Constructions = 0;
            Disposals = 0;
        }

        public CountedDisposable(int rank)
        {
            Rank = rank;
            ++Constructions;
        }

        public int Rank { get; }

        public void Dispose() =>
            ++Disposals;
    }

    static (int cached, int constructions, int disposals) ObserveConstantConstruction(bool useDirectSubscription)
    {
        CountedDisposable.Reset();
        var subject = new TestPerson { Name = "Anne" };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        int cached;
        using (var expression = observer.Observe(p => p.Name!.Length + new CountedDisposable(1).Rank > 0, subject))
        {
            Assert.IsTrue(expression.Evaluation.Result);
            cached = observer.CachedObservableExpressions;
            subject.Name = "Benjamin";
            Assert.IsTrue(expression.Evaluation.Result, "the observation stopped agreeing after a source changed");
        }
        return (cached, CountedDisposable.Constructions, CountedDisposable.Disposals);
    }

    /// <summary>
    /// The graph constructs it once, because a node whose arguments cannot change is never re-evaluated, and disposes it once when the observation ends
    /// </summary>
    [TestMethod]
    public void TheGraphConstructsAnInvariantDisposableOnce()
    {
        var (_, constructions, disposals) = ObserveConstantConstruction(false);
        Assert.AreEqual(1, constructions, "the graph constructed an invariant disposable more than once");
        Assert.AreEqual(1, disposals, "the graph did not dispose what it constructed exactly once");
    }

    /// <summary>
    /// The analyzer refuses a constructed disposable today however invariant its arguments are, so an observation of one runs on the graph whatever the options say
    /// </summary>
    /// <remarks>
    /// When this row reads 0 the refusal has been narrowed to match the method call's, and the two rows below it are what must not have moved with it
    /// </remarks>
    [TestMethod]
    public void AConstructedDisposableIsRefusedByTheAnalyzer()
    {
        var (cached, constructions, disposals) = ObserveConstantConstruction(true);
        Assert.AreNotEqual(0, cached, "the analyzer admitted a constructed disposable");
        Assert.AreEqual(1, constructions, "the refused shape did not construct exactly once");
        Assert.AreEqual(1, disposals, "the refused shape did not dispose exactly once");
    }

    /// <summary>
    /// A constructed disposable whose argument is a reading of something which can change is refused, and must stay refused, because its value is not one a slot filled once could hold
    /// </summary>
    [TestMethod]
    public void AConstructedDisposableOverAChangingArgumentIsRefused()
    {
        CountedDisposable.Reset();
        var subject = new TestPerson { Name = "Anne" };
        var observer = ExpressionObserverHelpers.Create(true);
        using (var expression = observer.Observe(p => new CountedDisposable(p.Name!.Length).Rank >= 0, subject))
        {
            Assert.IsTrue(expression.Evaluation.Result);
            Assert.AreNotEqual(0, observer.CachedObservableExpressions, "the analyzer admitted a constructed disposable over a changing argument");
        }
    }

    /// <summary>
    /// A read of a property registered for disposal is refused even where nothing it is read through can change, and must stay refused, because the property can announce and a slot filled once would keep what the graph would have replaced and disposed
    /// </summary>
    [TestMethod]
    public void ADisposedPropertyIsRefusedEvenWhereItsTargetCannotChange()
    {
        var holder = new CountedDisposableHolder();
        var options = new ExpressionObserverOptions { UseDirectSubscription = true };
        Assert.IsTrue(options.AddPropertyValueDisposal(typeof(CountedDisposableHolder).GetProperty(nameof(CountedDisposableHolder.Held))!), "the property could not be registered for disposal, so this row proves nothing");
        var observer = new ExpressionObserver(options);
        using (var expression = observer.Observe(h => h.Held.Rank > 0, holder))
        {
            Assert.IsTrue(expression.Evaluation.Result);
            Assert.AreNotEqual(0, observer.CachedObservableExpressions, "the analyzer admitted a read of a property registered for disposal, which a held slot cannot serve because the property can announce");
        }
    }

    /// <summary>
    /// Carries a property whose value the observer can be told to dispose of, and announces that it changed
    /// </summary>
    public sealed class CountedDisposableHolder :
        INotifyPropertyChanged
    {
        CountedDisposable held = new(1);

        public CountedDisposable Held
        {
            get => held;
            set
            {
                held = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Held)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
