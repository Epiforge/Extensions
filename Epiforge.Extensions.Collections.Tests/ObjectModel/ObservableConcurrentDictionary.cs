namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

[TestClass]
public class ObservableConcurrentDictionary
{
    #region Derivations

    class DerivationWithNullOnChangedArgs<TKey, TValue> :
        ObservableConcurrentDictionary<TKey, TValue>
        where TKey : notnull
    {
        public DerivationWithNullOnChangedArgs()
        {
        }

        protected override void OnChanged(NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
            base.OnChanged(null!);
    }

    #endregion Derivations

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void AddOrUpdateAddValueUpdateValueFactoryNullUpdateValueFactory() =>
        new ObservableConcurrentDictionary<string, string>().AddOrUpdate("key", "value", null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void AddOrUpdateAddValueFactoryUpdateValueFactoryNullAddValueFactory() =>
        new ObservableConcurrentDictionary<string, string>().AddOrUpdate("key", (Func<string, string>)null!, (key, value) => "value");

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void AddOrUpdateAddValueFactoryUpdateValueFactoryNullUpdateValueFactory() =>
        new ObservableConcurrentDictionary<string, string>().AddOrUpdate("key", key => "value", null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void AddOrUpdateAddValueFactoryUpdateValueFactoryFactoryArgumentNullAddValueFactory() =>
        new ObservableConcurrentDictionary<string, string>().AddOrUpdate("key", (Func<string, string, string>)null!, (key, value, arg) => "value", "value");

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void AddOrUpdateAddValueFactoryUpdateValueFactoryFactoryArgumentNullUpdateValueFactory() =>
        new ObservableConcurrentDictionary<string, string>().AddOrUpdate("key", (key, arg) => "value", null!, "value");

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void CollectionAddDuplicateKey() =>
        ((ICollection<KeyValuePair<string, string>>)new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } })).Add(new KeyValuePair<string, string>("key", "value"));

