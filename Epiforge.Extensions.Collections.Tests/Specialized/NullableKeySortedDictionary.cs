namespace Epiforge.Extensions.Collections.Tests.Specialized;

[TestClass]
public class NullableKeySortedDictionary
{
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void AddDuplicateNullKey()
    {
#pragma warning disable IDE0028 // Simplify collection initialization
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
#pragma warning restore IDE0028 // Simplify collection initialization
        dictionary.Add(null, 2);
    }

    [TestMethod]
    public void Clear()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
        Assert.AreEqual(1, dictionary.Count);
        dictionary.Clear();
        Assert.AreEqual(0, dictionary.Count);
    }

    [TestMethod]
    public void CollectionAdd()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int>();
        Assert.AreEqual(0, dictionary.Count);
        ((ICollection<KeyValuePair<string?, int>>)dictionary).Add(new KeyValuePair<string?, int>(null, 1));
        Assert.AreEqual(1, dictionary.Count);
    }

    [TestMethod]
    public void CollectionContains()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
        Assert.IsTrue(((ICollection<KeyValuePair<string?, int>>)dictionary).Contains(new KeyValuePair<string?, int>(null, 1)));
    }

    [TestMethod]
    public void CollectionCopyTo()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
        var array = new KeyValuePair<string?, int>[1];
        ((ICollection<KeyValuePair<string?, int>>)dictionary).CopyTo(array, 0);
        Assert.AreEqual(1, array[0].Value);
    }

    [TestMethod]
    public void CollectionRemoveNonNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { "one", 1 } };
        Assert.IsTrue(((ICollection<KeyValuePair<string?, int>>)dictionary).Remove(new KeyValuePair<string?, int>("one", 1)));
    }

    [TestMethod]
    public void CollectionRemoveNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
        Assert.IsTrue(((ICollection<KeyValuePair<string?, int>>)dictionary).Remove(new KeyValuePair<string?, int>(null, 1)));
    }

    [TestMethod]
    public void CollectionRemoveNullKeyNotFound()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
        Assert.IsFalse(((ICollection<KeyValuePair<string?, int>>)dictionary).Remove(new KeyValuePair<string?, int>(null, 2)));
    }

    [TestMethod]
    public void ConstructionFromDictionary()
    {
        var dictionary = new NullableKeySortedDictionary<int, string>(new Dictionary<int, string>
        {
            { 1, "one" },
            { 2, "two" },
            { 3, "three" }
        });
        Assert.AreEqual(3, dictionary.Count);
    }

    [TestMethod]
    public void ConstructionFromDictionaryWithComparer()
    {
        var dictionary = new NullableKeySortedDictionary<string, int>(new Dictionary<string, int>
        {
            { "one", 1 },
            { "two", 2 },
            { "three", 3 }
        }, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(3, dictionary.Count);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase, dictionary.Comparer);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ConstructionFromNullDictionary() =>
        new NullableKeySortedDictionary<int, string>((IDictionary<int, string>)null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ConstructionFromNullDictionaryWithComparer() =>
        new NullableKeySortedDictionary<string, int>(null!, StringComparer.OrdinalIgnoreCase);

    [TestMethod]
    public void ContainsNonNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { "one", 1 } };
        Assert.IsTrue(dictionary.ContainsKey("one"));
    }

    [TestMethod]
    public void ContainsNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
        Assert.IsTrue(dictionary.ContainsKey(null));
    }

    [TestMethod]
    public void ContainsValueForNonNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { "one", 1 } };
        Assert.IsTrue(dictionary.ContainsValue(1));
    }

    [TestMethod]
    public void ContainsValueForNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
        Assert.IsTrue(dictionary.ContainsValue(1));
    }

    [TestMethod]
    public void GetEnumerator()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 }, { "two", 2 } };
        Assert.AreEqual(2, dictionary.Count);
        foreach (var pair in dictionary)
            if (pair.Key == null)
                Assert.AreEqual(1, pair.Value);
            else
            {
                Assert.AreEqual("two", pair.Key);
                Assert.AreEqual(2, pair.Value);
            }
    }

    [TestMethod]
    public void GetNonGenericEnumerator()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 }, { "two", 2 } };
        Assert.AreEqual(2, dictionary.Count);
        var enumerator = ((IEnumerable)dictionary).GetEnumerator();
        enumerator.MoveNext();
        var pair = (KeyValuePair<string?, int>)enumerator.Current;
        Assert.AreEqual(null, pair.Key);
        Assert.AreEqual(1, pair.Value);
        enumerator.MoveNext();
        pair = (KeyValuePair<string?, int>)enumerator.Current;
        Assert.AreEqual("two", pair.Key);
        Assert.AreEqual(2, pair.Value);
    }

    [TestMethod]
    public void IndexerGetterNonNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { "one", 1 } };
        Assert.AreEqual(1, dictionary["one"]);
    }

    [TestMethod]
    [ExpectedException(typeof(KeyNotFoundException))]
    public void IndexerGetterNonNullKeyNotFound() =>
        _ = new NullableKeySortedDictionary<string?, int>()["one"];

    [TestMethod]
    public void IndexerGetterNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
        Assert.AreEqual(1, dictionary[null]);
    }

    [TestMethod]
    [ExpectedException(typeof(KeyNotFoundException))]
    public void IndexerGetterNullKeyNotFound() =>
        _ = new NullableKeySortedDictionary<string?, int>()[null];

    [TestMethod]
    public void IndexerSetterNonNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { "one", 1 } };
        dictionary["one"] = 1;
    }

    [TestMethod]
    public void IndexerSetterNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
        dictionary[null] = 2;
    }

    [TestMethod]
    [ExpectedException(typeof(KeyNotFoundException))]
    public void IndexerSetterNullKeyNotFound()
    {
#pragma warning disable IDE0028 // Simplify collection initialization
        var dictionary = new NullableKeySortedDictionary<string?, int>();
#pragma warning restore IDE0028 // Simplify collection initialization
        dictionary[null] = 2;
    }

    [TestMethod]
    public void Keys()
    {
        var dictionary = new NullableKeySortedDictionary<int, string>(new Dictionary<int, string>
        {
            { 1, "one" },
            { 2, "two" },
            { 3, "three" }
        });
        var keys = new int[] { 1, 2, 3 };
        Assert.AreEqual(3, dictionary.Keys.Count);
        Assert.AreEqual(0, keys.Except(dictionary.Keys).Count());
        Assert.AreEqual(0, dictionary.Keys.Except(keys).Count());
    }

    [TestMethod]
    public void IsReadOnly() =>
        Assert.IsFalse(((ICollection<KeyValuePair<string, int>>)new NullableKeySortedDictionary<string, int>()).IsReadOnly);

    [TestMethod]
    public void NullKeysAffectCount()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int>() { { "one", 1 } };
        Assert.AreEqual(1, dictionary.Count);
        dictionary.Add(null, 0);
        Assert.AreEqual(2, dictionary.Count);
        dictionary.Remove("one");
        Assert.AreEqual(1, dictionary.Count);
        dictionary.Remove(null);
        Assert.AreEqual(0, dictionary.Count);
    }

    [TestMethod]
    public void ReadOnlyKeys()
    {
        var dictionary = new NullableKeySortedDictionary<int, string>(new Dictionary<int, string>
        {
            { 1, "one" },
            { 2, "two" },
            { 3, "three" }
        });
        var keys = new int[] { 1, 2, 3 };
        var readOnlyDictionary = (IReadOnlyDictionary<int, string>)dictionary;
        Assert.AreEqual(3, readOnlyDictionary.Keys.Count());
        Assert.AreEqual(0, keys.Except(readOnlyDictionary.Keys).Count());
        Assert.AreEqual(0, readOnlyDictionary.Keys.Except(keys).Count());
    }

    [TestMethod]
    public void ReadOnlyValues()
    {
        var dictionary = new NullableKeySortedDictionary<int, string>(new Dictionary<int, string>
        {
            { 1, "one" },
            { 2, "two" },
            { 3, "three" }
        });
        var values = new string[] { "one", "two", "three" };
        var readOnlyDictionary = (IReadOnlyDictionary<int, string>)dictionary;
        Assert.AreEqual(3, readOnlyDictionary.Values.Count());
        Assert.AreEqual(0, readOnlyDictionary.Values.Except(values).Count());
        Assert.AreEqual(0, values.Except(readOnlyDictionary.Values).Count());
    }

    [TestMethod]
    public void RemoveNonNullKeyAndGetValue()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { "one", 1 } };
        Assert.IsTrue(dictionary.Remove("one", out var value));
        Assert.AreEqual(1, value);
        Assert.AreEqual(0, dictionary.Count);
    }

    [TestMethod]
    public void RemoveNonNullKeyNotFoundAndGetValue()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int>();
        Assert.IsFalse(dictionary.Remove("one", out _));
        Assert.AreEqual(0, dictionary.Count);
    }

    [TestMethod]
    public void RemoveNullKeyAndGetValue()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
        Assert.IsTrue(dictionary.Remove(null, out var value));
        Assert.AreEqual(1, value);
        Assert.AreEqual(0, dictionary.Count);
    }

    [TestMethod]
    public void RemoveNullKeyNotFoundAndGetValue()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int>();
        Assert.IsFalse(dictionary.Remove(null, out _));
        Assert.AreEqual(0, dictionary.Count);
    }

    [TestMethod]
    public void RemoveNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
        Assert.IsTrue(dictionary.Remove(null));
        Assert.AreEqual(0, dictionary.Count);
    }

    [TestMethod]
    public void RemoveNullKeyNotFound()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int>();
        Assert.IsFalse(dictionary.Remove(null));
        Assert.AreEqual(0, dictionary.Count);
    }

    [TestMethod]
    public void TryAddNonNullDuplicateKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { "one", 1 } };
        Assert.IsFalse(dictionary.TryAdd("one", 2));
        Assert.AreEqual(1, dictionary.Count);
    }

    [TestMethod]
    public void TryAddNonNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int>();
        Assert.IsTrue(dictionary.TryAdd("one", 2));
        Assert.AreEqual(1, dictionary.Count);
    }

    [TestMethod]
    public void TryAddDuplicateNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
        Assert.IsFalse(dictionary.TryAdd(null, 2));
        Assert.AreEqual(1, dictionary.Count);
    }

    [TestMethod]
    public void TryAddNullKey()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int>();
        Assert.IsTrue(dictionary.TryAdd(null, 1));
        Assert.AreEqual(1, dictionary.Count);
    }

    [TestMethod]
    public void TryGetNonNullKeyValue()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { "one", 1 } };
        Assert.IsTrue(dictionary.TryGetValue("one", out var value));
        Assert.AreEqual(1, value);
    }

    [TestMethod]
    public void TryGetNullKeyNotFoundValue()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { "one", 1 } };
        Assert.IsFalse(dictionary.TryGetValue(null, out _));
    }

    [TestMethod]
    public void TryGetNullKeyValue()
    {
        var dictionary = new NullableKeySortedDictionary<string?, int> { { null, 1 } };
        Assert.IsTrue(dictionary.TryGetValue(null, out var value));
        Assert.AreEqual(1, value);
    }

    [TestMethod]
    public void Values()
    {
        var dictionary = new NullableKeySortedDictionary<int, string>(new Dictionary<int, string>
        {
            { 1, "one" },
            { 2, "two" },
            { 3, "three" }
        });
        var values = new string[] { "one", "two", "three" };
        Assert.AreEqual(3, dictionary.Values.Count);
        Assert.AreEqual(0, dictionary.Values.Except(values).Count());
        Assert.AreEqual(0, values.Except(dictionary.Values).Count());
    }
}
