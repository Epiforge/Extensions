namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

[TestClass]
public class ObservableRangeCollectionSingleMove
{
    static void AssertMovingOneElementAgreesWithAList(int count, int oldIndex, int newIndex)
    {
        var collection = new ObservableRangeCollection<int>(Enumerable.Range(0, count));
        var expected = Enumerable.Range(0, count).ToList();
        var moved = expected[oldIndex];
        expected.RemoveAt(oldIndex);
        expected.Insert(newIndex, moved);
        var announcements = new List<NotifyCollectionChangedEventArgs>();
        var properties = new List<string>();
        collection.CollectionChanged += (sender, e) => announcements.Add(e);
        ((System.ComponentModel.INotifyPropertyChanged)collection).PropertyChanged += (sender, e) => properties.Add(e.PropertyName!);
        collection.MoveRange(oldIndex, newIndex, 1);
        var context = $"moving one of {count} from {oldIndex} to {newIndex}";
        CollectionAssert.AreEqual(expected, collection.ToList(), context);
        if (oldIndex == newIndex)
        {
            Assert.AreEqual(0, announcements.Count, $"{context} announced a change");
            Assert.AreEqual(0, properties.Count, $"{context} announced a property");
            return;
        }
        Assert.AreEqual(1, announcements.Count, context);
        var announcement = announcements[0];
        Assert.AreEqual(NotifyCollectionChangedAction.Move, announcement.Action, context);
        Assert.AreEqual(oldIndex, announcement.OldStartingIndex, context);
        Assert.AreEqual(newIndex, announcement.NewStartingIndex, context);
        CollectionAssert.AreEqual(new[] { moved }, announcement.OldItems, context);
        CollectionAssert.AreEqual(new[] { moved }, announcement.NewItems, context);
        CollectionAssert.AreEqual(new[] { "Item[]" }, properties, context);
    }

    [TestMethod]
    public void MovingOneElementAgreesWithAListEveryWay()
    {
        for (var count = 1; count <= 8; ++count)
            for (var oldIndex = 0; oldIndex < count; ++oldIndex)
                for (var newIndex = 0; newIndex < count; ++newIndex)
                    AssertMovingOneElementAgreesWithAList(count, oldIndex, newIndex);
    }

    [TestMethod]
    public void MovingOneElementFarAgreesWithAList()
    {
        AssertMovingOneElementAgreesWithAList(1000, 0, 999);
        AssertMovingOneElementAgreesWithAList(1000, 999, 0);
        AssertMovingOneElementAgreesWithAList(1000, 250, 750);
        AssertMovingOneElementAgreesWithAList(1000, 750, 250);
    }
}
