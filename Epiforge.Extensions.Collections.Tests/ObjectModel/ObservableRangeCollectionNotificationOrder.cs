namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

using System.ComponentModel;

[TestClass]
public class ObservableRangeCollectionNotificationOrder
{
    static void AssertTheCollectionIsAnnouncedLast(Action<ObservableRangeCollection<int>> operation)
    {
        AssertTheCollectionIsAnnouncedLast(true, operation);
        AssertTheCollectionIsAnnouncedLast(false, operation);
    }

    static void AssertTheCollectionIsAnnouncedLast(bool individually, Action<ObservableRangeCollection<int>> operation)
    {
        var collection = new ObservableRangeCollection<int>(new[] { 1, 2, 3, 4, 5 }, individually);
        var notifications = new List<string>();
        collection.CollectionChanged += (sender, e) => notifications.Add("the collection");
        ((INotifyPropertyChanged)collection).PropertyChanged += (sender, e) => notifications.Add(e.PropertyName!);
        operation(collection);
        Assert.AreNotEqual(0, notifications.Count, "the operation announced nothing at all");
        Assert.AreEqual("the collection", notifications[^1], $"the last thing announced was {notifications[^1]}, after the collection change it describes");
    }

    [TestMethod]
    public void AddRangeAnnouncesTheCollectionLast() =>
        AssertTheCollectionIsAnnouncedLast(collection => collection.AddRange(new[] { 6, 7 }));

    [TestMethod]
    public void GetAndRemoveAllAnnouncesTheCollectionLast() =>
        AssertTheCollectionIsAnnouncedLast(collection => collection.GetAndRemoveAll(i => i is 2 or 3));

    [TestMethod]
    public void InsertRangeAnnouncesTheCollectionLast() =>
        AssertTheCollectionIsAnnouncedLast(collection => collection.InsertRange(1, new[] { 6, 7 }));

    [TestMethod]
    public void MoveRangeAnnouncesTheCollectionLast() =>
        AssertTheCollectionIsAnnouncedLast(collection => collection.MoveRange(0, 3, 2));

    [TestMethod]
    public void RemoveRangeOfItemsAnnouncesTheCollectionLast() =>
        AssertTheCollectionIsAnnouncedLast(collection => collection.RemoveRange(new[] { 2, 3 }));

    [TestMethod]
    public void RemoveRangeOfPositionsAnnouncesTheCollectionLast() =>
        AssertTheCollectionIsAnnouncedLast(collection => collection.RemoveRange(1, 2));

    [TestMethod]
    public void ReplaceAllAnnouncesTheCollectionLast() =>
        AssertTheCollectionIsAnnouncedLast(collection => collection.ReplaceAll(new[] { 8, 9 }));

    [TestMethod]
    public void ReplaceRangeAnnouncesTheCollectionLast() =>
        AssertTheCollectionIsAnnouncedLast(collection => collection.ReplaceRange(1, 2, new[] { 8, 9, 10 }));

    [TestMethod]
    public void ResetAnnouncesTheCollectionLast() =>
        AssertTheCollectionIsAnnouncedLast(collection => collection.Reset(new[] { 8, 9 }));

    [TestMethod]
    public void ResetRemovingAllAnnouncesTheCollectionLast() =>
        AssertTheCollectionIsAnnouncedLast(collection => collection.ResetRemovingAll(i => i is 2 or 3));
}
