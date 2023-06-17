namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

[TestClass]
public class ReadOnlyObservableRangeDictionary
{
    [TestMethod]
    public void CollectionChanged()
    {
        var dictionary = new ObservableDictionary<string, string>();
        var readOnlyDictionary = new ReadOnlyObservableRangeDictionary<string, string>(dictionary);
        var collectionChanged = false;
        readOnlyDictionary.CollectionChanged += (sender, e) => collectionChanged = true;
        dictionary.Add("key", "value");
        Assert.IsTrue(collectionChanged);
    }

    [TestMethod]
    public void NonGenericDictionaryChanged()
    {
        var dictionary = new ObservableDictionary<string, string>();
        var readOnlyDictionary = new ReadOnlyObservableRangeDictionary<string, string>(dictionary);
        var dictionaryChanged = false;
        void handler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) =>
            dictionaryChanged = true;
        ((INotifyDictionaryChanged)readOnlyDictionary).DictionaryChanged += handler;
        dictionary.Add("key", "value");
        Assert.IsTrue(dictionaryChanged);
        ((INotifyDictionaryChanged)readOnlyDictionary).DictionaryChanged -= handler;
    }
}
