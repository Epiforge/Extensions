namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Which mechanism observes a method call, decided by whether the observer disposes of what that method returned rather than by whether its return type could ever be disposed of at all
/// </summary>
/// <remarks>
/// Disposal is something only the graph performs, so a method whose return value is disposed of must be refused. What the analyzer used to ask instead was whether the return type was sealed and implemented neither disposal interface, which refused every method returning an unsealed type whether or not anything would ever dispose of its value, and which no property read was ever held to. These pin the narrowed rule and, in passing, the attribute nothing else covered
/// </remarks>
[TestClass]
public class MethodReturnValueDisposal
{
    static (object? Result, int Announcements, IReadOnlyList<string> Attachments) ObserveThroughACall(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 2 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        var announcements = 0;
        using (var expression = observer.Observe(s => s.Self().Rank, subject))
        {
            expression.PropertyChanged += (sender, e) => ++announcements;
            Assert.AreEqual(2, expression.Evaluation.Result, "the two forms did not start alike");
            subject.Rank = 7;
            var reported = expression.Evaluation.Result;
            var attachments = log.Attachments();
            Assert.AreNotEqual(0, log.Outstanding, "nothing was subscribed to, so this observation could not have been watching anything");
            return (reported, announcements, attachments);
        }
    }

    [TestMethod]
    public void ACallWhoseReturnValueIsNotDisposedTakesTheFastPath()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 2 };
        var observer = ExpressionObserverHelpers.Create();
        using (var expression = observer.Observe(s => s.Self().Rank, subject))
        {
            Assert.AreEqual(0, observer.CachedObservableExpressions, "the analyzer refused a call whose return value nothing disposes of");
            Assert.AreEqual(2, expression.Evaluation.Result);
            subject.Rank = 7;
            Assert.AreEqual(7, expression.Evaluation.Result);
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    public void ACallWhoseReturnValueIsDisposedBuildsTheGraph()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 2 };
        var observer = ExpressionObserverHelpers.Create();
        using (var expression = observer.Observe(s => s.Held().Rank, subject))
        {
            Assert.AreNotEqual(0, observer.CachedObservableExpressions, "the analyzer admitted a call whose return value the observer disposes of");
            Assert.AreEqual(2, expression.Evaluation.Result);
            subject.Rank = 7;
            Assert.AreEqual(7, expression.Evaluation.Result);
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    public void TheMechanismsAgreeOverACallWhoseReturnValueIsNotDisposed()
    {
        var graph = ObserveThroughACall(false);
        var fast = ObserveThroughACall(true);
        Assert.AreEqual(graph.Result, fast.Result, "the mechanisms report different values through a method call");
        Assert.AreEqual(graph.Announcements, fast.Announcements, "the mechanisms did not announce alike through a method call");
        CollectionAssert.AreEqual(graph.Attachments.ToArray(), fast.Attachments.ToArray(), "the mechanisms did not subscribe to the same sources through a method call");
    }

    /// <summary>
    /// Establishes that the refusal is earning its keep, by showing that a return value declared disposable by attribute alone is in fact disposed of
    /// </summary>
    [TestMethod]
    public void AnAttributedReturnValueIsDisposed()
    {
        SyncDisposableTestPerson person;
        using (var expression = ExpressionObserverHelpers.Create().Observe(() => new Options.TestObject().GetAttributedPersonNamedAfterType(typeof(string))))
        {
            person = expression.Evaluation.Result!;
            Assert.AreEqual(typeof(string).Name, person.Name);
        }
        Assert.IsTrue(person.IsDisposed, "the attribute did not cause the returned value to be disposed of");
    }

    [TestMethod]
    public void AnUnattributedReturnValueIsNotDisposed()
    {
        SyncDisposableTestPerson person;
        using (var expression = ExpressionObserverHelpers.Create().Observe(() => new Options.TestObject().GetPersonNamedAfterType(typeof(string))))
        {
            person = expression.Evaluation.Result!;
            Assert.AreEqual(typeof(string).Name, person.Name);
        }
        Assert.IsFalse(person.IsDisposed, "a return value nothing declared disposable was disposed of anyway");
        person.Dispose();
    }
}
