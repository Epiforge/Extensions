namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// How often each mechanism repeats a subexpression which cannot change, once the observation exists and its sources are announcing
/// </summary>
/// <remarks>
/// The graph re-evaluates a node only when one of the nodes it depends on announces, so a subexpression whose operands cannot change is evaluated once and never again. The fast path has one compiled delegate for the whole body and invokes all of it whenever any subscribed source announces, so it repeats every subexpression the analyzer did not hold. For a pure call that is wasted work rather than a different answer, and these record how much of it there is, on both mechanisms, so that widening what is held can be scored against them
/// </remarks>
[TestClass]
public class RepeatedEvaluation
{
    public sealed class Counter
    {
        public int Calls;

        /// <summary>
        /// A call the analyzer admits and does not hold, its return type being sealed and implementing neither disposal interface
        /// </summary>
        public int Scale()
        {
            ++Calls;
            return 2;
        }
    }

    static int RepeatsOf(bool useDirectSubscription, int changes)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var counter = new Counter();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expression = observer.Observe(s => s.Rank + counter.Scale(), subject))
        {
            Assert.AreEqual(3, expression.Evaluation.Result, "the two forms did not start alike");
            for (var change = 1; change <= changes; ++change)
            {
                subject.Rank = change + 1;
                Assert.AreEqual(change + 3, expression.Evaluation.Result, "the observation did not follow the source it was reading");
            }
        }
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
        return counter.Calls;
    }

    /// <summary>
    /// A call which is not held is made once by the graph and once per evaluation by the fast path, which is the cost the two mechanisms do not share
    /// </summary>
    /// <remarks>
    /// This is a difference in how much work is done and not in what is reported, the library having always required an observed expression to be free of side effects. It is recorded rather than corrected because correcting it means holding every call whose operands cannot change rather than only those whose value is disposed of, which is a widening to be measured before it is made
    /// </remarks>
    [TestMethod]
    public void TheFastPathRepeatsACallWhichIsNotHeldAndTheGraphDoesNot()
    {
        Assert.AreEqual(1, RepeatsOf(false, 5), "the graph made a call again whose operands had not moved");
        Assert.AreEqual(6, RepeatsOf(true, 5), "the fast path did not make the call once per evaluation, so what is held has changed and this row should say so");
    }

    /// <summary>
    /// The same shape with more changes, which pins that what the fast path repeats grows with the notifications and what the graph repeats does not
    /// </summary>
    [TestMethod]
    public void WhatIsRepeatedGrowsWithTheNotificationsOnTheFastPathAlone()
    {
        Assert.AreEqual(1, RepeatsOf(false, 20));
        Assert.AreEqual(21, RepeatsOf(true, 20));
    }
}
