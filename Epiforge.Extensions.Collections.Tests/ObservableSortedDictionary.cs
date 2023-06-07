namespace Epiforge.Extensions.Collections.Tests;

[TestClass]
public class ObservableSortedDictionary
{
    #region Derivations

    class DerivationWithGetDictionaryEnumerator<TKey, TValue> :
        ObservableSortedDictionary<TKey, TValue>
        where TKey : notnull
    {
        public DerivationWithGetDictionaryEnumerator()
        {
        }

        public new IDictionaryEnumerator GetDictionaryEnumerator() =>
            base.GetDictionaryEnumerator();
    }

    class DerivationWithNullOnChangedArgs<TKey, TValue> :
        ObservableSortedDictionary<TKey, TValue>
        where TKey : notnull
    {
        public DerivationWithNullOnChangedArgs()
        {
        }

        protected override void OnChanged(NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
            base.OnChanged(null!);
    }

    class DerivationWithUnsupportedNotifyDictionaryChangedAction<TKey, TValue> :
        ObservableSortedDictionary<TKey, TValue>
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
        new ObservableSortedDictionary<string, string>().Add(null!, "value");

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void AddRangeNullKey() =>
        new ObservableSortedDictionary<string, string>().AddRange(new KeyValuePair<string, string>[] { new KeyValuePair<string, string>(null!, "value") });

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void AddRangeNullList() =>
        new ObservableSortedDictionary<string, string>().AddRange(null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void CollectionAddNullKey() =>
        ((ICollection<KeyValuePair<string, string>>)new ObservableSortedDictionary<string, string>()).Add(new KeyValuePair<string, string>(null!, "value"));

    [TestMethod]
    public void CollectionContains() =>
        Assert.IsTrue(((ICollection<KeyValuePair<string, string>>)new ObservableSortedDictionary<string, string>() { { "key", "value" } }).Contains(new KeyValuePair<string, string>("key", "value")));

    [TestMethod]
    public void CollectionCopyTo()
    {
        var array = new KeyValuePair<string, string>[1];
        ((ICollection<KeyValuePair<string, string>>)new ObservableSortedDictionary<string, string>() { { "key", "value" } }).CopyTo(array, 0);
        Assert.AreEqual("key", array[0].Key);
        Assert.AreEqual("value", array[0].Value);
    }

    [TestMethod]
    public void CollectionIsReadOnly() =>
        Assert.IsFalse(((ICollection<KeyValuePair<string, string>>)new ObservableSortedDictionary<string, string>()).IsReadOnly);

    [TestMethod]
    public void CollectionIsSynchronized() =>
        Assert.IsFalse(((ICollection)new ObservableSortedDictionary<string, string>()).IsSynchronized);

    [TestMethod]
    public void CollectionRemove() =>
        Assert.IsTrue(((ICollection<KeyValuePair<string, string>>)new ObservableSortedDictionary<string, string> { { "key", "value" } }).Remove(new KeyValuePair<string, string>("key", "value")));

    [TestMethod]
    public void CollectionRemoveNotFound() =>
        Assert.IsFalse(((ICollection<KeyValuePair<string, string>>)new ObservableSortedDictionary<string, string> { { "key", "value" } }).Remove(new KeyValuePair<string, string>("other key", "value")));

    [TestMethod]
    public void Construction() =>
        Assert.AreEqual(0, new ObservableSortedDictionary<string, string>().Count);

    [TestMethod]
    public void ConstructionWithComparer()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase, observableSortedDictionary.Comparer);
        Assert.AreEqual(0, observableSortedDictionary.Count);
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
        var observableSortedDictionary = new ObservableSortedDictionary<string, string>(dictionary);
        Assert.AreEqual(3, observableSortedDictionary.Count);
        Assert.AreEqual("value1", observableSortedDictionary["key1"]);
        Assert.AreEqual("value2", observableSortedDictionary["key2"]);
        Assert.AreEqual("value3", observableSortedDictionary["key3"]);
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
        var observableSortedDictionary = new ObservableSortedDictionary<string, string>(dictionary, StringComparer.OrdinalIgnoreCase);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase, observableSortedDictionary.Comparer);
        Assert.AreEqual(3, observableSortedDictionary.Count);
        Assert.AreEqual("value1", observableSortedDictionary["key1"]);
        Assert.AreEqual("value2", observableSortedDictionary["key2"]);
        Assert.AreEqual("value3", observableSortedDictionary["key3"]);
    }

    [TestMethod]
    public void ContainsKey() =>
        Assert.IsTrue(new ObservableSortedDictionary<string, string> { { "key", "value" } }.ContainsKey("key"));

    [TestMethod]
    public void ContainsValue() =>
        Assert.IsTrue(new ObservableSortedDictionary<string, string> { { "key", "value" } }.ContainsValue("value"));

    [TestMethod]
    public void DictionaryIsFixedSize() =>
        Assert.IsFalse(((IDictionary)new ObservableSortedDictionary<string, string>()).IsFixedSize);

    [TestMethod]
    public void DictionaryIsReadOnly() =>
        Assert.IsFalse(((IDictionary)new ObservableSortedDictionary<string, string>()).IsReadOnly);

    [TestMethod]
    public void DictionaryKeys() =>
        Assert.AreEqual(0, ((IDictionary)new ObservableSortedDictionary<string, string>()).Keys.Count);

    [TestMethod]
    public void DictionaryValues() =>
        Assert.AreEqual(0, ((IDictionary)new ObservableSortedDictionary<string, string>()).Values.Count);

    [TestMethod]
    public void GenericDictionaryKeys() =>
        Assert.AreEqual(0, ((IDictionary<string, string>)new ObservableSortedDictionary<string, string>()).Keys.Count);

    [TestMethod]
    public void GenericDictionaryValues() =>
        Assert.AreEqual(0, ((IDictionary<string, string>)new ObservableSortedDictionary<string, string>()).Values.Count);

    [TestMethod]
    public void GetDictionaryEnumerator() =>
        Assert.IsNotNull(new DerivationWithGetDictionaryEnumerator<string, string>().GetDictionaryEnumerator());

    [TestMethod]
    public void GetEnumerator() =>
        Assert.IsNotNull(new ObservableSortedDictionary<string, string>().GetEnumerator());

    [TestMethod]
    public void GetKeyValuePairEnumerator() =>
        Assert.IsNotNull(((IEnumerable<KeyValuePair<string, string>>)new ObservableSortedDictionary<string, string>()).GetEnumerator());

    [TestMethod]
    public void GetRange() =>
        Assert.AreEqual(1, new ObservableSortedDictionary<string, string> { { "key", "value" } }.GetRange(new[] { "key" }).Count);

    [TestMethod]
    public void Keys() =>
        Assert.AreEqual(0, new ObservableSortedDictionary<string, string>().Keys.Count);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NonGenericAddNullKey() =>
        ((IDictionary)new ObservableSortedDictionary<string, string>()).Add(null!, "value");

    [TestMethod]
    public void NonGenericCollectionCopyTo()
    {
        var array = new KeyValuePair<string, string>[1];
        ((ICollection)new ObservableSortedDictionary<string, string>() { { "key", "value" } }).CopyTo(array, 0);
        Assert.AreEqual("key", array[0].Key);
        Assert.AreEqual("value", array[0].Value);
    }

    [TestMethod]
    public void NonGenericContains() =>
        Assert.IsTrue(((IDictionary)new ObservableSortedDictionary<string, string> { { "key", "value" } }).Contains("key"));

    [TestMethod]
    public void NonGenericGetEnumerator()
    {
        Assert.IsNotNull(((IEnumerable)new ObservableSortedDictionary<string, string>()).GetEnumerator());
        Assert.IsNotNull(((IDictionary)new ObservableSortedDictionary<string, string>()).GetEnumerator());
    }

    [TestMethod]
    public void NonGenericIndexerGetter() =>
        Assert.AreEqual("value", ((IDictionary)new ObservableSortedDictionary<string, string> { { "key", "value" } })["key"]);

    [TestMethod]
    public void NonGenericIndexerSetter()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string>();
        ((IDictionary)observableSortedDictionary)["key"] = "value";
        Assert.AreEqual("value", observableSortedDictionary["key"]);
    }

    [TestMethod]
    public void NonGenericRemove() =>
        ((IDictionary)new ObservableSortedDictionary<string, string> { { "key", "value" } }).Remove("key");

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullOnChangedArgs() =>
        new DerivationWithNullOnChangedArgs<string, string>().Add("key", "value");

    [TestMethod]
    public void ObservedAdd()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string>();
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
        observableSortedDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("value", args!.NewItems[0]!.Value);
        }
        observableSortedDictionary.DictionaryChanged += dictionaryChangedHandler;
        void nonGenericDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("value", args!.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableSortedDictionary).DictionaryChanged += nonGenericDictionaryChangedHandler;
        observableSortedDictionary.Add("key", "value");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableSortedDictionary.CollectionChanged -= collectionChangedHandler;
        observableSortedDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableSortedDictionary).DictionaryChanged -= nonGenericDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedAddRange()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string>();
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableSortedDictionary.CollectionChanged += (sender, args) =>
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
            Assert.AreEqual(2, args.NewItems!.Count);
            Assert.AreEqual("key1", ((KeyValuePair<string, string>)args.NewItems[0]!).Key);
            Assert.AreEqual("value1", ((KeyValuePair<string, string>)args.NewItems[0]!).Value);
            Assert.AreEqual("key2", ((KeyValuePair<string, string>)args.NewItems[1]!).Key);
            Assert.AreEqual("value2", ((KeyValuePair<string, string>)args.NewItems[1]!).Value);
        };
        observableSortedDictionary.DictionaryChanged += (sender, args) =>
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args.Action);
            Assert.AreEqual(2, args.NewItems!.Count);
            Assert.AreEqual("key1", args.NewItems[0]!.Key);
            Assert.AreEqual("value1", args.NewItems[0]!.Value);
            Assert.AreEqual("key2", args.NewItems[1]!.Key);
            Assert.AreEqual("value2", args.NewItems[1]!.Value);
        };
        ((INotifyDictionaryChanged)observableSortedDictionary).DictionaryChanged += (sender, args) =>
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args.Action);
            Assert.AreEqual(2, args.NewItems!.Count);
            Assert.AreEqual("key1", args.NewItems[0]!.Key);
            Assert.AreEqual("value1", args.NewItems[0]!.Value);
            Assert.AreEqual("key2", args.NewItems[1]!.Key);
            Assert.AreEqual("value2", args.NewItems[1]!.Value);
        };
        observableSortedDictionary.AddRange(new Dictionary<string, string>
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
        var observableSortedDictionary = new ObservableSortedDictionary<string, string> { { "key", "value" } };
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableSortedDictionary.CollectionChanged += (sender, args) =>
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, args.Action);
            Assert.AreEqual(1, args.OldItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args.OldItems![0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args.OldItems![0]!).Value);
            Assert.IsNull(args.NewItems);
        };
        observableSortedDictionary.DictionaryChanged += (sender, args) =>
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args.Action);
            Assert.AreEqual(1, args.OldItems!.Count);
            Assert.AreEqual("key", args.OldItems![0]!.Key);
            Assert.AreEqual("value", args.OldItems![0]!.Value);
            Assert.AreEqual(0, args.NewItems!.Count);
        };
        ((INotifyDictionaryChanged)observableSortedDictionary).DictionaryChanged += (sender, args) =>
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args.Action);
            Assert.AreEqual(1, args.OldItems!.Count);
            Assert.AreEqual("key", args.OldItems![0]!.Key);
            Assert.AreEqual("value", args.OldItems![0]!.Value);
            Assert.AreEqual(0, args.NewItems!.Count);
        };
        observableSortedDictionary.Clear();
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
    }

    [TestMethod]
    public void ObservedCollectionAdd()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string>();
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableSortedDictionary.CollectionChanged += (sender, args) =>
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        };
        observableSortedDictionary.DictionaryChanged += (sender, args) =>
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("value", args!.NewItems[0]!.Value);
        };
        ((INotifyDictionaryChanged)observableSortedDictionary).DictionaryChanged += (sender, args) =>
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("value", args!.NewItems[0]!.Value);
        };
        ((ICollection<KeyValuePair<string, string>>)observableSortedDictionary).Add(new KeyValuePair<string, string>("key", "value"));
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
    }

    [TestMethod]
    public void ObservedNonGenericDictionaryAdd()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string>();
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableSortedDictionary.CollectionChanged += (sender, args) =>
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        };
        observableSortedDictionary.DictionaryChanged += (sender, args) =>
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("value", args!.NewItems[0]!.Value);
        };
        ((INotifyDictionaryChanged)observableSortedDictionary).DictionaryChanged += (sender, args) =>
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("value", args!.NewItems[0]!.Value);
        };
        ((IDictionary)observableSortedDictionary).Add("key", "value");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
    }

    [TestMethod]
    public void ObservedRemove()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string> { { "key", "value" } };
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableSortedDictionary.CollectionChanged += (sender, args) =>
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args!.OldItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.OldItems[0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args!.OldItems[0]!).Value);
        };
        observableSortedDictionary.DictionaryChanged += (sender, args) =>
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args!.OldItems!.Count);
            Assert.AreEqual("key", args!.OldItems[0]!.Key);
            Assert.AreEqual("value", args!.OldItems[0]!.Value);
        };
        ((INotifyDictionaryChanged)observableSortedDictionary).DictionaryChanged += (sender, args) =>
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args!.OldItems!.Count);
            Assert.AreEqual("key", args!.OldItems[0]!.Key);
            Assert.AreEqual("value", args!.OldItems[0]!.Value);
        };
        observableSortedDictionary.Remove("key");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
    }

    [TestMethod]
    public void ObservedReplace()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string> { { "key", "value" } };
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableSortedDictionary.CollectionChanged += (sender, args) =>
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
        observableSortedDictionary.DictionaryChanged += (sender, args) =>
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
        ((INotifyDictionaryChanged)observableSortedDictionary).DictionaryChanged += (sender, args) =>
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
        observableSortedDictionary["key"] = "new value";
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
    }

    [TestMethod]
    public void ObservedReset()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string> { { "key", "value" } };
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        observableSortedDictionary.CollectionChanged += (sender, args) =>
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, args.Action);
        };
        observableSortedDictionary.DictionaryChanged += (sender, args) =>
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Reset, args.Action);
        };
        ((INotifyDictionaryChanged)observableSortedDictionary).DictionaryChanged += (sender, args) =>
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Reset, args.Action);
        };
        observableSortedDictionary.Reset(new Dictionary<string, string> { { "other key", "other value" } });
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
    }

    [TestMethod]
    public void RangeDictionaryKeys() =>
        Assert.AreEqual(0, ((IRangeDictionary<string, string>)new ObservableSortedDictionary<string, string>()).Keys.Count());

    [TestMethod]
    public void RangeDictionaryValues() =>
        Assert.AreEqual(0, ((IRangeDictionary<string, string>)new ObservableSortedDictionary<string, string>()).Values.Count());

    [TestMethod]
    public void ReadOnlyDictionaryKeys() =>
        Assert.AreEqual(0, ((IReadOnlyDictionary<string, string>)new ObservableSortedDictionary<string, string>()).Keys.Count());

    [TestMethod]
    public void ReadOnlyDictionaryValues() =>
        Assert.AreEqual(0, ((IReadOnlyDictionary<string, string>)new ObservableSortedDictionary<string, string>()).Values.Count());

    [TestMethod]
    public void RemoveAll()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string> { { "key", "value" } };
        observableSortedDictionary.RemoveAll((k, v) => k.StartsWith("k"));
        Assert.AreEqual(0, observableSortedDictionary.Count);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void RemoveAllNullPredicate() =>
        new ObservableSortedDictionary<string, string> { { "key", "value" } }.RemoveAll(null!);

    [TestMethod]
    public void RemoveAndGetValue()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string> { { "key", "value" } };
        Assert.IsTrue(observableSortedDictionary.Remove("key", out var value));
        Assert.AreEqual("value", value);
        Assert.IsFalse(observableSortedDictionary.ContainsKey("key"));
    }

    [TestMethod]
    public void RemoveAndGetValueNotFound()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string> { { "key", "value" } };
        Assert.IsFalse(observableSortedDictionary.Remove("other key", out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void RemoveNotFound() =>
        Assert.IsFalse(new ObservableSortedDictionary<string, string> { { "key", "value" } }.Remove("other key"));

    [TestMethod]
    public void RemoveRange()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string> { { "key", "value" } };
        observableSortedDictionary.RemoveRange(new[] { "key" });
        Assert.AreEqual(0, observableSortedDictionary.Count);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void RemoveRangeNullKeys() =>
        new ObservableSortedDictionary<string, string> { { "key", "value" } }.RemoveRange(null!);

    [TestMethod]
    public void ReplaceRange()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string> { { "key", "value" } };
        observableSortedDictionary.ReplaceRange(new Dictionary<string, string> { { "key", "other value" } });
        Assert.AreEqual(1, observableSortedDictionary.Count);
        Assert.AreEqual("other value", observableSortedDictionary["key"]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ReplaceRangeKeyNotFound() =>
        new ObservableSortedDictionary<string, string> { { "key", "value" } }.ReplaceRange(new Dictionary<string, string> { { "other key", "other value" } });

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ReplaceRangeNullDictionary() =>
        new ObservableSortedDictionary<string, string> { { "key", "value" } }.ReplaceRange(null!);

    [TestMethod]
    public void ReplaceRangeWithKeys()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string> { { "key", "value" } };
        observableSortedDictionary.ReplaceRange(new[] { "key" }, new Dictionary<string, string> { { "key", "other value" }, { "yet another key", "yet another value" } });
        Assert.AreEqual(2, observableSortedDictionary.Count);
        Assert.AreEqual("other value", observableSortedDictionary["key"]);
        Assert.AreEqual("yet another value", observableSortedDictionary["yet another key"]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ReplaceRangeWithKeysKeyNotFound() =>
        new ObservableSortedDictionary<string, string> { { "key", "value" } }.ReplaceRange(new[] { "other key" }, new Dictionary<string, string> { { "key", "other value" } });

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ReplaceRangeWithKeysNullReplaceKeys() =>
        new ObservableSortedDictionary<string, string> { { "key", "value" } }.ReplaceRange(null!, new Dictionary<string, string> { { "key", "other value" } });

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ReplaceRangeWithKeysNullReplaceValues() =>
        new ObservableSortedDictionary<string, string> { { "key", "value" } }.ReplaceRange(new[] { "key" }, null!);

    [TestMethod]
    public void Reset() =>
        new ObservableSortedDictionary<string, string> { { "key", "value" } }.Reset();

    [TestMethod]
    public void ResetWithDictionary()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string> { { "key", "value" } };
        observableSortedDictionary.Reset(new Dictionary<string, string> { { "other key", "other value" }, { "yet another key", "yet another value" } });
        Assert.AreEqual(2, observableSortedDictionary.Count);
        Assert.AreEqual("other value", observableSortedDictionary["other key"]);
        Assert.AreEqual("yet another value", observableSortedDictionary["yet another key"]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ResetWithDictionaryNullDictionary() =>
        new ObservableSortedDictionary<string, string> { { "key", "value" } }.Reset(null!);

    [TestMethod]
    public void SyncRoot() =>
        Assert.IsNotNull(((ICollection)new ObservableSortedDictionary<string, string>()).SyncRoot);

    [TestMethod]
    public void TryAdd()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string>();
        Assert.IsTrue(observableSortedDictionary.TryAdd("key", "value"));
        Assert.AreEqual(1, observableSortedDictionary.Count);
        Assert.AreEqual("value", observableSortedDictionary["key"]);
        Assert.IsFalse(observableSortedDictionary.TryAdd("key", "other value"));
        Assert.AreEqual(1, observableSortedDictionary.Count);
        Assert.AreEqual("value", observableSortedDictionary["key"]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void TryAddNullKey() =>
        new ObservableSortedDictionary<string, string>().TryAdd(null!, "value");

    [TestMethod]
    public void TryGetValue()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string> { { "key", "value" } };
        Assert.IsTrue(observableSortedDictionary.TryGetValue("key", out var value));
        Assert.AreEqual("value", value);
        Assert.IsFalse(observableSortedDictionary.TryGetValue("other key", out value));
        Assert.IsNull(value);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void UnsupportedNotifyDictionaryChangedAction() =>
        new DerivationWithUnsupportedNotifyDictionaryChangedAction<string, string>().Add("key", "value");

    [TestMethod]
    public void Values()
    {
        var observableSortedDictionary = new ObservableSortedDictionary<string, string> { { "key", "value" } };
        var values = observableSortedDictionary.Values;
        Assert.AreEqual(1, values.Count);
        Assert.AreEqual("value", values.First());
    }
}
