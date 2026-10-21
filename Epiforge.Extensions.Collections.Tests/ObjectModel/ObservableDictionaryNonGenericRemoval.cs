namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

[TestClass]
public class ObservableDictionaryNonGenericRemoval
{
    static void AssertRemovingThroughTheNonGenericInterfaceAnnouncesOnce<TDictionary>(TDictionary dictionary)
        where TDictionary : IDictionary, INotifyDictionaryChanged<int, int>, System.ComponentModel.INotifyPropertyChanging, System.ComponentModel.INotifyPropertyChanged
    {
        var removals = new List<string>();
        var changing = new List<string>();
        var changed = new List<string>();
        dictionary.DictionaryChanged += (sender, e) => removals.Add($"{e.Action}:{string.Join("+", e.OldItems.Select(pair => $"{pair.Key}={pair.Value}"))}");
        dictionary.PropertyChanging += (sender, e) => changing.Add(e.PropertyName!);
        dictionary.PropertyChanged += (sender, e) => changed.Add(e.PropertyName!);
        dictionary.Remove(1);
        Assert.AreEqual(1, dictionary.Count);
        Assert.AreEqual("Remove:1=10", string.Join(", ", removals), "removing one key through the non-generic interface was announced other than once");
        Assert.AreEqual("Count", string.Join(", ", changing), "the count was announced as changing other than once");
        Assert.AreEqual("Count, Item[]", string.Join(", ", changed), "the count and the indexer were announced as changed other than once each");
    }

    [TestMethod]
    public void TheDictionaryAnnouncesANonGenericRemovalOnce() =>
        AssertRemovingThroughTheNonGenericInterfaceAnnouncesOnce(new ObservableDictionary<int, int> { { 1, 10 }, { 2, 20 } });

    [TestMethod]
    public void TheSortedDictionaryAnnouncesANonGenericRemovalOnce() =>
        AssertRemovingThroughTheNonGenericInterfaceAnnouncesOnce(new ObservableSortedDictionary<int, int> { { 1, 10 }, { 2, 20 } });
}
