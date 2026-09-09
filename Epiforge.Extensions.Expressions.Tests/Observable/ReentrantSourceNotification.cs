namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// How often each mechanism reads a property whose getter announces a change while it is being read
/// </summary>
/// <remarks>
/// An observation is re-entered by a dependency announcing to it, which it now declines, and by a source announcing to it, which it does not: <c>DirectObservableExpression.OnSourceChanged</c> and <c>ObservableMemberExpression.ObservableExpressionValuePropertyChanged</c> both call <c>Evaluate</c> without the guard. A source can only announce during an evaluation if something the evaluation reads announces while being read, which is what the subject here does.
/// The subject announces on its first few reads only, so that whatever the mechanisms do the recursion ends. That bound is not a convenience: an unbounded form of this subject cannot be run at all, because a stack overflow cannot be caught and would take the test host with it. These rows record what happens today so that closing the gap has to flip an assertion rather than quietly make one vacuous
/// </remarks>
[TestClass]
public class ReentrantSourceNotification
{
    /// <summary>
    /// Announces that one property changed while a different one is being read, for its first few reads only
    /// </summary>
    public sealed class Announcing :
        INotifyPropertyChanged
    {
        internal const int Bound = 3;

        int announced;
        int reads;

        public int Reads =>
            reads;

        public int Loud
        {
            get
            {
                ++reads;
                if (announced < Bound)
                {
                    ++announced;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quiet)));
                }
                return 1;
            }
        }

        public int Quiet =>
            0;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    static int ReadsWhileConstructing(bool useDirectSubscription)
    {
        var subject = new Announcing();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(s => s.Loud + s.Quiet, subject);
        Assert.AreEqual(1, expression.Evaluation.Result, "the two forms did not start alike");
        return subject.Reads;
    }

    /// <summary>
    /// The graph reads it once, because the announcement names a property a different node watches, and the node composing them reads what its children last evaluated to rather than reading the source again
    /// </summary>
    [TestMethod]
    public void TheGraphReadsAnAnnouncingGetterOnce() =>
        Assert.AreEqual(1, ReadsWhileConstructing(false), "the graph read a getter which announces while being read more than once");

    /// <summary>
    /// The fast path reads it once per re-entry, because an announcement it wants re-invokes the whole compiled delegate, which reads every input again
    /// </summary>
    /// <remarks>
    /// One read for the evaluation which begins it and one for each announcement the subject makes. If this row ever reads 1, the guard which declines a dependency's re-entry has been extended to a source's, and this remark is the thing to correct
    /// </remarks>
    [TestMethod]
    public void TheFastPathReadsAnAnnouncingGetterOncePerReentry() =>
        Assert.AreEqual(Announcing.Bound + 1, ReadsWhileConstructing(true), "the fast path did not re-enter its evaluation once per announcement");
}
