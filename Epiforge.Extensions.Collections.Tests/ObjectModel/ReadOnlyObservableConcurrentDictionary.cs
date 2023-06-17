namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

[TestClass]
public class ReadOnlyObservableConcurrentDictionary
{
    [TestMethod]
    public void Comparer() =>
        Assert.AreEqual(StringComparer.OrdinalIgnoreCase, new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)).Comparer);

    [TestMethod]
    public void ContainsKey() =>
        Assert.IsFalse(new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>()).ContainsKey("key"));

    [TestMethod]
    public void Count() =>
        Assert.AreEqual(0, new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>()).Count);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void GenericICollectionAdd() =>
        ((ICollection<KeyValuePair<string, string>>)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).Add(new KeyValuePair<string, string>("key", "value"));

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void GenericICollectionClear() =>
        ((ICollection<KeyValuePair<string, string>>)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).Clear();

    [TestMethod]
    public void GenericICollectionContains() =>
        Assert.IsTrue(((ICollection<KeyValuePair<string, string>>)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } }))).Contains(new KeyValuePair<string, string>("key", "value")));

    [TestMethod]
    public void GenericICollectionCopyTo()
    {
        var array = new KeyValuePair<string, string>[1];
        ((ICollection<KeyValuePair<string, string>>)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } }))).CopyTo(array, 0);
        var kvp = array[0];
        Assert.AreEqual("key", kvp.Key);
        Assert.AreEqual("value", kvp.Value);
    }

    [TestMethod]
    public void GenericICollectionIsReadOnly() =>
        Assert.IsTrue(((ICollection<KeyValuePair<string, string>>)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).IsReadOnly);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void GenericICollectionRemove() =>
        ((ICollection<KeyValuePair<string, string>>)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).Remove(new KeyValuePair<string, string>("key", "value"));

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void GenericIDictionaryAdd() =>
        ((IDictionary<string, string>)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).Add("key", "value");

    [TestMethod]
    public void GenericIDictionaryIndexerGetter() =>
        Assert.AreEqual("value", ((IDictionary<string, string>)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } })))["key"]);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void GenericIDictionaryIndexerSetter() =>
        ((IDictionary<string, string>)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } })))["key"] = "value";

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void GenericIDictionaryRemove() =>
        ((IDictionary<string, string>)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).Remove("key");

    [TestMethod]
    public void GetEnumerator() =>
        Assert.IsNotNull(new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>()).GetEnumerator());

    [TestMethod]
    public void ICollectionCopyTo()
    {
        var array = new KeyValuePair<string, string>[1];
        ((ICollection)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } }))).CopyTo(array, 0);
        var kvp = (KeyValuePair<string, string>)array[0];
        Assert.AreEqual("key", kvp.Key);
        Assert.AreEqual("value", kvp.Value);
    }

    [TestMethod]
    public void ICollectionIsSynchronized() =>
        Assert.IsFalse(((ICollection)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).IsSynchronized);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void ICollectionSyncRoot() =>
        _ = ((ICollection)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).SyncRoot;

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IDictionaryAdd() =>
        ((IDictionary)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).Add("key", "value");

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IDictionaryClear() =>
        ((IDictionary)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).Clear();

    [TestMethod]
    public void IDictionaryContains() =>
        Assert.IsTrue(((IDictionary)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } }))).Contains("key"));

    [TestMethod]
    public void IDictionaryGetEnumerator() =>
        Assert.IsNotNull(((IDictionary)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).GetEnumerator());

    [TestMethod]
    public void IDictionaryIndexerGetter() =>
        Assert.AreEqual("value", ((IDictionary)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } })))["key"]);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IDictionaryIndexerSetter() =>
        ((IDictionary)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } })))["key"] = "value";

    [TestMethod]
    public void IDictionaryIsFixedSize() =>
        Assert.IsFalse(((IDictionary)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).IsFixedSize);

    [TestMethod]
    public void IDictionaryIsReadOnly() =>
        Assert.IsTrue(((IDictionary)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).IsReadOnly);

    [TestMethod]
    public void IDictionaryKeys() =>
        Assert.AreEqual(0, ((IDictionary)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).Keys.Count);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IDictionaryRemove() =>
        ((IDictionary)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).Remove(new KeyValuePair<string, string>("key", "value"));

    [TestMethod]
    public void IDictionaryValues() =>
        Assert.AreEqual(0, ((IDictionary)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).Values.Count);

    [TestMethod]
    public void IEnumeratorGetEnumerator() =>
        Assert.IsNotNull(((IEnumerable)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).GetEnumerator());

    [TestMethod]
    public void IndexerGetter() =>
        Assert.AreEqual("value", new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } }))["key"]);

    [TestMethod]
    public void INotifyDictionaryChangedOfObjectsDictionaryChanged()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>();
        var readOnlyDictionary = new ReadOnlyObservableConcurrentDictionary<string, string>(dictionary);
        var eventRaised = false;
        void dictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) =>
            eventRaised = true;
        ((INotifyDictionaryChanged)readOnlyDictionary).DictionaryChanged += dictionaryChanged;
        dictionary.TryAdd("key", "value");
        ((INotifyDictionaryChanged)readOnlyDictionary).DictionaryChanged -= dictionaryChanged;
        Assert.IsTrue(eventRaised);
    }

    [TestMethod]
    public void IReadOnlyDictionaryKeys() =>
        Assert.AreEqual(0, ((IReadOnlyDictionary<string, string>)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).Keys.Count());

    [TestMethod]
    public void IReadOnlyDictionaryValues() =>
        Assert.AreEqual(0, ((IReadOnlyDictionary<string, string>)new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>())).Values.Count());

    [TestMethod]
    public void IsEmpty() =>
        Assert.IsTrue(new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>()).IsEmpty);

    [TestMethod]
    public void Keys() =>
        Assert.AreEqual(0, new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>()).Keys.Count);

    [TestMethod]
    public void ToArray() =>
        Assert.AreEqual(0, new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>()).ToArray().Length);

    [TestMethod]
    public void TryGetValue()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>();
        var readOnlyDictionary = new ReadOnlyObservableConcurrentDictionary<string, string>(dictionary);
        Assert.IsFalse(readOnlyDictionary.TryGetValue("key", out _));
        dictionary.TryAdd("key", "value");
        Assert.IsTrue(readOnlyDictionary.TryGetValue("key", out var value));
        Assert.AreEqual("value", value);
    }

    [TestMethod]
    public void Values() =>
        Assert.AreEqual(0, new ReadOnlyObservableConcurrentDictionary<string, string>(new ObservableConcurrentDictionary<string, string>()).Values.Count);
}
