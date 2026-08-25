namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ArgumentIdentity
{
    #region TestMethod Classes

    // an entity compared by identity value, as line-of-business types commonly are
    public class IdentifiedTestPerson :
        PropertyChangeNotifier
    {
        public IdentifiedTestPerson(int id, string name)
        {
            this.id = id;
            this.name = name;
        }

        readonly int id;
        string? name;

        public string? Name
        {
            get => name;
            set => SetBackedProperty(ref name, in value);
        }

        public override bool Equals(object? obj) =>
            obj is IdentifiedTestPerson other && other.id == id;

        public override int GetHashCode() =>
            id;
    }

    #endregion TestMethod Classes

    [TestMethod]
    public void EqualButDistinctArgumentsAreObservedIndependently()
    {
        var first = new IdentifiedTestPerson(1, "John");
        var second = new IdentifiedTestPerson(1, "John");
        Assert.AreEqual(first, second);
        Assert.AreNotSame(first, second);
        var observer = ExpressionObserverHelpers.Create();
        using var observingFirst = observer.Observe(p => p.Name!.Length, first);
        using var observingSecond = observer.Observe(p => p.Name!.Length, second);
        Assert.AreEqual(4, observingSecond.Evaluation.Result);

        // a change to the object actually being observed must be seen
        second.Name = "Johnathan";
        Assert.AreEqual(9, observingSecond.Evaluation.Result);

        // and a change to the other object must not be
        first.Name = "Jo";
        Assert.AreEqual(9, observingSecond.Evaluation.Result);
        Assert.AreEqual(2, observingFirst.Evaluation.Result);
    }

    [TestMethod]
    public void TheSameArgumentInstanceStillSharesOneObservation()
    {
        var person = new IdentifiedTestPerson(1, "John");
        var observer = ExpressionObserverHelpers.Create();
        using var first = observer.Observe(p => p.Name!.Length, person);
        var cachedAfterFirstObservation = observer.CachedObservableExpressions;
        using var second = observer.Observe(p => p.Name!.Length, person);
        Assert.AreEqual(cachedAfterFirstObservation, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void IdenticalLiteralsInPredicatesStillShareOneObservation()
    {
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
        using var first = observer.Observe(p => p.Name!.Length == 4, john);
        var cachedAfterFirstObservation = observer.CachedObservableExpressions;
        using var second = observer.Observe(p => p.Name!.Length == 4, john);
        Assert.AreEqual(cachedAfterFirstObservation, observer.CachedObservableExpressions);
    }
}
