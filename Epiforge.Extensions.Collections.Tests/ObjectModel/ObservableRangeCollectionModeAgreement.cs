namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

using System.ComponentModel;

[TestClass]
public class ObservableRangeCollectionModeAgreement
{
    static void AssertModesNotifyTheSameProperties(Action<ObservableRangeCollection<int>> operation) =>
        Assert.AreEqual(PropertiesChangedBy(true, operation), PropertiesChangedBy(false, operation), "the batch mode did not notify what the individual mode notified");

    static string PropertiesChangedBy(bool individually, Action<ObservableRangeCollection<int>> operation)
    {
        var collection = new ObservableRangeCollection<int>(new[] { 1, 2, 3, 4, 5 }, individually);
        var properties = new List<string>();
        ((INotifyPropertyChanged)collection).PropertyChanged += (sender, e) => properties.Add(e.PropertyName!);
        operation(collection);
        return string.Join(", ", properties.Distinct().OrderBy(name => name, StringComparer.Ordinal));
    }

    [TestMethod]
    public void AddRangeNotifiesTheSameProperties() =>
        AssertModesNotifyTheSameProperties(collection => collection.AddRange(new[] { 6, 7 }));

    [TestMethod]
    public void InsertRangeNotifiesTheSameProperties() =>
        AssertModesNotifyTheSameProperties(collection => collection.InsertRange(1, new[] { 6, 7 }));

    [TestMethod]
    public void MoveRangeNotifiesTheSameProperties() =>
        AssertModesNotifyTheSameProperties(collection => collection.MoveRange(0, 3, 2));

    [TestMethod]
    public void RemoveRangeNotifiesTheSameProperties() =>
        AssertModesNotifyTheSameProperties(collection => collection.RemoveRange(1, 2));

    [TestMethod]
    public void ReplaceAllNotifiesTheSameProperties() =>
        AssertModesNotifyTheSameProperties(collection => collection.ReplaceAll(new[] { 8, 9 }));

    [TestMethod]
    public void ReplaceRangeNotifiesTheSameProperties() =>
        AssertModesNotifyTheSameProperties(collection => collection.ReplaceRange(1, 2, new[] { 8, 9, 10 }));
}
