namespace Epiforge.Extensions.Comparisons.Tests;

/// <summary>
/// What a filtered view does when its predicate throws for one element, while the view is built and when a change makes it throw, and whether the view recovers once the element no longer makes it throw
/// </summary>
/// <remarks>
/// The predicate is <c>10 / person.Score &gt; 1</c>, which throws <see cref="DivideByZeroException"/> for an element whose score is zero. Every test asserts what its library does, so a test failing means that library now behaves differently from what the readmes say of it
/// </remarks>
[TestClass]
public class Faults
{
    static readonly Expression<Func<Person, bool>> dividing = person => 10 / person.Score > 1;
    static readonly Func<Person, bool> dividingDelegate = dividing.Compile();

    static void Add(ObservableCollection<Person> people) =>
        people.Add(new Person("P3", 1));

    [TestMethod]
    public void DynamicDataEndsTheViewWhenAChangeMakesThePredicateThrow()
    {
        var people = Person.Many(1, 2, 3);
        var errors = new List<Exception>();
        using var subscription = people.ToObservableChangeSet().AutoRefresh(person => person.Score).Filter(dividingDelegate).Bind(out var view).Subscribe(_ => { }, errors.Add);
        people[1].Score = 0;
        Assert.AreEqual(1, errors.Count);
        Assert.IsInstanceOfType<DivideByZeroException>(errors[0]);
        people[1].Score = 2;
        Add(people);
        Assert.AreEqual("P0,P1,P2", Person.Names(view));
    }

    [TestMethod]
    public void DynamicDataEndsTheViewWhenThePredicateThrowsWhileItIsBuilt()
    {
        var people = Person.Many(1, 2, 3);
        people[1].Score = 0;
        var errors = new List<Exception>();
        using var subscription = people.ToObservableChangeSet().AutoRefresh(person => person.Score).Filter(dividingDelegate).Bind(out var view).Subscribe(_ => { }, errors.Add);
        Assert.AreEqual(1, errors.Count);
        Assert.IsInstanceOfType<DivideByZeroException>(errors[0]);
        Assert.AreEqual(string.Empty, Person.Names(view));
        people[1].Score = 2;
        Add(people);
        Assert.AreEqual(string.Empty, Person.Names(view));
    }

    [TestMethod]
    public void DynamicDataThrowsIntoTheSetterWhenNothingHandlesItsError()
    {
        var people = Person.Many(1, 2, 3);
        using var subscription = people.ToObservableChangeSet().AutoRefresh(person => person.Score).Filter(dividingDelegate).Bind(out var view).Subscribe();
        Assert.ThrowsException<DivideByZeroException>(() => people[1].Score = 0);
    }

    [TestMethod]
    public void ExpressionsReportsAFaultFromAChangeWithoutThrowingAndRecovers()
    {
        var people = Person.Many(1, 2, 3);
        using var observation = new CollectionObserver().ObserveReadOnlyList(people);
        using var view = observation.ObserveWhere(dividing);
        people[1].Score = 0;
        Assert.AreEqual("P0,P2", Person.Names(view));
        Assert.IsNotNull(view.OperationFault);
        people[1].Score = 2;
        Add(people);
        Assert.AreEqual("P0,P1,P2,P3", Person.Names(view));
        Assert.IsNull(view.OperationFault);
    }

    [TestMethod]
    public void ExpressionsReportsAFaultWhileBuildingWithoutThrowingAndRecovers()
    {
        var people = Person.Many(1, 2, 3);
        people[1].Score = 0;
        using var observation = new CollectionObserver().ObserveReadOnlyList(people);
        using var view = observation.ObserveWhere(dividing);
        Assert.AreEqual("P0,P2", Person.Names(view));
        Assert.IsNotNull(view.OperationFault);
        people[1].Score = 2;
        Assert.AreEqual("P0,P1,P2", Person.Names(view));
        Assert.IsNull(view.OperationFault);
    }

    [TestMethod]
    public void NmfThrowsIntoTheSetterWhenAChangeMakesThePredicateThrowAndRecovers()
    {
        var people = Person.Many(1, 2, 3);
        using var view = ((IEnumerable<Person>)people).WithUpdates().Where(dividing);
        view.Successors.SetDummy();
        Assert.ThrowsException<DivideByZeroException>(() => people[1].Score = 0);
        people[1].Score = 2;
        Add(people);
        Assert.AreEqual("P0,P1,P2,P3", Person.Names(view));
    }

    [TestMethod]
    public void NmfThrowsOutOfBuildingWhenThePredicateThrows()
    {
        var people = Person.Many(1, 2, 3);
        people[1].Score = 0;
        Assert.ThrowsException<DivideByZeroException>(() =>
        {
            var view = ((IEnumerable<Person>)people).WithUpdates().Where(dividing);
            view.Successors.SetDummy();
        });
    }

    [TestMethod]
    public void ObservableComputationsThrowsIntoTheSetterWhenAChangeMakesThePredicateThrowAndMissesAnElementAddedAfterward()
    {
        var people = Person.Many(1, 2, 3);
        using var consumer = new OcConsumer();
        var view = people.Filtering(dividing).For(consumer);
        Assert.ThrowsException<DivideByZeroException>(() => people[1].Score = 0);
        people[1].Score = 2;
        Add(people);
        Assert.AreEqual("P0,P1,P2", Person.Names(view));
    }

    [TestMethod]
    public void ObservableComputationsThrowsOutOfBuildingWhenThePredicateThrows()
    {
        var people = Person.Many(1, 2, 3);
        people[1].Score = 0;
        using var consumer = new OcConsumer();
        Assert.ThrowsException<DivideByZeroException>(() => people.Filtering(dividing).For(consumer));
    }
}
