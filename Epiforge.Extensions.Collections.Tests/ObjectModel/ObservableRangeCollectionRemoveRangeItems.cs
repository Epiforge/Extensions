namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

[TestClass]
public class ObservableRangeCollectionRemoveRangeItems
{
    static string EventsRaisedBy(bool individually, IEnumerable<int> source, IEnumerable<int> removing, out IReadOnlyList<int> remaining)
    {
        var collection = new ObservableRangeCollection<int>(source, individually);
        var events = new List<string>();
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, e.Action);
            Assert.IsNotNull(e.OldItems);
            if (individually)
                Assert.AreEqual(1, e.OldItems.Count, "individual mode raised an event carrying more than one item");
            events.Add($"{e.OldStartingIndex}:{string.Join("+", e.OldItems.Cast<int>())}");
        }
        collection.CollectionChanged += collectionChangedHandler;
        collection.RemoveRange(removing);
        collection.CollectionChanged -= collectionChangedHandler;
        remaining = [..collection];
        return string.Join(", ", events);
    }

    static void AssertModesLeaveTheSameCollection(IEnumerable<int> source, IEnumerable<int> removing)
    {
        EventsRaisedBy(false, source, removing, out var batchedRemaining);
        EventsRaisedBy(true, source, removing, out var individualRemaining);
        CollectionAssert.AreEqual(individualRemaining.ToArray(), batchedRemaining.ToArray(), "the two modes did not leave the same collection");
    }

    [TestMethod]
    public void AdjacentItemsAreOneEvent()
    {
        var events = EventsRaisedBy(false, Enumerable.Range(1, 10), new[] { 3, 4, 5 }, out var remaining);
        Assert.AreEqual("2:3+4+5", events);
        CollectionAssert.AreEqual(new[] { 1, 2, 6, 7, 8, 9, 10 }, remaining.ToArray());
    }

    [TestMethod]
    public void ANullIsRemovedLikeAnyOtherItem()
    {
        var collection = new ObservableRangeCollection<string?>(new[] { "a", null, "b", null });
        collection.RemoveRange(new string?[] { null });
        CollectionAssert.AreEqual(new[] { "a", "b", null }, collection.ToArray());
    }

    [TestMethod]
    public void AnItemWhichIsItselfAListIsOneItem()
    {
        var items = new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5, 6 } };
        foreach (var individually in new[] { true, false })
        {
            var collection = new ObservableRangeCollection<int[]>(items, individually);
            var reported = 0;
            void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
            {
                reported = e.OldItems!.Count;
                Assert.AreSame(items[1], e.OldItems[0]);
            }
            collection.CollectionChanged += collectionChangedHandler;
            collection.RemoveRange(new[] { items[1] });
            collection.CollectionChanged -= collectionChangedHandler;
            Assert.AreEqual(1, reported, $"removing one item reported {reported} of them because the item is itself a list");
        }
    }

    [TestMethod]
    public void DuplicatesRemoveThatManyOccurrences()
    {
        var events = EventsRaisedBy(false, new[] { 1, 2, 2, 3, 2 }, new[] { 2, 2 }, out var remaining);
        Assert.AreEqual("1:2+2", events);
        CollectionAssert.AreEqual(new[] { 1, 3, 2 }, remaining.ToArray());
    }

    [TestMethod]
    public void IndividualModeNeverCoalescesAdjacentItems()
    {
        var events = EventsRaisedBy(true, Enumerable.Range(1, 10), new[] { 4, 5, 6 }, out var remaining);
        Assert.AreEqual("3:4, 3:5, 3:6", events);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 7, 8, 9, 10 }, remaining.ToArray());
    }

    [TestMethod]
    public void IndividualModeReportsInTheOrderSupplied()
    {
        var events = EventsRaisedBy(true, Enumerable.Range(1, 10), new[] { 5, 3 }, out var remaining);
        Assert.AreEqual("4:5, 2:3", events);
        CollectionAssert.AreEqual(new[] { 1, 2, 4, 6, 7, 8, 9, 10 }, remaining.ToArray());
    }

    [TestMethod]
    public void ItemsAbsentFromTheCollectionAreIgnored()
    {
        var events = EventsRaisedBy(false, Enumerable.Range(1, 5), new[] { 3, 99 }, out var remaining);
        Assert.AreEqual("2:3", events);
        CollectionAssert.AreEqual(new[] { 1, 2, 4, 5 }, remaining.ToArray());
    }

    [TestMethod]
    public void NeitherModeDiffersInWhatItLeaves()
    {
        AssertModesLeaveTheSameCollection(Enumerable.Range(1, 10), new[] { 3, 4, 5 });
        AssertModesLeaveTheSameCollection(Enumerable.Range(1, 10), new[] { 5, 3, 9 });
        AssertModesLeaveTheSameCollection(new[] { 1, 2, 2, 3, 2 }, new[] { 2, 2 });
        AssertModesLeaveTheSameCollection(Enumerable.Range(1, 5), new[] { 3, 99 });
        AssertModesLeaveTheSameCollection(Enumerable.Range(1, 5), Array.Empty<int>());
    }

    [TestMethod]
    public void NothingSuppliedIsSilent()
    {
        var events = EventsRaisedBy(false, Enumerable.Range(1, 5), Array.Empty<int>(), out var remaining);
        Assert.AreEqual(string.Empty, events);
        Assert.AreEqual(5, remaining.Count);
    }

    [TestMethod]
    public void ScatteredItemsAreReportedInCollectionOrder()
    {
        var events = EventsRaisedBy(false, Enumerable.Range(1, 10), new[] { 9, 5, 3 }, out var remaining);
        Assert.AreEqual("2:3, 3:5, 6:9", events);
        CollectionAssert.AreEqual(new[] { 1, 2, 4, 6, 7, 8, 10 }, remaining.ToArray());
    }
}
