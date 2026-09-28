namespace Epiforge.Extensions.Comparisons.Tests;

/// <summary>
/// Whether a projected view disposes of the values it makes from its elements, when a change replaces one, when an element leaves, and when the view is torn down
/// </summary>
/// <remarks>
/// The view makes a <see cref="Handle"/> from each of three elements, then remakes the first element's when its rank changes and discards the second's when the element is removed, so four are made and two discarded before the view is torn down
/// </remarks>
[TestClass]
public class MadeValueDisposal
{
    static void Change(ObservableCollection<Person> people)
    {
        people[0].Rank = 10;
        people.RemoveAt(1);
    }

    [TestMethod]
    public void DynamicDataDisposesOfEveryValueWhenAsked()
    {
        var ledger = new Handle.Ledger();
        var people = Person.Many(1, 2, 3);
        var subscription = people.ToObservableChangeSet().AutoRefresh(person => person.Rank).Transform(person => new Handle(ledger, person.Rank), true).DisposeMany().Bind(out var view).Subscribe();
        Change(people);
        Assert.AreEqual((4, 2), (ledger.Made, ledger.Disposed));
        subscription.Dispose();
        Assert.AreEqual((4, 4), (ledger.Made, ledger.Disposed));
    }

    [TestMethod]
    public void DynamicDataDisposesOfNothingUnlessAsked()
    {
        var ledger = new Handle.Ledger();
        var people = Person.Many(1, 2, 3);
        var subscription = people.ToObservableChangeSet().AutoRefresh(person => person.Rank).Transform(person => new Handle(ledger, person.Rank), true).Bind(out var view).Subscribe();
        Change(people);
        subscription.Dispose();
        Assert.AreEqual((4, 0), (ledger.Made, ledger.Disposed));
    }

    [TestMethod]
    public void ExpressionsDisposesOfEveryValueWithoutBeingAsked()
    {
        var ledger = new Handle.Ledger();
        var people = Person.Many(1, 2, 3);
        var observation = new CollectionObserver().ObserveReadOnlyList(people);
        var view = observation.ObserveSelect(person => new Handle(ledger, person.Rank));
        Change(people);
        Assert.AreEqual((4, 2), (ledger.Made, ledger.Disposed));
        view.Dispose();
        observation.Dispose();
        Assert.AreEqual((4, 4), (ledger.Made, ledger.Disposed));
    }

    [TestMethod]
    public void NmfDisposesOfOneDiscardedValueAndNoneAtTeardown()
    {
        var ledger = new Handle.Ledger();
        var people = Person.Many(1, 2, 3);
        var view = ((IEnumerable<Person>)people).WithUpdates().Select(person => new Handle(ledger, person.Rank));
        view.Successors.SetDummy();
        Change(people);
        Assert.AreEqual((4, 1), (ledger.Made, ledger.Disposed));
        view.Dispose();
        Assert.AreEqual((4, 1), (ledger.Made, ledger.Disposed));
    }

    [TestMethod]
    public void ObservableComputationsDisposesOfEveryValueWhenAsked()
    {
        var ledger = new Handle.Ledger();
        var people = Person.Many(1, 2, 3);
        var consumer = new OcConsumer();
        people.Selecting(person => new Handle(ledger, person.Rank)).CollectionDisposing().For(consumer);
        Change(people);
        Assert.AreEqual((4, 2), (ledger.Made, ledger.Disposed));
        consumer.Dispose();
        Assert.AreEqual((4, 4), (ledger.Made, ledger.Disposed));
    }

    [TestMethod]
    public void ObservableComputationsDisposesOfNothingUnlessAsked()
    {
        var ledger = new Handle.Ledger();
        var people = Person.Many(1, 2, 3);
        var consumer = new OcConsumer();
        people.Selecting(person => new Handle(ledger, person.Rank)).For(consumer);
        Change(people);
        consumer.Dispose();
        Assert.AreEqual((4, 0), (ledger.Made, ledger.Disposed));
    }
}
