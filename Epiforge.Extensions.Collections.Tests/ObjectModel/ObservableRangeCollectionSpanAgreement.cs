namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

[TestClass]
public class ObservableRangeCollectionSpanAgreement
{
    const int largestCount = 7;

    static List<NotifyCollectionChangedEventArgs> Announce(ObservableRangeCollection<int> collection)
    {
        var announcements = new List<NotifyCollectionChangedEventArgs>();
        collection.CollectionChanged += (sender, e) => announcements.Add(e);
        return announcements;
    }

    static void AssertInvalidRangeChangesNothing(Action<ObservableRangeCollection<int>> operation, string context)
    {
        var collection = new ObservableRangeCollection<int>(Enumerable.Range(0, 5));
        var announcements = Announce(collection);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => operation(collection), context);
        CollectionAssert.AreEqual(Enumerable.Range(0, 5).ToList(), collection.ToList(), $"{context} changed the collection");
        Assert.AreEqual(0, announcements.Count, $"{context} announced a change");
    }

    [TestMethod]
    public void InsertingARangeAgreesWithAListEveryWay()
    {
        for (var count = 0; count <= largestCount; ++count)
            for (var index = 0; index <= count; ++index)
                for (var inserted = 0; inserted <= 3; ++inserted)
                {
                    var collection = new ObservableRangeCollection<int>(Enumerable.Range(0, count));
                    var expected = Enumerable.Range(0, count).ToList();
                    var newItems = Enumerable.Range(100, inserted).ToList();
                    expected.InsertRange(index, newItems);
                    var announcements = Announce(collection);
                    collection.InsertRange(index, newItems.Select(item => item));
                    var context = $"inserting {inserted} into {count} at {index}";
                    CollectionAssert.AreEqual(expected, collection.ToList(), context);
                    if (inserted == 0)
                    {
                        Assert.AreEqual(0, announcements.Count, $"{context} announced a change");
                        continue;
                    }
                    Assert.AreEqual(1, announcements.Count, context);
                    Assert.AreEqual(NotifyCollectionChangedAction.Add, announcements[0].Action, context);
                    Assert.AreEqual(index, announcements[0].NewStartingIndex, context);
                    CollectionAssert.AreEqual(newItems, announcements[0].NewItems, context);
                }
    }

    [TestMethod]
    public void InvalidRangesChangeNothing()
    {
        AssertInvalidRangeChangesNothing(collection => collection.MoveRange(0, 4, 3), "moving three of five to four");
        AssertInvalidRangeChangesNothing(collection => collection.MoveRange(3, 0, 3), "moving three of five from three");
        AssertInvalidRangeChangesNothing(collection => collection.RemoveRange(3, 5), "removing five of five from three");
        AssertInvalidRangeChangesNothing(collection => collection.ReplaceRange(3, 5, new[] { 100 }), "replacing five of five from three");
    }

    [TestMethod]
    public void MovingARangeAgreesWithAListEveryWay()
    {
        for (var count = 2; count <= largestCount; ++count)
            for (var moved = 2; moved <= count; ++moved)
                for (var oldIndex = 0; oldIndex + moved <= count; ++oldIndex)
                    for (var newIndex = 0; newIndex + moved <= count; ++newIndex)
                    {
                        var collection = new ObservableRangeCollection<int>(Enumerable.Range(0, count));
                        var expected = Enumerable.Range(0, count).ToList();
                        var movedItems = expected.GetRange(oldIndex, moved);
                        expected.RemoveRange(oldIndex, moved);
                        expected.InsertRange(newIndex, movedItems);
                        var announcements = Announce(collection);
                        collection.MoveRange(oldIndex, newIndex, moved);
                        var context = $"moving {moved} of {count} from {oldIndex} to {newIndex}";
                        CollectionAssert.AreEqual(expected, collection.ToList(), context);
                        if (oldIndex == newIndex)
                        {
                            Assert.AreEqual(0, announcements.Count, $"{context} announced a change");
                            continue;
                        }
                        Assert.AreEqual(1, announcements.Count, context);
                        Assert.AreEqual(NotifyCollectionChangedAction.Move, announcements[0].Action, context);
                        Assert.AreEqual(oldIndex, announcements[0].OldStartingIndex, context);
                        Assert.AreEqual(newIndex, announcements[0].NewStartingIndex, context);
                        CollectionAssert.AreEqual(movedItems, announcements[0].OldItems, context);
                    }
    }

    [TestMethod]
    public void RemovingAllMatchesAgreesWithAListEveryWay()
    {
        for (var count = 0; count <= largestCount; ++count)
            for (var mask = 0; mask < 1 << count; ++mask)
            {
                var collection = new ObservableRangeCollection<int>(Enumerable.Range(0, count));
                var reference = Enumerable.Range(0, count).ToList();
                var announcements = Announce(collection);
                var replayed = new List<int>(reference);
                collection.RemoveAll(item => (mask & 1 << item) != 0);
                var context = $"removing mask {mask} from {count}";
                reference.RemoveAll(item => (mask & 1 << item) != 0);
                CollectionAssert.AreEqual(reference, collection.ToList(), context);
                foreach (var announcement in announcements)
                {
                    Assert.AreEqual(NotifyCollectionChangedAction.Remove, announcement.Action, context);
                    CollectionAssert.AreEqual(replayed.GetRange(announcement.OldStartingIndex, announcement.OldItems!.Count), announcement.OldItems, context);
                    replayed.RemoveRange(announcement.OldStartingIndex, announcement.OldItems.Count);
                }
                CollectionAssert.AreEqual(reference, replayed, $"{context} announced removals which do not replay to the collection");
            }
    }

    [TestMethod]
    public void RemovingARangeAgreesWithAListEveryWay()
    {
        for (var count = 0; count <= largestCount; ++count)
            for (var index = 0; index <= count; ++index)
                for (var removed = 0; index + removed <= count; ++removed)
                {
                    var collection = new ObservableRangeCollection<int>(Enumerable.Range(0, count));
                    var expected = Enumerable.Range(0, count).ToList();
                    var oldItems = expected.GetRange(index, removed);
                    expected.RemoveRange(index, removed);
                    var announcements = Announce(collection);
                    collection.RemoveRange(index, removed);
                    var context = $"removing {removed} of {count} from {index}";
                    CollectionAssert.AreEqual(expected, collection.ToList(), context);
                    if (removed == 0)
                    {
                        Assert.AreEqual(0, announcements.Count, $"{context} announced a change");
                        continue;
                    }
                    Assert.AreEqual(1, announcements.Count, context);
                    Assert.AreEqual(NotifyCollectionChangedAction.Remove, announcements[0].Action, context);
                    Assert.AreEqual(index, announcements[0].OldStartingIndex, context);
                    CollectionAssert.AreEqual(oldItems, announcements[0].OldItems, context);
                }
    }

    [TestMethod]
    public void ReplacingARangeAgreesWithAListEveryWay()
    {
        for (var count = 0; count <= largestCount; ++count)
            for (var index = 0; index <= count; ++index)
                for (var replaced = 0; index + replaced <= count; ++replaced)
                    for (var inserted = 0; inserted <= 3; ++inserted)
                    {
                        var collection = new ObservableRangeCollection<int>(Enumerable.Range(0, count));
                        var expected = Enumerable.Range(0, count).ToList();
                        var oldItems = expected.GetRange(index, replaced);
                        var newItems = Enumerable.Range(100, inserted).ToList();
                        expected.RemoveRange(index, replaced);
                        expected.InsertRange(index, newItems);
                        var announcements = Announce(collection);
                        var returned = collection.ReplaceRange(index, replaced, newItems.Select(item => item));
                        var context = $"replacing {replaced} of {count} from {index} with {inserted}";
                        CollectionAssert.AreEqual(expected, collection.ToList(), context);
                        CollectionAssert.AreEqual(oldItems, returned.ToList(), $"{context} did not return what it replaced");
                        var replayed = Enumerable.Range(0, count).ToList();
                        foreach (var announcement in announcements)
                            switch (announcement.Action)
                            {
                                case NotifyCollectionChangedAction.Replace:
                                    CollectionAssert.AreEqual(replayed.GetRange(announcement.OldStartingIndex, announcement.OldItems!.Count), announcement.OldItems, context);
                                    replayed.RemoveRange(announcement.OldStartingIndex, announcement.OldItems.Count);
                                    replayed.InsertRange(announcement.NewStartingIndex, announcement.NewItems!.Cast<int>());
                                    break;
                                case NotifyCollectionChangedAction.Remove:
                                    CollectionAssert.AreEqual(replayed.GetRange(announcement.OldStartingIndex, announcement.OldItems!.Count), announcement.OldItems, context);
                                    replayed.RemoveRange(announcement.OldStartingIndex, announcement.OldItems.Count);
                                    break;
                                case NotifyCollectionChangedAction.Add:
                                    replayed.InsertRange(announcement.NewStartingIndex, announcement.NewItems!.Cast<int>());
                                    break;
                                default:
                                    Assert.Fail($"{context} announced {announcement.Action}");
                                    break;
                            }
                        CollectionAssert.AreEqual(expected, replayed, $"{context} announced changes which do not replay to the collection");
                    }
    }
}
