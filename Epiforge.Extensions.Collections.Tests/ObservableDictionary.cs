namespace Epiforge.Extensions.Collections.Tests;

[TestClass]
public class ObservableDictionary
{
    #region Derivations

    class DerivationWithGetDictionaryEnumerator<TKey, TValue> :
        ObservableDictionary<TKey, TValue>
        where TKey : notnull
    {
        public DerivationWithGetDictionaryEnumerator()
        {
        }

        public new IDictionaryEnumerator GetDictionaryEnumerator() =>
            base.GetDictionaryEnumerator();
    }

    class DerivationWithNullOnChangedArgs<TKey, TValue> :
        ObservableDictionary<TKey, TValue>
        where TKey : notnull
    {
        public DerivationWithNullOnChangedArgs()
        {
        }

        protected override void OnChanged(NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
            base.OnChanged(null!);
    }

    class DerivationWithUnsupportedNotifyDictionaryChangedAction<TKey, TValue> :
        ObservableDictionary<TKey, TValue>
        where TKey : notnull
    {
        public DerivationWithUnsupportedNotifyDictionaryChangedAction()
        {
        }

        protected override void OnChanged(NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
            base.OnChanged(new NotifyDictionaryChangedEventArgs<TKey, TValue>((NotifyDictionaryChangedAction)200, e.NewItems, e.OldItems));
    }

    #endregion Derivations

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void AddNullKey() =>
        new ObservableDictionary<string, string>().Add(null!, "value");

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void AddRangeNullKey() =>
        new ObservableDictionary<string, string>().AddRange(new KeyValuePair<string, string>[] { new KeyValuePair<string, string>(null!, "value") });

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void AddRangeNullList() =>
        new ObservableDictionary<string, string>().AddRange(null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void CollectionAddNullKey() =>
        ((ICollection<KeyValuePair<string, string>>)new ObservableDictionary<string, string>()).Add(new KeyValuePair<string, string>(null!, "value"));

    [TestMethod]
    public void CollectionContains() =>
        Assert.IsTrue(((ICollection<KeyValuePair<string, string>>)new ObservableDictionary<string, string>() { { "key", "value" } }).Contains(new KeyValuePair<string, string>("key", "value")));

    [TestMethod]
    public void CollectionCopyTo()
    {
        var array = new KeyValuePair<string, string>[1];
        ((ICollection<KeyValuePair<string, string>>)new ObservableDictionary<string, string>() { { "key", "value" } }).CopyTo(array, 0);
        Assert.AreEqual("key", array[0].Key);
        Assert.AreEqual("value", array[0].Value);
    }

    [TestMethod]
    public void CollectionIsReadOnly() =>
        Assert.IsFalse(((ICollection<KeyValuePair<string, string>>)new ObservableDictionary<string, string>()).IsReadOnly);

    [TestMethod]
    public void CollectionIsSynchronized() =>
        Assert.IsFalse(((ICollection)new ObservableDictionary<string, string>()).IsSynchronized);

    [TestMethod]
    public void CollectionRemove() =>
        Assert.IsTrue(((ICollection<KeyValuePair<string, string>>)new ObservableDictionary<string, string> { { "key", "value" } }).Remove(new KeyValuePair<string, string>("key", "value")));

    [TestMethod]
    public void CollectionRemoveNotFound() =>
        Assert.IsFalse(((ICollection<KeyValuePair<string, string>>)new ObservableDictionary<string, string> { { "key", "value" } }).Remove(new KeyValuePair<string, string>("other key", "value")));

    [TestMethod]
    public void Construction() =>
        Assert.AreEqual(0, new ObservableDictionary<string, string>().Count);

    [TestMethod]
    public void ConstructionWithCapacity() =>
        Assert.AreEqual(0, new ObservableDictionary<string, string>(10).Count);

    [TestMethod]
    public void ConstructionWithCapacityAndComparer()
    {
        var observableDictionary = new ObservableDictionary<string, string>(10, StringComparer.OrdinalIgnoreCase);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase, observableDictionary.Comparer);
        Assert.AreEqual(0, observableDictionary.Count);
    }

    [TestMethod]
    public void ConstructionWithComparer()
    {
        var observableDictionary = new ObservableDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase, observableDictionary.Comparer);
        Assert.AreEqual(0, observableDictionary.Count);
    }

    [TestMethod]
    public void ConstructionWithDictionary()
    {
        var dictionary = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" },
            { "key3", "value3" },
        };
        var observableDictionary = new ObservableDictionary<string, string>(dictionary);
        Assert.AreEqual(3, observableDictionary.Count);
        Assert.AreEqual("value1", observableDictionary["key1"]);
        Assert.AreEqual("value2", observableDictionary["key2"]);
        Assert.AreEqual("value3", observableDictionary["key3"]);
    }

