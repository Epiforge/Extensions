namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservationLifetime
{
    [TestMethod]
    public void IdenticalObservationsAreDistinctHandles()
    {
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
        using var first = observer.Observe(p => p.Name!.Length, john);
        using var second = observer.Observe(p => p.Name!.Length, john);
        Assert.AreNotSame(first, second);
        Assert.AreEqual(first.Evaluation.Result, second.Evaluation.Result);
    }

    [TestMethod]
    public void IdenticalObservationsShareOneCachedExpression()
    {
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
        using var first = observer.Observe(p => p.Name!.Length, john);
        var cachedAfterFirstObservation = observer.CachedObservableExpressions;
        using var second = observer.Observe(p => p.Name!.Length, john);
        Assert.AreEqual(cachedAfterFirstObservation, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void DisposingAnObservationTwiceIsIdempotent()
    {
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
        var observation = observer.Observe(p => p.Name!.Length, john);
        observation.Dispose();
        observation.Dispose();
        Assert.IsTrue(observation.IsDisposed);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void DisposingAnObservationTwiceDoesNotReleaseAnother()
    {
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
        var first = observer.Observe(p => p.Name!.Length, john);
        var second = observer.Observe(p => p.Name!.Length, john);
        first.Dispose();
        first.Dispose();
        Assert.AreNotEqual(0, observer.CachedObservableExpressions);
        john.Name = "Johnathan";
        Assert.AreEqual("Johnathan".Length, second.Evaluation.Result);
        second.Dispose();
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void DisposalNotificationIsScopedToTheObservationDisposed()
    {
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
        var first = observer.Observe(p => p.Name!.Length, john);
        using var second = observer.Observe(p => p.Name!.Length, john);
        var firstDisposals = 0;
        var secondDisposals = 0;
        first.Disposed += (_, _) => ++firstDisposals;
        second.Disposed += (_, _) => ++secondDisposals;
        first.Dispose();
        first.Dispose();
        Assert.AreEqual(1, firstDisposals);
        Assert.AreEqual(0, secondDisposals);
    }
}
