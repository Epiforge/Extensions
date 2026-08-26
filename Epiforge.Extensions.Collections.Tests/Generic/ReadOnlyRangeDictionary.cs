namespace Epiforge.Extensions.Collections.Tests.Generic;

[TestClass]
public class ReadOnlyRangeDictionary
{
    [TestMethod]
    public void Contains() =>
        Assert.IsTrue(new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } }).Contains(new KeyValuePair<string, string>("key", "value")));

    [TestMethod]
    public void CopyTo()
    {
        var array = new KeyValuePair<string, string>[1];
        new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } }).CopyTo(array, 0);
        Assert.AreEqual(new KeyValuePair<string, string>("key", "value"), array[0]);
    }

    [TestMethod]
    public void GetRange()
    {
        var range = new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } }).GetRange(new string[] { "key" });
        Assert.AreEqual(1, range.Count);
        Assert.AreEqual(new KeyValuePair<string, string>("key", "value"), range[0]);
    }

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void ICollectionAdd() =>
        ((ICollection<KeyValuePair<string, string>>)new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } })).Add(new KeyValuePair<string, string>("other", "value"));

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void ICollectionClear() =>
        ((ICollection<KeyValuePair<string, string>>)new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } })).Clear();

    [TestMethod]
    public void ICollectionIsReadOnly() =>
        Assert.IsTrue(((ICollection<KeyValuePair<string, string>>)new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } })).IsReadOnly);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void ICollectionRemove() =>
        ((ICollection<KeyValuePair<string, string>>)new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } })).Remove(new KeyValuePair<string, string>("key", "value"));

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IDictionaryAdd() =>
        ((IDictionary<string, string>)new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } })).Add("other", "value");

    [TestMethod]
    public void IDictionaryKeys()
    {
        var keys = ((IDictionary<string, string>)new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } })).Keys;
        Assert.AreEqual(1, keys.Count());
        Assert.AreEqual("key", keys.ElementAt(0));
    }

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IDictionaryRemove() =>
        ((IDictionary<string, string>)new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } })).Remove("key");

    [TestMethod]
    public void IDictionaryValues()
    {
        var values = ((IDictionary<string, string>)new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } })).Values;
        Assert.AreEqual(1, values.Count());
        Assert.AreEqual("value", values.ElementAt(0));
    }

    [TestMethod]
    public void IDictionaryIndexerGetter() =>
        Assert.AreEqual("value", ((IDictionary<string, string>)new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } }))["key"]);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IDictionaryIndexerSetter() =>
        ((IDictionary<string, string>)new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } }))["key"] = "other";

    [TestMethod]
    public void IReadOnlyRangeDictionaryIndexerGetter() =>
        Assert.AreEqual("value", ((IReadOnlyRangeDictionary<string, string>)new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } }))["key"]);

    [TestMethod]
    public void IsNotARangeDictionary() =>
        Assert.IsFalse(new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } }) is IRangeDictionary<string, string>);

    [TestMethod]
    public void WrapsAReadOnlyRangeDictionary()
    {
        var wrapper = new ReadOnlyRangeDictionary<string, string>(new ReadOnlyRangeDictionary<string, string>(new ObservableDictionary<string, string> { { "key", "value" } }));
        Assert.AreEqual("value", wrapper["key"]);
        Assert.AreEqual(1, wrapper.GetRange(new string[] { "key" }).Count);
    }
}
