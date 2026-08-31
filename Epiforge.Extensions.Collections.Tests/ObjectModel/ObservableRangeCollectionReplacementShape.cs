namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

[TestClass]
public class ObservableRangeCollectionReplacementShape
{
    static string EventsRaisedBy(Action<ObservableRangeCollection<int>> operation, IEnumerable<int> source)
    {
        var collection = new ObservableRangeCollection<int>(source);
        var events = new List<string>();
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e) =>
            events.Add($"{e.Action} {e.OldItems?.Count ?? 0}@{e.OldStartingIndex} {e.NewItems?.Count ?? 0}@{e.NewStartingIndex}");
        collection.CollectionChanged += collectionChangedHandler;
        operation(collection);
        collection.CollectionChanged -= collectionChangedHandler;
        return string.Join(", ", events);
    }

    [TestMethod]
    public void FewerItemsThanItReplacesReplacesWhatItCanAndRemovesTheRest() =>
        Assert.AreEqual("Replace 2@2 2@2, Remove 2@4 0@-1", EventsRaisedBy(collection => collection.ReplaceRange(2, 4, new[] { 11, 12 }), Enumerable.Range(1, 10)));

    [TestMethod]
    public void MoreItemsThanItReplacesReplacesWhatItCanAndAddsTheRest() =>
        Assert.AreEqual("Replace 2@2 2@2, Add 0@-1 2@4", EventsRaisedBy(collection => collection.ReplaceRange(2, 2, new[] { 11, 12, 13, 14 }), Enumerable.Range(1, 10)));

    [TestMethod]
    public void NoItemsAtAllIsSilent() =>
        Assert.AreEqual(string.Empty, EventsRaisedBy(collection => collection.ReplaceAll(Array.Empty<int>()), Array.Empty<int>()));

    [TestMethod]
    public void NothingToReplaceIsAnAddition() =>
        Assert.AreEqual("Add 0@-1 2@0", EventsRaisedBy(collection => collection.ReplaceAll(new[] { 11, 12 }), Array.Empty<int>()));

    [TestMethod]
    public void ReplacingEverythingWithNothingIsARemoval() =>
        Assert.AreEqual("Remove 10@0 0@-1", EventsRaisedBy(collection => collection.ReplaceAll(Array.Empty<int>()), Enumerable.Range(1, 10)));

    [TestMethod]
    public void TheSameNumberOfItemsIsOneReplacement() =>
        Assert.AreEqual("Replace 10@0 10@0", EventsRaisedBy(collection => collection.ReplaceAll(Enumerable.Range(11, 10)), Enumerable.Range(1, 10)));

    [TestMethod]
    public void TheSameNumberOfItemsInARangeIsOneReplacement() =>
        Assert.AreEqual("Replace 3@2 3@2", EventsRaisedBy(collection => collection.ReplaceRange(2, 3, new[] { 11, 12, 13 }), Enumerable.Range(1, 10)));

    [TestMethod]
    public void EveryReplacementLeavesTheCollectionTheSameInBothModes()
    {
        foreach (var operation in new Action<ObservableRangeCollection<int>>[]
        {
            collection => collection.ReplaceAll(new[] { 11, 12 }),
            collection => collection.ReplaceAll(Enumerable.Range(11, 10)),
            collection => collection.ReplaceRange(2, 4, new[] { 11, 12 }),
            collection => collection.ReplaceRange(2, 2, new[] { 11, 12, 13, 14 }),
            collection => collection.ReplaceRange(2, 3, new[] { 11, 12, 13 })
        })
        {
            var batched = new ObservableRangeCollection<int>(Enumerable.Range(1, 10), false);
            var individual = new ObservableRangeCollection<int>(Enumerable.Range(1, 10), true);
            operation(batched);
            operation(individual);
            CollectionAssert.AreEqual(individual.ToArray(), batched.ToArray(), "the two modes did not leave the same collection");
        }
    }
}
