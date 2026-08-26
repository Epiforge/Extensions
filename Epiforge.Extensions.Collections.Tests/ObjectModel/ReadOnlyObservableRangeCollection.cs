namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

[TestClass]
public class ReadOnlyObservableRangeCollection
{
    [TestMethod]
    public void CollectionChanged()
    {
        var source = new ObservableRangeCollection<int>();
        using var readOnlyCollection = new ReadOnlyObservableRangeCollection<int>(source);
        var collectionChanged = false;
        readOnlyCollection.CollectionChanged += (sender, e) => collectionChanged = true;
        source.Add(1);
        Assert.IsTrue(collectionChanged);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ConstructorNullCollection() =>
        new ReadOnlyObservableRangeCollection<int>(null!);

    [TestMethod]
    public void Contains() =>
        Assert.IsTrue(new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))).Contains(2));

    [TestMethod]
    public void CopyTo()
    {
        var array = new int[3];
        new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))).CopyTo(array, 0);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, array);
    }

    [TestMethod]
    public void Count() =>
        Assert.AreEqual(3, new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))).Count);

    [TestMethod]
    public void DisposeUnsubscribesFromCollectionChanged()
    {
        var source = new ObservableRangeCollection<int>();
        var readOnlyCollection = new ReadOnlyObservableRangeCollection<int>(source);
        var collectionChanges = 0;
        readOnlyCollection.CollectionChanged += (sender, e) => ++collectionChanges;
        source.Add(1);
        Assert.AreEqual(1, collectionChanges);
        readOnlyCollection.Dispose();
        source.Add(2);
        Assert.AreEqual(1, collectionChanges);
    }

    [TestMethod]
    public void DisposeUnsubscribesFromPropertyChanged()
    {
        var source = new ObservableRangeCollection<int>();
        var readOnlyCollection = new ReadOnlyObservableRangeCollection<int>(source);
        var countChanges = 0;
        readOnlyCollection.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(readOnlyCollection.Count))
                ++countChanges;
        };
        source.Add(1);
        Assert.AreEqual(1, countChanges);
        readOnlyCollection.Dispose();
        source.Add(2);
        Assert.AreEqual(1, countChanges);
    }

    [TestMethod]
    public void GetEnumerator() =>
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))).ToList());

    [TestMethod]
    public void GetRange()
    {
        var range = new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 10))).GetRange(3, 3);
        Assert.AreEqual(3, range.Count);
        Assert.AreEqual(4, range[0]);
        Assert.AreEqual(5, range[1]);
        Assert.AreEqual(6, range[2]);
    }

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void ICollectionAdd() =>
        ((ICollection<int>)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).Add(4);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void ICollectionClear() =>
        ((ICollection<int>)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).Clear();

    [TestMethod]
    public void ICollectionCopyTo()
    {
        var array = new int[3];
        ((ICollection)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).CopyTo(array, 0);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, array);
    }

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void ICollectionRemove() =>
        ((ICollection<int>)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).Remove(1);

    [TestMethod]
    public void IEnumerableGetEnumerator()
    {
        var enumerator = ((IEnumerable)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual(1, enumerator.Current);
    }

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IListAdd() =>
        ((IList)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).Add(4);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IListClear() =>
        ((IList)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).Clear();

    [TestMethod]
    public void IListContains() =>
        Assert.IsTrue(((IList)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).Contains(2));

    [TestMethod]
    public void IListIndexerGetter() =>
        Assert.AreEqual(2, ((IList)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))))[1]);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IListIndexerSetter() =>
        ((IList)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))))[1] = 4;

    [TestMethod]
    public void IListIndexOf() =>
        Assert.AreEqual(1, ((IList)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).IndexOf(2));

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IListInsert() =>
        ((IList)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).Insert(0, 4);

    [TestMethod]
    public void IListIsFixedSize() =>
        Assert.IsFalse(((IList)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).IsFixedSize);

    [TestMethod]
    public void IListIsReadOnly() =>
        Assert.IsTrue(((IList)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).IsReadOnly);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IListRemove() =>
        ((IList)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).Remove(1);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IListRemoveAt() =>
        ((IList)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).RemoveAt(0);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IListGenericIndexerSetter() =>
        ((IList<int>)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))))[1] = 4;

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IListGenericInsert() =>
        ((IList<int>)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).Insert(0, 4);

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void IListGenericRemoveAt() =>
        ((IList<int>)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))).RemoveAt(0);

    [TestMethod]
    public void Indexer() =>
        Assert.AreEqual(2, new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3)))[1]);

    [TestMethod]
    public void IndexOf() =>
        Assert.AreEqual(1, new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))).IndexOf(2));

    [TestMethod]
    public void IsNotAnObservableRangeCollection() =>
        Assert.IsFalse((object)new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))) is IObservableRangeCollection<int>);

    [TestMethod]
    public void IsReadOnly() =>
        Assert.IsTrue(new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))).IsReadOnly);

    [TestMethod]
    public void IsSynchronized() =>
        Assert.IsFalse(new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))).IsSynchronized);

    [TestMethod]
    public void PropertyChanged()
    {
        var source = new ObservableRangeCollection<int>();
        using var readOnlyCollection = new ReadOnlyObservableRangeCollection<int>(source);
        var propertyChanged = false;
        readOnlyCollection.PropertyChanged += (sender, e) => propertyChanged = true;
        source.Add(1);
        Assert.IsTrue(propertyChanged);
    }

    [TestMethod]
    public void SyncRoot() =>
        Assert.IsNotNull(new ReadOnlyObservableRangeCollection<int>(new ObservableRangeCollection<int>(Enumerable.Range(1, 3))).SyncRoot);

    [TestMethod]
    public void WrapsAReadOnlyObservableRangeCollection()
    {
        var source = new ObservableRangeCollection<int>();
        using var inner = new ReadOnlyObservableRangeCollection<int>(source);
        using var outer = new ReadOnlyObservableRangeCollection<int>(inner);
        var collectionChanged = false;
        outer.CollectionChanged += (sender, e) => collectionChanged = true;
        source.Add(1);
        Assert.IsTrue(collectionChanged);
        Assert.AreEqual(1, outer.Count);
        Assert.AreEqual(1, outer[0]);
    }
}
