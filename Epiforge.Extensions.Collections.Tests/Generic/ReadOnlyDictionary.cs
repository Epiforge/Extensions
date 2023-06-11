namespace Epiforge.Extensions.Collections.Tests.Generic;

[TestClass]
public class ReadOnlyDictionary
{
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ConstructionWithNullDictionary() =>
        new ReadOnlyDictionary<string, string>(null!);

    [TestMethod]
    public void ContainsKey()
    {
        var readOnlyDictionary = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.IsTrue(readOnlyDictionary.ContainsKey("key"));
        Assert.IsFalse(readOnlyDictionary.ContainsKey("other"));
    }

    [TestMethod]
    public void GetEnumerator()
    {
        var readOnlyDictionary = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual(1, readOnlyDictionary.Count);
        var first = readOnlyDictionary.First();
        Assert.AreEqual("key", first.Key);
        Assert.AreEqual("value", first.Value);
    }

    [TestMethod]
    public void IndexerGetter()
    {
        var readOnlyDictionary = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual("value", readOnlyDictionary["key"]);
        Assert.ThrowsException<KeyNotFoundException>(() => readOnlyDictionary["other"]);
    }

    [TestMethod]
    public void Keys()
    {
        var readOnlyDictionary = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual(1, readOnlyDictionary.Keys.Count());
        Assert.IsTrue(readOnlyDictionary.Keys.Contains("key"));
    }

    [TestMethod]
    public void NonGenericGetEnumerator()
    {
        var readOnlyDictionary = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual(1, readOnlyDictionary.Count);
        var enumerator = ((IEnumerable)readOnlyDictionary).GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());
        var first = (KeyValuePair<string, string>)enumerator.Current;
        Assert.AreEqual("key", first.Key);
        Assert.AreEqual("value", first.Value);
    }

    [TestMethod]
    public void TryGetValue()
    {
        var readOnlyDictionary = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.IsTrue(readOnlyDictionary.TryGetValue("key", out var value));
        Assert.AreEqual("value", value);
        Assert.IsFalse(readOnlyDictionary.TryGetValue("other", out value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void Values()
    {
        var readOnlyDictionary = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual(1, readOnlyDictionary.Values.Count());
        Assert.IsTrue(readOnlyDictionary.Values.Contains("value"));
    }
}
