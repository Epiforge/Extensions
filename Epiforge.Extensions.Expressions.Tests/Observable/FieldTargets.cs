namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// When each mechanism captures a subexpression the analyzer calls fixed, after the thing it reads through is reassigned behind both of them
/// </summary>
/// <remarks>
/// The contract these pin: a member read through something which cannot announce is captured exactly once for the life of an observation and does not change afterward, and where such a read occurs only in a branch which is not evaluated immediately, the moment of capture is unspecified. Read eagerly, both mechanisms capture when the observation is built and agree. Read inside a deferred branch, a field is pinned when the observation is built while a local a lambda captured is pinned whenever the branch is first reached, because pinning the latter early would also attach to its value's contents, which a branch not yet taken must not do. That difference is deliberate and it is why the last test asserts capture-once rather than agreement: agreement about a mutation neither mechanism can see was never promised, and is now explicitly not promised
/// </remarks>
[TestClass]
public class FieldTargets
{
    static (object? Result, int Announcements) ObserveThroughACapturedLocal(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var first = new Recorded(log) { Rank = 2 };
        var second = new Recorded(log) { Rank = 10 };
        var other = first;
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        var announcements = 0;
        using var expression = observer.Observe(s => s.Rank + other.Rank, subject);
        expression.PropertyChanged += (sender, e) => ++announcements;
        Assert.AreEqual(3, expression.Evaluation.Result, "the two forms did not start alike");
        other = second;
        subject.Rank = 5;
        return (expression.Evaluation.Result, announcements);
    }

    static (object? Result, int Announcements) ObserveThroughAField(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1, Linked = new Recorded(log) { Rank = 2 } };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        var announcements = 0;
        using var expression = observer.Observe(s => s.Rank + s.Linked!.Rank, subject);
        expression.PropertyChanged += (sender, e) => ++announcements;
        Assert.AreEqual(3, expression.Evaluation.Result, "the two forms did not start alike");
        subject.Linked = new Recorded(log) { Rank = 10 };
        subject.Rank = 5;
        return (expression.Evaluation.Result, announcements);
    }

    /// <summary>
    /// Reads the field only inside a branch which is not taken until after the reassignment, which is the shape seed 1015 generated
    /// </summary>
    static (object? Result, int Announcements) ObserveThroughAFieldInsideADeferredBranch(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1, Score = 0, Linked = new Recorded(log) { Rank = 2 } };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        var announcements = 0;
        using var expression = observer.Observe(s => s.Score > 0 ? s.Linked!.Rank : s.Rank, subject);
        expression.PropertyChanged += (sender, e) => ++announcements;
        Assert.AreEqual(1, expression.Evaluation.Result, "the two forms did not start alike");
        subject.Linked = new Recorded(log) { Rank = 10 };
        subject.Score = 1;
        return (expression.Evaluation.Result, announcements);
    }

    /// <summary>
    /// Reassigns a captured local twice around the first reading of a deferred branch, and requires the value not to move once that branch has read it
    /// </summary>
    /// <remarks>
    /// This asserts capture-once rather than which of the two reassignments was captured. Which one it is differs between the mechanisms and is deliberately unspecified: pinning a captured local when the observation is built would also attach to its value's contents, which a branch not yet taken must not do
    /// </remarks>
    static void AssertCapturedOnce(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1, Score = 0 };
        var first = new Recorded(log) { Rank = 2 };
        var second = new Recorded(log) { Rank = 10 };
        var third = new Recorded(log) { Rank = 100 };
        var other = first;
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(s => s.Score > 0 ? other.Rank : s.Rank, subject);
        Assert.AreEqual(1, expression.Evaluation.Result, "the branch which reads the captured local was taken too early");
        other = second;
        subject.Score = 1;
        var captured = expression.Evaluation.Result;
        Assert.IsTrue(captured is 2 or 10, $"the deferred branch read neither object the captured local had held, but {captured}");
        other = third;
        subject.Score = 2;
        Assert.AreEqual(captured, expression.Evaluation.Result, "the value moved after the deferred branch had already captured it");
    }

    [TestMethod]
    public void TheMechanismsAgreeAfterACapturedLocalIsReassigned()
    {
        var graph = ObserveThroughACapturedLocal(false);
        var fast = ObserveThroughACapturedLocal(true);
        Assert.AreEqual(graph.Result, fast.Result, "the mechanisms report different values after a captured local was reassigned");
        Assert.AreEqual(graph.Announcements, fast.Announcements, "the mechanisms did not announce alike after a captured local was reassigned");
    }

    [TestMethod]
    public void TheMechanismsAgreeAfterAFieldIsReassigned()
    {
        var graph = ObserveThroughAField(false);
        var fast = ObserveThroughAField(true);
        Assert.AreEqual(graph.Result, fast.Result, "the mechanisms report different values after a field read through was reassigned");
        Assert.AreEqual(graph.Announcements, fast.Announcements, "the mechanisms did not announce alike after a field read through was reassigned");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NeitherMechanismRecapturesACapturedLocalOnceADeferredBranchHasReadIt(bool useDirectSubscription) =>
        AssertCapturedOnce(useDirectSubscription);

    [TestMethod]
    public void TheMechanismsAgreeWhenADeferredBranchReadsAReassignedField()
    {
        var graph = ObserveThroughAFieldInsideADeferredBranch(false);
        var fast = ObserveThroughAFieldInsideADeferredBranch(true);
        Assert.AreEqual(graph.Result, fast.Result, "the mechanisms captured a fixed subexpression in a deferred branch at different moments");
        Assert.AreEqual(graph.Announcements, fast.Announcements, "the mechanisms did not announce alike after a deferred branch read a reassigned field");
    }
}
