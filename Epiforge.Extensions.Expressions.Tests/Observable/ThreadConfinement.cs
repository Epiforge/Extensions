namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// An observer told that its observations are confined to one thread at a time, which on that thread must behave exactly as one which is not
/// </summary>
/// <remarks>
/// What confinement gives up is only what happens when threads meet, and nothing here can make them meet without breaking the promise the option asks for, so these rows compare a confined observer against a safe one on one thread and leave meeting threads to <see cref="ConcurrentChange"/>, which runs with the default
/// </remarks>
[TestClass]
public class ThreadConfinement
{
    static ExpressionObserver CreateConfined(bool useDirectSubscription, ExpressionObserverOptions? options = null)
    {
        options ??= new ExpressionObserverOptions();
        options.IsThreadSafe = false;
        return ExpressionObserverHelpers.Create(useDirectSubscription, options);
    }

    static int ReadsWhileConstructing(ExpressionObserver observer)
    {
        var subject = new ReentrantSourceNotification.Announcing();
        using var expression = observer.Observe(s => s.Loud + s.Quiet, subject);
        Assert.AreEqual(1, expression.Evaluation.Result);
        return subject.Reads;
    }

    [TestMethod]
    public void ObserversAreThreadSafeByDefault()
    {
        Assert.IsTrue(new ExpressionObserverOptions().IsThreadSafe);
        Assert.IsTrue(new ExpressionObserver().IsThreadSafe);
    }

    [TestMethod]
    public void TheOptionReachesTheObserver() =>
        Assert.IsFalse(new ExpressionObserver(new ExpressionObserverOptions { IsThreadSafe = false }).IsThreadSafe);

    /// <summary>
    /// A confined observation follows changes, faults and recovers, and leaves nothing cached once disposed of
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AConfinedObservationFollowsChanges(bool useDirectSubscription)
    {
        var observer = CreateConfined(useDirectSubscription);
        var person = new TestPerson("Ben");
        using (var observation = observer.Observe(p => p.Name!.Length, person))
        {
            Assert.AreEqual(3, observation.Evaluation.Result);
            person.Name = "Bridget";
            Assert.AreEqual(7, observation.Evaluation.Result);
            person.Name = null;
            Assert.IsInstanceOfType<NullReferenceException>(observation.Evaluation.Fault);
            person.Name = "Erin";
            Assert.IsNull(observation.Evaluation.Fault);
            Assert.AreEqual(4, observation.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    /// <summary>
    /// Confined observations sharing nodes release them as safe ones do
    /// </summary>
    [TestMethod]
    public void ConfinedObservationsReleaseSharedNodes()
    {
        var observer = CreateConfined(false);
        var person = new TestPerson("Ben");
        var first = observer.Observe(p => p.Name!.Length > 2, person);
        var second = observer.Observe(p => p.Name!.Length > 2, person);
        person.Name = "Bo";
        Assert.IsFalse(first.Evaluation.Result);
        Assert.IsFalse(second.Evaluation.Result);
        first.Dispose();
        person.Name = "Bridget";
        Assert.IsTrue(second.Evaluation.Result);
        second.Dispose();
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    /// <summary>
    /// A confined observation of a getter which announces while being read reads it as often as a safe one does, which is the re-entry a confined observer still has to handle on its one thread
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AConfinedObservationHandlesReentryAsASafeOneDoes(bool useDirectSubscription) =>
        Assert.AreEqual(ReadsWhileConstructing(ExpressionObserverHelpers.Create(useDirectSubscription)), ReadsWhileConstructing(CreateConfined(useDirectSubscription)));

    /// <summary>
    /// A confined observation disposes of everything it made, once each, both as changes replace it and when the observation is disposed of
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AConfinedObservationDisposesOfWhatItMadeOnce(bool useDirectSubscription)
    {
        var maker = new ConcurrentChange.Maker();
        var options = new ExpressionObserverOptions { PreferAsyncDisposal = false };
        options.AddMethodReturnValueDisposal(typeof(ConcurrentChange.Maker).GetMethod(nameof(ConcurrentChange.Maker.Make))!);
        var observer = CreateConfined(useDirectSubscription, options);
        var gated = new ConcurrentChange.Gated(1);
        using (var observation = observer.Observe(g => maker.Make(g.Value), gated))
        {
            gated.Change(2);
            gated.Change(3);
            Assert.AreEqual(3, observation.Evaluation.Result!.Value);
        }
        Assert.AreEqual(3, maker.Made.Count);
        foreach (var made in maker.Made)
            Assert.AreEqual(1, made.Disposals, $"what was made for {made.Value} was disposed of {made.Disposals} times");
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }
}
