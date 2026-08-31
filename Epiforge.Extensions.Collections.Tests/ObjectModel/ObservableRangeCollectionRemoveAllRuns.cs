namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

[TestClass]
public class ObservableRangeCollectionRemoveAllRuns
{
    static string EventsRaisedBy(bool individually, Func<int, bool> predicate, out IReadOnlyList<int> removed, out IReadOnlyList<int> remaining)
    {
        var collection = new ObservableRangeCollection<int>(Enumerable.Range(1, 10), individually);
        var events = new List<string>();
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, e.Action);
            Assert.IsNotNull(e.OldItems);
            events.Add($"{e.OldStartingIndex}:{string.Join("+", e.OldItems.Cast<int>())}");
        }
        collection.CollectionChanged += collectionChangedHandler;
        removed = collection.GetAndRemoveAll(predicate);
        collection.CollectionChanged -= collectionChangedHandler;
        remaining = [..collection];
        return string.Join(", ", events);
    }

    static void AssertModesRemoveTheSameItems(Func<int, bool> predicate)
    {
        var batched = EventsRaisedBy(false, predicate, out var batchedRemoved, out var batchedRemaining);
        var individual = EventsRaisedBy(true, predicate, out var individualRemoved, out var individualRemaining);
        CollectionAssert.AreEqual(individualRemoved.ToArray(), batchedRemoved.ToArray(), "the two modes did not remove the same items in the same order");
        CollectionAssert.AreEqual(individualRemaining.ToArray(), batchedRemaining.ToArray(), "the two modes did not leave the same collection");
        Assert.AreNotEqual(string.Empty, batched);
        Assert.AreNotEqual(string.Empty, individual);
    }

    [TestMethod]
    public void AContiguousBlockIsOneEvent()
    {
        var events = EventsRaisedBy(false, i => i is >= 3 and <= 5, out var removed, out var remaining);
        Assert.AreEqual("2:3+4+5", events);
        CollectionAssert.AreEqual(new[] { 3, 4, 5 }, removed.ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 6, 7, 8, 9, 10 }, remaining.ToArray());
    }

    [TestMethod]
    public void AlternatingMatchesAreOneEventEach()
    {
        var events = EventsRaisedBy(false, i => i % 2 == 0, out var removed, out var remaining);
        Assert.AreEqual("1:2, 2:4, 3:6, 4:8, 5:10", events);
        CollectionAssert.AreEqual(new[] { 2, 4, 6, 8, 10 }, removed.ToArray());
        CollectionAssert.AreEqual(new[] { 1, 3, 5, 7, 9 }, remaining.ToArray());
    }

    [TestMethod]
    public void EverythingMatchingIsOneEvent()
    {
        var events = EventsRaisedBy(false, i => true, out var removed, out var remaining);
        Assert.AreEqual("0:1+2+3+4+5+6+7+8+9+10", events);
        Assert.AreEqual(10, removed.Count);
        Assert.AreEqual(0, remaining.Count);
    }

    [TestMethod]
    public void IndividualModeNeverRaisesARange()
    {
        var events = EventsRaisedBy(true, i => i is >= 3 and <= 5, out var removed, out var remaining);
        Assert.AreEqual("2:3, 2:4, 2:5", events, "individual mode raised an event carrying more than one item");
        CollectionAssert.AreEqual(new[] { 3, 4, 5 }, removed.ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 6, 7, 8, 9, 10 }, remaining.ToArray());
    }

    [TestMethod]
    public void NeitherModeDiffersInWhatItRemoves()
    {
        AssertModesRemoveTheSameItems(i => i is >= 3 and <= 5);
        AssertModesRemoveTheSameItems(i => i % 2 == 0);
        AssertModesRemoveTheSameItems(i => i is >= 2 and <= 3 or >= 7 and <= 8);
        AssertModesRemoveTheSameItems(i => i >= 9);
        AssertModesRemoveTheSameItems(i => i <= 2);
    }

    [TestMethod]
    [Timeout(5000)]
    public void RemoveAllCountsWithoutCollectingInEitherMode()
    {
        foreach (var individually in new[] { true, false })
        {
            var collection = new ObservableRangeCollection<int>(Enumerable.Range(1, 10), individually);
            Assert.AreEqual(3, collection.RemoveAll(i => i % 3 == 0));
            CollectionAssert.AreEqual(new[] { 1, 2, 4, 5, 7, 8, 10 }, collection.ToArray());
        }
    }

    [TestMethod]
    public void SeparatedBlocksAreOneEventEach()
    {
        var events = EventsRaisedBy(false, i => i is >= 2 and <= 3 or >= 7 and <= 8, out var removed, out var remaining);
        Assert.AreEqual("1:2+3, 4:7+8", events);
        CollectionAssert.AreEqual(new[] { 2, 3, 7, 8 }, removed.ToArray());
        CollectionAssert.AreEqual(new[] { 1, 4, 5, 6, 9, 10 }, remaining.ToArray());
    }

    [TestMethod]
    public void TheFinalBlockIsOneEvent()
    {
        var events = EventsRaisedBy(false, i => i >= 9, out var removed, out var remaining);
        Assert.AreEqual("8:9+10", events);
        CollectionAssert.AreEqual(new[] { 9, 10 }, removed.ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, remaining.ToArray());
    }

    [TestMethod]
    public void TheLeadingBlockIsOneEvent()
    {
        var events = EventsRaisedBy(false, i => i <= 2, out var removed, out var remaining);
        Assert.AreEqual("0:1+2", events);
        CollectionAssert.AreEqual(new[] { 1, 2 }, removed.ToArray());
        CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 7, 8, 9, 10 }, remaining.ToArray());
    }
}