    [TestMethod]
    public void CollectionContains() =>
        Assert.IsTrue(((ICollection<KeyValuePair<string, string>>)new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } })).Contains(new KeyValuePair<string, string>("key", "value")));

    [TestMethod]
    public void CollectionCopyTo()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var array = new KeyValuePair<string, string>[1];
        ((ICollection)dictionary).CopyTo(array, 0);
        Assert.AreEqual("key", array[0].Key);
        Assert.AreEqual("value", array[0].Value);
    }

    [TestMethod]
    public void CollectionIsReadOnly()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>();
        Assert.IsFalse(((ICollection<KeyValuePair<string, string>>)dictionary).IsReadOnly);
    }

    [TestMethod]
    public void CollectionIsSynchronized()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>();
        Assert.IsFalse(((ICollection)dictionary).IsSynchronized);
    }

    [TestMethod]
    public void Construction()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>();
        Assert.AreEqual(0, dictionary.Count);
    }

    [TestMethod]
    public void ConstructionWithCollection()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual(1, dictionary.Count);
    }

    [TestMethod]
    public void ConstructionWithCollectionAndComparer()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } }, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(1, dictionary.Count);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase, dictionary.Comparer);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ConstructionWithCollectionAndNullComparer() =>
        new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } }, null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ConstructionWithComparerAndNullCollection() =>
        new ObservableConcurrentDictionary<string, string>(null!, StringComparer.OrdinalIgnoreCase);

    [TestMethod]
    public void ConstructionWithComparer()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(0, dictionary.Count);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase, dictionary.Comparer);
    }

    [TestMethod]
    public void ConstructionWithConcurrencyLevelAndCapacity()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(1, 1);
        Assert.AreEqual(0, dictionary.Count);
    }

    [TestMethod]
    public void ConstructionWithConcurrencyLevelCapacityAndComparer()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(1, 1, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(0, dictionary.Count);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase, dictionary.Comparer);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ConstructionWithConcurrencyLevelCapacityAndNullComparer() =>
        new ObservableConcurrentDictionary<string, string>(1, 1, null!);

    [TestMethod]
    public void ConstructionWithConcurrencyLevelCollectionAndComparer()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(1, new Dictionary<string, string> { { "key", "value" } }, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(1, dictionary.Count);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase, dictionary.Comparer);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ConstructionWithConcurrencyLevelNullCollectionAndComparer() =>
        new ObservableConcurrentDictionary<string, string>(1, null!, StringComparer.OrdinalIgnoreCase);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ConstructionWithConcurrencyLevelCollectionAndNullComparer() =>
        new ObservableConcurrentDictionary<string, string>(1, new Dictionary<string, string> { { "key", "value" } }, null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ConstructionWithNullComparer() =>
        new ObservableConcurrentDictionary<string, string>((IEqualityComparer<string>)null!);

    [TestMethod]
    public void ContainsKey() =>
        Assert.IsTrue(new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } }).ContainsKey("key"));

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DictionaryAddNullKey() =>
        ((IDictionary<string, string>)new ObservableConcurrentDictionary<string, string>()).Add(null!, "value");

    [TestMethod]
    public void DictionaryContains() =>
        Assert.IsTrue(((IDictionary)new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } })).Contains("key"));

    [TestMethod]
    public void DictionaryGetEnumerator()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var enumerator = ((IDictionary)dictionary).GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("key", enumerator.Key);
        Assert.AreEqual("value", enumerator.Value);
    }

    [TestMethod]
    public void DictionaryIsReadOnly()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>();
        Assert.IsFalse(((IDictionary)dictionary).IsReadOnly);
    }

    [TestMethod]
    public void DictionaryKeys()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual(1, ((IDictionary)dictionary).Keys.Count);
        Assert.AreEqual("key", ((IDictionary)dictionary).Keys.Cast<string>().ElementAt(0));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DictionaryRemoveNullKey() =>
        ((IDictionary)new ObservableConcurrentDictionary<string, string>()).Remove(null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void DictionaryRemoveInvalidKey() =>
        ((IDictionary)new ObservableConcurrentDictionary<string, string>()).Remove(3);

    [TestMethod]
    public void DictionaryValues()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual(1, ((IDictionary)dictionary).Values.Count);
        Assert.AreEqual("value", ((IDictionary)dictionary).Values.Cast<string>().ElementAt(0));
    }

    [TestMethod]
    public void EnumerableGetEnumerator()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var enumerator = ((IEnumerable)dictionary).GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("key", ((KeyValuePair<string, string>)enumerator.Current).Key);
        Assert.AreEqual("value", ((KeyValuePair<string, string>)enumerator.Current).Value);
    }

    [TestMethod]
    public void GenericCollectionCopyTo()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var array = new KeyValuePair<string, string>[1];
        ((ICollection<KeyValuePair<string, string>>)dictionary).CopyTo(array, 0);
        Assert.AreEqual("key", array[0].Key);
        Assert.AreEqual("value", array[0].Value);
    }

    [TestMethod]
    public void GetEnumerator()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var enumerator = dictionary.GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("key", enumerator.Current.Key);
        Assert.AreEqual("value", enumerator.Current.Value);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void GetOrAddValueFactoryNullValueFactory() =>
        new ObservableConcurrentDictionary<string, string>().GetOrAdd("key", (Func<string, string>)null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void GetOrAddValueFactoryNullValueFactoryFactoryArgument() =>
        new ObservableConcurrentDictionary<string, string>().GetOrAdd("key", (Func<string, string, string>)null!, "value");

    [TestMethod]
    public void IndexerGetter()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual("value", dictionary["key"]);
    }

    [TestMethod]
    public void IsEmpty()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>();
        Assert.IsTrue(dictionary.IsEmpty);
        Assert.IsTrue(dictionary.TryAdd("key", "value"));
        Assert.IsFalse(dictionary.IsEmpty);
    }

    [TestMethod]
    public void IsFixedSize()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>();
        Assert.IsFalse(((IDictionary)dictionary).IsFixedSize);
    }

    [TestMethod]
    public void Keys()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual(1, dictionary.Keys.Count);
        Assert.AreEqual("key", dictionary.Keys.ElementAt(0));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NonGenericAddNullKey() =>
        ((IDictionary)new ObservableConcurrentDictionary<string, string>()).Add(null!, "value");

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void NonGenericAddInvalidKey() =>
        ((IDictionary)new ObservableConcurrentDictionary<string, string>()).Add(1, "value");

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void NonGenericAddInvalidValue() =>
        ((IDictionary)new ObservableConcurrentDictionary<string, string>()).Add("key", 1);

    [TestMethod]
    public void NonGenericIndexerGetter()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual("value", ((IDictionary)dictionary)["key"]);
    }

    [TestMethod]
    public void NonGenericIndexerSetter()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        ((IDictionary)dictionary)["key"] = "new value";
        Assert.AreEqual("new value", dictionary["key"]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NonGenericIndexerSetterNullKey() =>
        ((IDictionary)new ObservableConcurrentDictionary<string, string>())[null!] = "value";

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void NonGenericIndexerSetterInvalidKey() =>
        ((IDictionary)new ObservableConcurrentDictionary<string, string>())[new object()] = "value";

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void NonGenericIndexerSetterInvalidValue() =>
        ((IDictionary)new ObservableConcurrentDictionary<string, string>())["key"] = new object();

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullOnChangedArgs() =>
        new DerivationWithNullOnChangedArgs<string, string>().TryAdd("key", "value");

    [TestMethod]
    public void ObservedAddOrUpdateAddValueUpdateValueFactoryAdd()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>();
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
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value", args.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value", args.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        observableDictionary.AddOrUpdate("key", "value", (key, value) => value + "2");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedAddOrUpdateAddValueUpdateValueFactoryUpdate()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Replace, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("value2", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Replace, args!.Action);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value2", args.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Replace, args!.Action);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value2", args.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        observableDictionary.AddOrUpdate("key", "value", (key, value) => value + "2");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedAddOrUpdateAddValueFactoryUpdateValueFactoryAdd()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>();
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
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value", args.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value", args.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        observableDictionary.AddOrUpdate("key", key => "value", (key, value) => value + "2");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedAddOrUpdateAddValueFactoryUpdateValueFactoryUpdate()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Replace, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("value2", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Replace, args!.Action);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value2", args.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Replace, args!.Action);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value2", args.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        observableDictionary.AddOrUpdate("key", key => "value", (key, value) => value + "2");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedAddOrUpdateAddValueFactoryUpdateValueFactoryFactoryArgumentAdd()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>();
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
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value", args.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value", args.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        observableDictionary.AddOrUpdate("key", (key, arg) => arg, (key, value, arg) => arg + "2", "value");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedAddOrUpdateAddValueFactoryUpdateValueFactoryFactoryArgumentUpdate()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Replace, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("value2", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Replace, args!.Action);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value2", args.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Replace, args!.Action);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value2", args.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        observableDictionary.AddOrUpdate("key", (key, arg) => arg, (key, value, arg) => arg + "2", "value");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedClear()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args!.OldItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.OldItems[0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args!.OldItems[0]!).Value);
            Assert.IsNull(args.NewItems);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args.OldItems.Count);
            Assert.AreEqual("key", args.OldItems[0]!.Key);
            Assert.AreEqual("value", args.OldItems[0]!.Value);
            Assert.AreEqual(0, args.NewItems.Count);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args.OldItems.Count);
            Assert.AreEqual("key", args.OldItems[0]!.Key);
            Assert.AreEqual("value", args.OldItems[0]!.Value);
            Assert.AreEqual(0, args.NewItems.Count);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        observableDictionary.Clear();
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedCollectionAdd()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>();
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args!.Action);
            Assert.IsNull(args.OldItems);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value", args.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value", args.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        ((ICollection<KeyValuePair<string, string>>)observableDictionary).Add(new KeyValuePair<string, string>("key", "value"));
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedCollectionRemove()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args!.OldItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.OldItems[0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args!.OldItems[0]!).Value);
            Assert.IsNull(args.NewItems);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args.OldItems.Count);
            Assert.AreEqual("key", args.OldItems[0]!.Key);
            Assert.AreEqual("value", args.OldItems[0]!.Value);
            Assert.AreEqual(0, args.NewItems.Count);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args.OldItems.Count);
            Assert.AreEqual("key", args.OldItems[0]!.Key);
            Assert.AreEqual("value", args.OldItems[0]!.Value);
            Assert.AreEqual(0, args.NewItems.Count);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        Assert.IsTrue(((ICollection<KeyValuePair<string, string>>)observableDictionary).Remove(new KeyValuePair<string, string>("key", "value")));
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedDictionaryAdd()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>();
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args!.Action);
            Assert.IsNull(args.OldItems);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value", args.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value", args.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        ((IDictionary)observableDictionary).Add("key", "value");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedDictionaryRemove()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args!.OldItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.OldItems[0]!).Key);
            Assert.AreEqual("value", ((KeyValuePair<string, string>)args!.OldItems[0]!).Value);
            Assert.IsNull(args.NewItems);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args.OldItems.Count);
            Assert.AreEqual("key", args.OldItems[0]!.Key);
            Assert.AreEqual("value", args.OldItems[0]!.Value);
            Assert.AreEqual(0, args.NewItems.Count);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Remove, args!.Action);
            Assert.AreEqual(1, args.OldItems.Count);
            Assert.AreEqual("key", args.OldItems[0]!.Key);
            Assert.AreEqual("value", args.OldItems[0]!.Value);
            Assert.AreEqual(0, args.NewItems.Count);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        ((IDictionary)observableDictionary).Remove("key");
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedGetOrAddValueGet()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        [ExcludeFromCodeCoverage]
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args) =>
            collectionChangeObserved = true;
        observableDictionary.CollectionChanged += collectionChangedHandler;
        [ExcludeFromCodeCoverage]
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args) =>
            dictionaryChangeObserved = true;
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        [ExcludeFromCodeCoverage]
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args) =>
            boxedDictionaryChangeObserved = true;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        Assert.AreEqual("value", observableDictionary.GetOrAdd("key", "value2"));
        Assert.IsFalse(collectionChangeObserved);
        Assert.IsFalse(dictionaryChangeObserved);
        Assert.IsFalse(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedGetOrAddValueAdd()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>();
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args!.Action);
            Assert.IsNull(args.OldItems);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("value2", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value2", args.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value2", args.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        Assert.AreEqual("value2", observableDictionary.GetOrAdd("key", "value2"));
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedGetOrAddValueFactoryGet()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        [ExcludeFromCodeCoverage]
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args) =>
            collectionChangeObserved = true;
        observableDictionary.CollectionChanged += collectionChangedHandler;
        [ExcludeFromCodeCoverage]
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args) =>
            dictionaryChangeObserved = true;
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        [ExcludeFromCodeCoverage]
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args) =>
            boxedDictionaryChangeObserved = true;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        Assert.AreEqual("value", observableDictionary.GetOrAdd("key", k => "value2"));
        Assert.IsFalse(collectionChangeObserved);
        Assert.IsFalse(dictionaryChangeObserved);
        Assert.IsFalse(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedGetOrAddValueFactoryAdd()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>();
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args!.Action);
            Assert.IsNull(args.OldItems);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("value2", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value2", args.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value2", args.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        Assert.AreEqual("value2", observableDictionary.GetOrAdd("key", k => "value2"));
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedGetOrAddValueFactoryFactoryArgumentGet()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        [ExcludeFromCodeCoverage]
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args) =>
            collectionChangeObserved = true;
        observableDictionary.CollectionChanged += collectionChangedHandler;
        [ExcludeFromCodeCoverage]
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args) =>
            dictionaryChangeObserved = true;
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        [ExcludeFromCodeCoverage]
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args) =>
            boxedDictionaryChangeObserved = true;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        Assert.AreEqual("value", observableDictionary.GetOrAdd("key", (k, a) => "value" + a, "2"));
        Assert.IsFalse(collectionChangeObserved);
        Assert.IsFalse(dictionaryChangeObserved);
        Assert.IsFalse(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedGetOrAddValueFactoryFactoryArgumentAdd()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>();
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args!.Action);
            Assert.IsNull(args.OldItems);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("value2", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value2", args.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void boxedDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Add, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args.NewItems[0]!.Key);
            Assert.AreEqual("value2", args.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += boxedDictionaryChangedHandler;
        Assert.AreEqual("value2", observableDictionary.GetOrAdd("key", (k, a) => "value" + a, "2"));
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= boxedDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedIndexerSetter()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Replace, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("new value", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Replace, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("new value", args!.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void nonGenericDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Replace, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("new value", args!.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += nonGenericDictionaryChangedHandler;
        observableDictionary["key"] = "new value";
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= nonGenericDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedReset()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, args!.Action);
            Assert.IsNull(args.OldItems);
            Assert.IsNull(args.NewItems);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Reset, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(0, args!.NewItems!.Count);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void nonGenericDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Reset, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(0, args!.NewItems!.Count);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += nonGenericDictionaryChangedHandler;
        Assert.AreEqual(1, observableDictionary.Count);
        observableDictionary.Reset();
        Assert.AreEqual(0, observableDictionary.Count);
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= nonGenericDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedResetWithKeyValuePairs()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, args!.Action);
            Assert.IsNull(args.OldItems);
            Assert.IsNull(args.NewItems);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Reset, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(0, args!.NewItems!.Count);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void nonGenericDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Reset, args!.Action);
            Assert.AreEqual(0, args!.OldItems!.Count);
            Assert.AreEqual(0, args!.NewItems!.Count);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += nonGenericDictionaryChangedHandler;
        Assert.AreEqual(1, observableDictionary.Count);
        Assert.AreEqual("value", observableDictionary["key"]);
        observableDictionary.Reset(new Dictionary<string, string> { { "key2", "value2" } });
        Assert.AreEqual(1, observableDictionary.Count);
        Assert.AreEqual("value2", observableDictionary["key2"]);
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= nonGenericDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedTryUpdate()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            collectionChangeObserved = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Replace, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", ((KeyValuePair<string, string>)args!.NewItems[0]!).Key);
            Assert.AreEqual("new value", ((KeyValuePair<string, string>)args!.NewItems[0]!).Value);
        }
        observableDictionary.CollectionChanged += collectionChangedHandler;
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args)
        {
            dictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Replace, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("new value", args!.NewItems[0]!.Value);
        }
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        void nonGenericDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args)
        {
            boxedDictionaryChangeObserved = true;
            Assert.AreEqual(NotifyDictionaryChangedAction.Replace, args!.Action);
            Assert.AreEqual(1, args!.NewItems!.Count);
            Assert.AreEqual("key", args!.NewItems[0]!.Key);
            Assert.AreEqual("new value", args!.NewItems[0]!.Value);
        }
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += nonGenericDictionaryChangedHandler;
        Assert.IsTrue(observableDictionary.TryUpdate("key", "new value", "value"));
        Assert.IsTrue(collectionChangeObserved);
        Assert.IsTrue(dictionaryChangeObserved);
        Assert.IsTrue(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= nonGenericDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedTryUpdateNotFound()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        [ExcludeFromCodeCoverage]
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args) =>
            collectionChangeObserved = true;
        observableDictionary.CollectionChanged += collectionChangedHandler;
        [ExcludeFromCodeCoverage]
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args) =>
            dictionaryChangeObserved = true;
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        [ExcludeFromCodeCoverage]
        void nonGenericDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args) =>
            boxedDictionaryChangeObserved = true;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += nonGenericDictionaryChangedHandler;
        Assert.IsFalse(observableDictionary.TryUpdate("new key", "new value", "value"));
        Assert.IsFalse(collectionChangeObserved);
        Assert.IsFalse(dictionaryChangeObserved);
        Assert.IsFalse(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= nonGenericDictionaryChangedHandler;
    }

    [TestMethod]
    public void ObservedTryUpdateValueUnequal()
    {
        var observableDictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var collectionChangeObserved = false;
        var dictionaryChangeObserved = false;
        var boxedDictionaryChangeObserved = false;
        [ExcludeFromCodeCoverage]
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args) =>
            collectionChangeObserved = true;
        observableDictionary.CollectionChanged += collectionChangedHandler;
        [ExcludeFromCodeCoverage]
        void dictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<string, string> args) =>
            dictionaryChangeObserved = true;
        observableDictionary.DictionaryChanged += dictionaryChangedHandler;
        [ExcludeFromCodeCoverage]
        void nonGenericDictionaryChangedHandler(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> args) =>
            boxedDictionaryChangeObserved = true;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged += nonGenericDictionaryChangedHandler;
        Assert.IsFalse(observableDictionary.TryUpdate("key", "new value", "val-you"));
        Assert.IsFalse(collectionChangeObserved);
        Assert.IsFalse(dictionaryChangeObserved);
        Assert.IsFalse(boxedDictionaryChangeObserved);
        observableDictionary.CollectionChanged -= collectionChangedHandler;
        observableDictionary.DictionaryChanged -= dictionaryChangedHandler;
        ((INotifyDictionaryChanged)observableDictionary).DictionaryChanged -= nonGenericDictionaryChangedHandler;
    }

    [TestMethod]
    public void ReadOnlyDictionaryKeys()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual(1, ((IReadOnlyDictionary<string, string>)dictionary).Keys.Count());
        Assert.AreEqual("key", ((IReadOnlyDictionary<string, string>)dictionary).Keys.Cast<string>().ElementAt(0));
    }

    [TestMethod]
    public void ReadOnlyDictionaryValues()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual(1, ((IReadOnlyDictionary<string, string>)dictionary).Values.Count());
        Assert.AreEqual("value", ((IReadOnlyDictionary<string, string>)dictionary).Values.Cast<string>().ElementAt(0));
    }

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void SyncRoot() =>
        _ = ((ICollection)new ObservableConcurrentDictionary<string, string>()).SyncRoot;

    [TestMethod]
    public void ToArray()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        var array = dictionary.ToArray();
        Assert.AreEqual(1, array.Length);
        Assert.AreEqual("key", array[0].Key);
        Assert.AreEqual("value", array[0].Value);
    }

    [TestMethod]
    public void TryRemoveNotFound() =>
        Assert.IsFalse(new ObservableConcurrentDictionary<string, string>().TryRemove("key", out _));

    [TestMethod]
    public void Values()
    {
        var dictionary = new ObservableConcurrentDictionary<string, string>(new Dictionary<string, string> { { "key", "value" } });
        Assert.AreEqual(1, dictionary.Values.Count);
        Assert.AreEqual("value", dictionary.Values.ElementAt(0));
    }
}