    [TestMethod]
    public void ConstructionWithDictionaryAndComparer()
    {
        var dictionary = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" },
            { "key3", "value3" },
        };
        var observableDictionary = new ObservableDictionary<string, string>(dictionary, StringComparer.OrdinalIgnoreCase);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase, observableDictionary.Comparer);
        Assert.AreEqual(3, observableDictionary.Count);
        Assert.AreEqual("value1", observableDictionary["key1"]);
        Assert.AreEqual("value2", observableDictionary["key2"]);
        Assert.AreEqual("value3", observableDictionary["key3"]);
    }

    [TestMethod]
    public void ContainsKey() =>
        Assert.IsTrue(new ObservableDictionary<string, string> { { "key", "value" } }.ContainsKey("key"));

    [TestMethod]
    public void ContainsValue() =>
        Assert.IsTrue(new ObservableDictionary<string, string> { { "key", "value" } }.ContainsValue("value"));

    [TestMethod]
    public void DictionaryIsFixedSize() =>
        Assert.IsFalse(((IDictionary)new ObservableDictionary<string, string>()).IsFixedSize);

    [TestMethod]
    public void DictionaryIsReadOnly() =>
        Assert.IsFalse(((IDictionary)new ObservableDictionary<string, string>()).IsReadOnly);

    [TestMethod]
    public void DictionaryKeys() =>
        Assert.AreEqual(0, ((IDictionary)new ObservableDictionary<string, string>()).Keys.Count);

    [TestMethod]
    public void DictionaryValues() =>
        Assert.AreEqual(0, ((IDictionary)new ObservableDictionary<string, string>()).Values.Count);

    [TestMethod]
    public void EnsureCapacity() =>
        new ObservableDictionary<string, string> { { "key", "value" } }.EnsureCapacity(10);

    [TestMethod]
    public void GenericDictionaryKeys() =>
        Assert.AreEqual(0, ((IDictionary<string, string>)new ObservableDictionary<string, string>()).Keys.Count);

    [TestMethod]
    public void GenericDictionaryValues() =>
        Assert.AreEqual(0, ((IDictionary<string, string>)new ObservableDictionary<string, string>()).Values.Count);

    [TestMethod]
    public void GetDictionaryEnumerator() =>
        Assert.IsNotNull(new DerivationWithGetDictionaryEnumerator<string, string>().GetDictionaryEnumerator());

    [TestMethod]
    public void GetEnumerator() =>
        Assert.IsNotNull(new ObservableDictionary<string, string>().GetEnumerator());

    [TestMethod]
    public void GetKeyValuePairEnumerator() =>
        Assert.IsNotNull(((IEnumerable<KeyValuePair<string, string>>)new ObservableDictionary<string, string>()).GetEnumerator());

    [TestMethod]
    public void GetRange() =>
        Assert.AreEqual(1, new ObservableDictionary<string, string> { { "key", "value" } }.GetRange(new[] { "key" }).Count);

    [TestMethod]
    public void Keys() =>
        Assert.AreEqual(0, new ObservableDictionary<string, string>().Keys.Count);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NonGenericAddNullKey() =>
        ((IDictionary)new ObservableDictionary<string, string>()).Add(null!, "value");

    [TestMethod]
    public void NonGenericCollectionCopyTo()
    {
        var array = new KeyValuePair<string, string>[1];
        ((ICollection)new ObservableDictionary<string, string>() { { "key", "value" } }).CopyTo(array, 0);
        Assert.AreEqual("key", array[0].Key);
        Assert.AreEqual("value", array[0].Value);
    }

    [TestMethod]
    public void NonGenericContains() =>
        Assert.IsTrue(((IDictionary)new ObservableDictionary<string, string> { { "key", "value" } }).Contains("key"));

    [TestMethod]
    public void NonGenericGetEnumerator()
    {
        Assert.IsNotNull(((IEnumerable)new ObservableDictionary<string, string>()).GetEnumerator());
        Assert.IsNotNull(((IDictionary)new ObservableDictionary<string, string>()).GetEnumerator());
    }

    [TestMethod]
    public void NonGenericIndexerGetter() =>
        Assert.AreEqual("value", ((IDictionary)new ObservableDictionary<string, string> { { "key", "value" } })["key"]);

    [TestMethod]
    public void NonGenericIndexerSetter()
    {
        var observableDictionary = new ObservableDictionary<string, string>();
        ((IDictionary)observableDictionary)["key"] = "value";
        Assert.AreEqual("value", observableDictionary["key"]);
    }

    [TestMethod]
    public void NonGenericRemove() =>
        ((IDictionary)new ObservableDictionary<string, string> { { "key", "value" } }).Remove("key");

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullOnChangedArgs() =>
        new DerivationWithNullOnChangedArgs<string, string>().Add("key", "value");

    [TestMethod]
    public void ObservedAdd()
    {
        var observableDictionary = new ObservableDictionary<string, string>();
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("value", args!.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void nonGenericDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("value", args!.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += nonGenericDictionaryChangedHandler;
        observableDictionary.Add("key", "value");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= nonGenericDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedAddRange()
    {
        var observableDictionary = new ObservableDictionary<string, string>();
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableDictionary.CollectionChanged += (sender, args) =>
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
            Assert.AreEqual(2, args.NewItems!.Count);
            Assert.AreEqual("key1", ((KeyValuePair<string, string>)args.NewItems[0]!).Key);
            Assert.AreEqual("value1", ((KeyValuePair<string, string>)args.NewItems[0]!).Value);
            Assert.AreEqual("key2", ((KeyValuePair<string, string>)args.NewItems[1]!).Key);
            Assert.AreEqual("value2", ((KeyValuePair<string, string>)args.NewItems[1]!).Value);
        };
        observableDictionary.DictionaryChanged += (sender, args) =>
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args.Action);
            Assert.AreEqual(2, args.NewItems!.Count);
            Assert.AreEqual("key1", args.NewItems[0]!.Key);
            Assert.AreEqual("value1", args.NewItems[0]!.Value);
            Assert.AreEqual("key2", args.NewItems[1]!.Key);
            Assert.AreEqual("value2", args.NewItems[1]!.Value);
        };
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += (sender, args) =>
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args.Action);
            Assert.AreEqual(2, args.NewItems!.Count);
            Assert.AreEqual("key1", args.NewItems[0]!.Key);
            Assert.AreEqual("value1", args.NewItems[0]!.Value);
            Assert.AreEqual("key2", args.NewItems[1]!.Key);
            Assert.AreEqual("value2", args.NewItems[1]!.Value);
        };
        observableDictionary.AddRange(new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" },
        });
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
    }

    [TestMethod]
    public void ObservedClear()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableDictionary.CollectionChanged += (sender, args) =>
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, args.Action);
            Assert.AreEqual(1, args.OldItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args.OldItems![0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args.OldItems![0]!).Value);
            Assert.IsNull(args.NewItems);
        };
        observableDictionary.DictionaryChanged += (sender, args) =>
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args.Action);
            Assert.AreEqual(1, args.OldItems!.Count);
            Assert.AreEqual("key", args.OldItems![0]!.Key);
            Assert.AreEqual("value", args.OldItems![0]!.Value);
            Assert.AreEqual(0, args.NewItems!.Count);
        };
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += (sender, args) =>
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args.Action);
            Assert.AreEqual(1, args.OldItems!.Count);
            Assert.AreEqual("key", args.OldItems![0]!.Key);
            Assert.AreEqual("value", args.OldItems![0]!.Value);
            Assert.AreEqual(0, args.NewItems!.Count);
        };
        observableDictionary.Clear();
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
    }

    [TestMethod]
    public void ObservedCollectionAdd()
    {
        var observableDictionary = new ObservableDictionary<string, string>();
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableDictionary.CollectionChanged += (sender, args) =>
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        };
        observableDictionary.DictionaryChanged += (sender, args) =>
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("value", args!.NewItems[0]!.Value);
        };
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += (sender, args) =>
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("value", args!.NewItems[0]!.Value);
        };
        ((ICollection<KeyValuePair<string, string>>)observableDictionary).Add(new KeyValuePair<string, string>("key", "value"));
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
    }

    [TestMethod]
    public void ObservedNonGenericDictionaryAdd()
    {
        var observableDictionary = new ObservableDictionary<string, string>();
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableDictionary.CollectionChanged += (sender, args) =>
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        };
        observableDictionary.DictionaryChanged += (sender, args) =>
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("value", args!.NewItems[0]!.Value);
        };
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += (sender, args) =>
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("value", args!.NewItems[0]!.Value);
        };
        ((IDictionary)observableDictionary).Add("key", "value");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
    }

    [TestMethod]
    public void ObservedRemove()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableDictionary.CollectionChanged += (sender, args) =>
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args!.OldItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.OldItems[0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args!.OldItems[0]!).Value);
        };
        observableDictionary.DictionaryChanged += (sender, args) =>
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args!.OldItems!.Count);
            Assert.AreEqual("key", args!.OldItems[0]!.Key);
            Assert.AreEqual("value", args!.OldItems[0]!.Value);
        };
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += (sender, args) =>
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args!.OldItems!.Count);
            Assert.AreEqual("key", args!.OldItems[0]!.Key);
            Assert.AreEqual("value", args!.OldItems[0]!.Value);
        };
        observableDictionary.Remove("key");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
    }

    [TestMethod]
    public void ObservedReplace()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableDictionary.CollectionChanged += (sender, args) =>
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Replace, args!.Action);
            Assert.AreEqual(1, args!.OldItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.OldItems[0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args!.OldItems[0]!).Value);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("new value", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        };
        observableDictionary.DictionaryChanged += (sender, args) =>
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Replace, args!.Action);
            Assert.AreEqual(1, args!.OldItems!.Count);
            Assert.AreEqual("key", args!.OldItems[0]!.Key);
            Assert.AreEqual("value", args!.OldItems[0]!.Value);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("new value", args!.NewItems[0]!.Value);
        };
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += (sender, args) =>
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Replace, args.Action);
            Assert.AreEqual(1, args!.OldItems.Count);
            Assert.AreEqual("key", args!.OldItems[0]!.Key);
            Assert.AreEqual("value", args!.OldItems[0]!.Value);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("new value", args!.NewItems[0]!.Value);
        };
        observableDictionary["key"] = "new value";
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
    }

    [TestMethod]
    public void ObservedReset()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableDictionary.CollectionChanged += (sender, args) =>
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, args.Action);
        };
        observableDictionary.DictionaryChanged += (sender, args) =>
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Reset, args.Action);
        };
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += (sender, args) =>
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Reset, args.Action);
        };
        observableDictionary.Reset(new Dictionary<string, string> { { "other key", "other value" } });
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
    }

    [TestMethod]
    public void RangeDictionaryKeys() =>
        Assert.AreEqual(0, ((IRangeDictionary<string, string>)new ObservableDictionary<string, string>()).Keys.Count());

    [TestMethod]
    public void RangeDictionaryValues() =>
        Assert.AreEqual(0, ((IRangeDictionary<string, string>)new ObservableDictionary<string, string>()).Values.Count());

    [TestMethod]
    public void ReadOnlyDictionaryKeys() =>
        Assert.AreEqual(0, ((IReadOnlyDictionary<string, string>)new ObservableDictionary<string, string>()).Keys.Count());

    [TestMethod]
    public void ReadOnlyDictionaryValues() =>
        Assert.AreEqual(0, ((IReadOnlyDictionary<string, string>)new ObservableDictionary<string, string>()).Values.Count());

    [TestMethod]
    public void RemoveAll()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        observableDictionary.RemoveAll((k, v) => k.StartsWith("k"));
        Assert.AreEqual(0, observableDictionary.Count);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void RemoveAllNullPredicate() =>
        new ObservableDictionary<string, string> { { "key", "value" } }.RemoveAll(null!);

    [TestMethod]
    public void RemoveAndGetValue()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        Assert.IsTrue(observableDictionary.Remove("key", out var value));
        Assert.AreEqual("value", value);
        Assert.IsFalse(observableDictionary.ContainsKey("key"));
    }

    [TestMethod]
    public void RemoveAndGetValueNotFound()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        Assert.IsFalse(observableDictionary.Remove("other key", out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void RemoveNotFound() =>
        Assert.IsFalse(new ObservableDictionary<string, string> { { "key", "value" } }.Remove("other key"));

    [TestMethod]
    public void RemoveRange()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        observableDictionary.RemoveRange(new[] { "key" });
        Assert.AreEqual(0, observableDictionary.Count);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void RemoveRangeNullKeys() =>
        new ObservableDictionary<string, string> { { "key", "value" } }.RemoveRange(null!);

    [TestMethod]
    public void ReplaceRange()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        observableDictionary.ReplaceRange(new Dictionary<string, string> { { "key", "other value" } });
        Assert.AreEqual(1, observableDictionary.Count);
        Assert.AreEqual("other value", observableDictionary["key"]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ReplaceRangeKeyNotFound() =>
        new ObservableDictionary<string, string> { { "key", "value" } }.ReplaceRange(new Dictionary<string, string> { { "other key", "other value" } });

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ReplaceRangeNullDictionary() =>
        new ObservableDictionary<string, string> { { "key", "value" } }.ReplaceRange(null!);

    [TestMethod]
    public void ReplaceRangeWithKeys()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        observableDictionary.ReplaceRange(new[] { "key" }, new Dictionary<string, string> { { "key", "other value" }, { "yet another key", "yet another value" } });
        Assert.AreEqual(2, observableDictionary.Count);
        Assert.AreEqual("other value", observableDictionary["key"]);
        Assert.AreEqual("yet another value", observableDictionary["yet another key"]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ReplaceRangeWithKeysKeyNotFound() =>
        new ObservableDictionary<string, string> { { "key", "value" } }.ReplaceRange(new[] { "other key" }, new Dictionary<string, string> { { "key", "other value" } });

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ReplaceRangeWithKeysNullReplaceKeys() =>
        new ObservableDictionary<string, string> { { "key", "value" } }.ReplaceRange(null!, new Dictionary<string, string> { { "key", "other value" } });

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ReplaceRangeWithKeysNullReplaceValues() =>
        new ObservableDictionary<string, string> { { "key", "value" } }.ReplaceRange(new[] { "key" }, null!);

    [TestMethod]
    public void Reset() =>
        new ObservableDictionary<string, string> { { "key", "value" } }.Reset();

    [TestMethod]
    public void ResetWithDictionary()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        observableDictionary.Reset(new Dictionary<string, string> { { "other key", "other value" }, { "yet another key", "yet another value" } });
        Assert.AreEqual(2, observableDictionary.Count);
        Assert.AreEqual("other value", observableDictionary["other key"]);
        Assert.AreEqual("yet another value", observableDictionary["yet another key"]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ResetWithDictionaryNullDictionary() =>
        new ObservableDictionary<string, string> { { "key", "value" } }.Reset(null!);

    [TestMethod]
    public void SyncRoot() =>
        Assert.IsNotNull(((ICollection)new ObservableDictionary<string, string>()).SyncRoot);

    [TestMethod]
    public void TrimExcess()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        observableDictionary.TrimExcess();
        Assert.AreEqual(1, observableDictionary.Count);
    }

    [TestMethod]
    public void TrimExcessWithCapacity()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        observableDictionary.TrimExcess(1);
        Assert.AreEqual(1, observableDictionary.Count);
    }

    [TestMethod]
    public void TryAdd()
    {
        var observableDictionary = new ObservableDictionary<string, string>();
        Assert.IsTrue(observableDictionary.TryAdd("key", "value"));
        Assert.AreEqual(1, observableDictionary.Count);
        Assert.AreEqual("value", observableDictionary["key"]);
        Assert.IsFalse(observableDictionary.TryAdd("key", "other value"));
        Assert.AreEqual(1, observableDictionary.Count);
        Assert.AreEqual("value", observableDictionary["key"]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void TryAddNullKey() =>
        new ObservableDictionary<string, string>().TryAdd(null!, "value");

    [TestMethod]
    public void TryGetValue()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        Assert.IsTrue(observableDictionary.TryGetValue("key", out var value));
        Assert.AreEqual("value", value);
        Assert.IsFalse(observableDictionary.TryGetValue("other key", out value));
        Assert.IsNull(value);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void UnsupportedNotifyDictionaryChangedAction() =>
        new DerivationWithUnsupportedNotifyDictionaryChangedAction<string, string>().Add("key", "value");

    [TestMethod]
    public void Values()
    {
        var observableDictionary = new ObservableDictionary<string, string> { { "key", "value" } };
        var values = observableDictionary.Values;
        Assert.AreEqual(1, values.Count);
        Assert.AreEqual("value", values.First());
    }
}
