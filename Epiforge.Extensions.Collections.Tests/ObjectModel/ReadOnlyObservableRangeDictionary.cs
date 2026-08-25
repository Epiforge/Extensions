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
    public void DisposeUnsubscribesFromCollectionChanged()
    {
        var dictionary = new ObservableDictionary<string, string>();
        var readOnlyDictionary = new ReadOnlyObservableRangeDictionary<string, string>(dictionary);
        var collectionChanges = 0;
        readOnlyDictionary.CollectionChanged += (sender, e) => ++collectionChanges;
        dictionary.Add("key", "value");
        Assert.AreEqual(1, collectionChanges);
        readOnlyDictionary.Dispose();
        dictionary.Add("otherKey", "otherValue");
        Assert.AreEqual(1, collectionChanges);
    }

    [TestMethod]
    public void DisposeUnsubscribesFromDictionaryChanged()
    {
        var dictionary = new ObservableDictionary<string, string>();
        var readOnlyDictionary = new ReadOnlyObservableRangeDictionary<string, string>(dictionary);
        var dictionaryChanges = 0;
        readOnlyDictionary.DictionaryChanged += (sender, e) => ++dictionaryChanges;
        dictionary.Add("key", "value");
        Assert.AreEqual(1, dictionaryChanges);
        readOnlyDictionary.Dispose();
        dictionary.Add("otherKey", "otherValue");
        Assert.AreEqual(1, dictionaryChanges);
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
