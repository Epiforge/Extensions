namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

using System.ComponentModel;

[TestClass]
public class ObservableDictionaryIndexerNotification
{
    static ObservableConcurrentDictionary<int, int> Concurrent()
    {
        var dictionary = new ObservableConcurrentDictionary<int, int>();
        dictionary.TryAdd(1, 1);
        return dictionary;
    }

    static string PropertiesChangedBy<TDictionary>(TDictionary dictionary, Action<TDictionary> operation)
        where TDictionary : INotifyPropertyChanged
    {
        var properties = new List<string>();
        dictionary.PropertyChanged += (sender, e) => properties.Add(e.PropertyName!);
        operation(dictionary);
        return string.Join(", ", properties.Distinct().OrderBy(name => name, StringComparer.Ordinal));
    }

    [TestMethod]
    public void TheConcurrentDictionaryAnnouncesItsIndexer()
    {
        Assert.AreEqual("Count, Item[]", PropertiesChangedBy(Concurrent(), dictionary => dictionary.TryAdd(2, 2)));
        Assert.AreEqual("Count, Item[]", PropertiesChangedBy(Concurrent(), dictionary => dictionary.TryRemove(1, out _)));
        Assert.AreEqual("Item[]", PropertiesChangedBy(Concurrent(), dictionary => dictionary[1] = 2), "replacing a value announced something other than the indexer alone");
    }

    [TestMethod]
    public void TheDictionaryAnnouncesItsIndexer()
    {
        Assert.AreEqual("Count, Item[]", PropertiesChangedBy(new ObservableDictionary<int, int> { { 1, 1 } }, dictionary => dictionary.Add(2, 2)));
        Assert.AreEqual("Count, Item[]", PropertiesChangedBy(new ObservableDictionary<int, int> { { 1, 1 } }, dictionary => dictionary.Remove(1)));
        Assert.AreEqual("Item[]", PropertiesChangedBy(new ObservableDictionary<int, int> { { 1, 1 } }, dictionary => dictionary[1] = 2), "replacing a value announced something other than the indexer alone");
    }

    [TestMethod]
    public void TheSortedDictionaryAnnouncesItsIndexer()
    {
        Assert.AreEqual("Count, Item[]", PropertiesChangedBy(new ObservableSortedDictionary<int, int> { { 1, 1 } }, dictionary => dictionary.Add(2, 2)));
        Assert.AreEqual("Count, Item[]", PropertiesChangedBy(new ObservableSortedDictionary<int, int> { { 1, 1 } }, dictionary => dictionary.Remove(1)));
        Assert.AreEqual("Item[]", PropertiesChangedBy(new ObservableSortedDictionary<int, int> { { 1, 1 } }, dictionary => dictionary[1] = 2), "replacing a value announced something other than the indexer alone");
    }
}
