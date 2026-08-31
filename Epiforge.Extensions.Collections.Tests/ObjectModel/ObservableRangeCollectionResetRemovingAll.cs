namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

using System.ComponentModel;

[TestClass]
public class ObservableRangeCollectionResetRemovingAll
{
    static string EventsRaisedBy(bool individually, Func<ObservableRangeCollection<int>, int> operation, out int returned, out IReadOnlyList<int> remaining)
    {
        var collection = new ObservableRangeCollection<int>(Enumerable.Range(1, 10), individually);
        var events = new List<string>();
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e) =>
            events.Add(e.Action.ToString());
        void propertyChangedHandler(object? sender, PropertyChangedEventArgs e) =>
            events.Add(e.PropertyName!);
        collection.CollectionChanged += collectionChangedHandler;
        ((INotifyPropertyChanged)collection).PropertyChanged += propertyChangedHandler;
        returned = operation(collection);
        collection.CollectionChanged -= collectionChangedHandler;
        ((INotifyPropertyChanged)collection).PropertyChanged -= propertyChangedHandler;
        remaining = [..collection];
        return string.Join(", ", events);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ANullPredicateThrows() =>
        new ObservableRangeCollection<int>().ResetRemovingAll(null!);

    [TestMethod]
    public void EveryMatchLeavesAndTheSurvivorsKeepTheirOrder()
    {
        var events = EventsRaisedBy(false, collection => collection.ResetRemovingAll(i => i % 3 == 0), out var returned, out var remaining);
        Assert.AreEqual("Reset, Count, Item[]", events);
        Assert.AreEqual(3, returned);
        CollectionAssert.AreEqual(new[] { 1, 2, 4, 5, 7, 8, 10 }, remaining.ToArray());
    }

    [TestMethod]
    public void MatchingEverythingEmptiesTheCollection()
    {
        var events = EventsRaisedBy(false, collection => collection.ResetRemovingAll(i => true), out var returned, out var remaining);
        Assert.AreEqual("Reset, Count, Item[]", events);
        Assert.AreEqual(10, returned);
        Assert.AreEqual(0, remaining.Count);
    }

    [TestMethod]
    public void MatchingNothingIsSilent()
    {
        var events = EventsRaisedBy(false, collection => collection.ResetRemovingAll(i => false), out var returned, out var remaining);
        Assert.AreEqual(string.Empty, events, "a call which removed nothing announced something");
        Assert.AreEqual(0, returned);
        Assert.AreEqual(10, remaining.Count);
    }

    [TestMethod]
    public void MatchingTheFirstItemLeavesTheRest()
    {
        var events = EventsRaisedBy(false, collection => collection.ResetRemovingAll(i => i == 1), out var returned, out var remaining);
        Assert.AreEqual("Reset, Count, Item[]", events);
        Assert.AreEqual(1, returned);
        CollectionAssert.AreEqual(new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 }, remaining.ToArray());
    }

    [TestMethod]
    public void MatchingTheLastItemLeavesTheRest()
    {
        var events = EventsRaisedBy(false, collection => collection.ResetRemovingAll(i => i == 10), out var returned, out var remaining);
        Assert.AreEqual("Reset, Count, Item[]", events);
        Assert.AreEqual(1, returned);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, remaining.ToArray());
    }

    [TestMethod]
    public void NeitherModeChangesWhatIsAnnounced()
    {
        var batched = EventsRaisedBy(false, collection => collection.ResetRemovingAll(i => i % 3 == 0), out var batchedReturned, out var batchedRemaining);
        var individual = EventsRaisedBy(true, collection => collection.ResetRemovingAll(i => i % 3 == 0), out var individualReturned, out var individualRemaining);
        Assert.AreEqual(batched, individual, "the constructor flag changed what this method announced");
        Assert.AreEqual(batchedReturned, individualReturned);
        CollectionAssert.AreEqual(batchedRemaining.ToArray(), individualRemaining.ToArray());
    }

    [TestMethod]
    public void OneEventDescribesAnyNumberOfRemovals()
    {
        var collection = new ObservableRangeCollection<int>(Enumerable.Range(1, 1000));
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, e.Action);
        }
        collection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(500, collection.ResetRemovingAll(i => i % 2 == 0));
        collection.CollectionChanged -= collectionChangedHandler;
        Assert.AreEqual(1, collectionChanged);
        Assert.AreEqual(500, collection.Count);
    }

    [TestMethod]
    public void ThePredicateSeesEveryItemExactlyOnce()
    {
        var seen = new List<int>();
        var collection = new ObservableRangeCollection<int>(Enumerable.Range(1, 10));
        collection.ResetRemovingAll(i =>
        {
            seen.Add(i);
            return i % 3 == 0;
        });
        CollectionAssert.AreEqual(Enumerable.Range(1, 10).ToArray(), seen.ToArray());
    }
}
