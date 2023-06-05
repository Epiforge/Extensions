namespace Epiforge.Extensions.Collections.Tests;

[TestClass]
public class DictionaryExtensions
{
    [TestMethod]
    public void GetOrAdd()
    {
        var dictionary = new Dictionary<string, string>();
        var value = dictionary.GetOrAdd("key", key => "value");
        Assert.AreEqual("value", value);
        Assert.AreEqual("value", dictionary["key"]);
    }

    [TestMethod]
    public void GetOrAddExisting()
    {
        var dictionary = new Dictionary<string, string> { { "key", "value" } };
        var value = dictionary.GetOrAdd("key", key => "other value");
        Assert.AreEqual("value", value);
        Assert.AreEqual("value", dictionary["key"]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void GetOrAddWithNullDictionary() =>
        ((IDictionary<string, string>)null!).GetOrAdd("key", key => "value");

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void GetOrAddWithNullValueFactory() =>
        new Dictionary<string, string>().GetOrAdd("key", null!);

    [TestMethod]
    public void NonGenericGetOrAdd()
    {
        var dictionary = new Dictionary<string, string>();
        var value = ((IDictionary)dictionary).GetOrAdd("key", key => "value");
        Assert.AreEqual("value", value);
        Assert.AreEqual("value", dictionary["key"]);
    }

    [TestMethod]
    public void NonGenericGetOrAddExisting()
    {
        var dictionary = new Dictionary<string, string> { { "key", "value" } };
        var value = ((IDictionary)dictionary).GetOrAdd("key", key => "other value");
        Assert.AreEqual("value", value);
        Assert.AreEqual("value", dictionary["key"]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NonGenericGetOrAddWithNullDictionary() =>
        ((IDictionary)null!).GetOrAdd("key", key => "value");

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NonGenericGetOrAddWithNullValueFactory() =>
        ((IDictionary)new Dictionary<string, string>()).GetOrAdd("key", null!);

    [TestMethod]
    public void NonGenericTryRemove()
    {
        var dictionary = new Dictionary<string, string> { { "key", "value" } };
        var removed = ((IDictionary)dictionary).TryRemove("key", out var value);
        Assert.IsTrue(removed);
        Assert.AreEqual("value", value);
    }

    [TestMethod]
    public void NonGenericTryRemoveNotFound()
    {
        var dictionary = new Dictionary<string, string> { { "key", "value" } };
        var removed = ((IDictionary)dictionary).TryRemove("other key", out var value);
        Assert.IsFalse(removed);
        Assert.IsNull(value);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NonGenericTryRemoveWithNullDictionary() =>
        ((IDictionary)null!).TryRemove("key", out _);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NonGenericTryRemoveWithNullKey() =>
        ((IDictionary)new Dictionary<string, string>()).TryRemove(null!, out _);

    [TestMethod]
    public void TryRemove()
    {
        var dictionary = new Dictionary<string, string> { { "key", "value" } };
        var removed = dictionary.TryRemove("key", out var value);
        Assert.IsTrue(removed);
        Assert.AreEqual("value", value);
    }

    [TestMethod]
    public void TryRemoveNotFound()
    {
        var dictionary = new Dictionary<string, string> { { "key", "value" } };
        var removed = dictionary.TryRemove("other key", out var value);
        Assert.IsFalse(removed);
        Assert.IsNull(value);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void TryRemoveWithNullDictionary() =>
        ((IDictionary<string, string>)null!).TryRemove("key", out _);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void TryRemoveWithNullKey() =>
        new Dictionary<string, string>().TryRemove(null!, out _);
}
