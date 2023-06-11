namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

[TestClass]
public class RangeObservableCollection
{
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void GetAndRemoveAllNullPredicate() =>
        new RangeObservableCollection<int>().GetAndRemoveAll(null!);

    [TestMethod]
    public void GetRange()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10));
        var range = rangeObservableCollection.GetRange(3, 3);
        Assert.AreEqual(3, range.Count);
        Assert.AreEqual(4, range[0]);
        Assert.AreEqual(5, range[1]);
        Assert.AreEqual(6, range[2]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void InsertRangeNullCollection() =>
        new RangeObservableCollection<int>().InsertRange(0, null!);

    [TestMethod]
    public void ObservedAddRange()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>();
        var collectionChanged = false;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            collectionChanged = true;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, e.Action);
            Assert.IsNull(e.OldItems);
            Assert.AreEqual(0, e.NewStartingIndex);
            Assert.AreEqual(3, e.NewItems?.Count);
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(0, rangeObservableCollection.Count);
        rangeObservableCollection.AddRange(new int[] { 1, 2, 3 });
        Assert.AreEqual(3, rangeObservableCollection.Count);
        Assert.AreEqual(1, rangeObservableCollection[0]);
        Assert.AreEqual(2, rangeObservableCollection[1]);
        Assert.AreEqual(3, rangeObservableCollection[2]);
        Assert.IsTrue(collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedGetAndRemoveAll()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10));
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, e.Action);
            Assert.IsNotNull(e.OldItems);
            foreach (var item in e.OldItems)
            {
                Assert.IsInstanceOfType(item, typeof(int));
                Assert.AreEqual(0, (int)item % 3);
            }
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(10, rangeObservableCollection.Count);
        var removed = rangeObservableCollection.GetAndRemoveAll(i => i % 3 == 0);
        Assert.AreEqual(3, removed.Count);
        Assert.AreEqual(3, removed[0]);
        Assert.AreEqual(6, removed[1]);
        Assert.AreEqual(9, removed[2]);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedIndividualAddRange()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(true);
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            Assert.AreEqual(NotifyCollectionChangedAction.Add, e.Action);
            Assert.IsNull(e.OldItems);
            Assert.AreEqual(rangeObservableCollection.Count - 1, e.NewStartingIndex);
            Assert.AreEqual(1, e.NewItems?.Count);
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(0, rangeObservableCollection.Count);
        rangeObservableCollection.AddRange(new int[] { 1, 2, 3 });
        Assert.AreEqual(3, rangeObservableCollection.Count);
        Assert.AreEqual(1, rangeObservableCollection[0]);
        Assert.AreEqual(2, rangeObservableCollection[1]);
        Assert.AreEqual(3, rangeObservableCollection[2]);
        Assert.AreEqual(3, collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedIndividualMoveRangeBackward()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10), true);
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            Assert.AreEqual(NotifyCollectionChangedAction.Move, e.Action);
            Assert.IsNotNull(e.OldItems);
            Assert.AreEqual(1, e.OldItems?.Count);
            Assert.IsNotNull(e.NewItems);
            Assert.AreEqual(1, e.NewItems?.Count);
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(10, rangeObservableCollection.Count);
        rangeObservableCollection.MoveRange(6, 3, 3);
        Assert.AreEqual(10, rangeObservableCollection.Count);
        Assert.AreEqual(1, rangeObservableCollection[0]);
        Assert.AreEqual(2, rangeObservableCollection[1]);
        Assert.AreEqual(3, rangeObservableCollection[2]);
        Assert.AreEqual(7, rangeObservableCollection[3]);
        Assert.AreEqual(8, rangeObservableCollection[4]);
        Assert.AreEqual(9, rangeObservableCollection[5]);
        Assert.AreEqual(4, rangeObservableCollection[6]);
        Assert.AreEqual(5, rangeObservableCollection[7]);
        Assert.AreEqual(6, rangeObservableCollection[8]);
        Assert.AreEqual(10, rangeObservableCollection[9]);
        Assert.AreEqual(3, collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedIndividualMoveRangeForward()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10), true);
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            Assert.AreEqual(NotifyCollectionChangedAction.Move, e.Action);
            Assert.IsNotNull(e.OldItems);
            Assert.AreEqual(1, e.OldItems?.Count);
            Assert.IsNotNull(e.NewItems);
            Assert.AreEqual(1, e.NewItems?.Count);
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(10, rangeObservableCollection.Count);
        rangeObservableCollection.MoveRange(3, 6, 3);
        Assert.AreEqual(10, rangeObservableCollection.Count);
        Assert.AreEqual(1, rangeObservableCollection[0]);
        Assert.AreEqual(2, rangeObservableCollection[1]);
        Assert.AreEqual(3, rangeObservableCollection[2]);
        Assert.AreEqual(7, rangeObservableCollection[3]);
        Assert.AreEqual(8, rangeObservableCollection[4]);
        Assert.AreEqual(9, rangeObservableCollection[5]);
        Assert.AreEqual(4, rangeObservableCollection[6]);
        Assert.AreEqual(5, rangeObservableCollection[7]);
        Assert.AreEqual(6, rangeObservableCollection[8]);
        Assert.AreEqual(10, rangeObservableCollection[9]);
        Assert.AreEqual(3, collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedIndividualRemoveRangeWithIndexAndCount()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10), true);
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, e.Action);
            Assert.IsNotNull(e.OldItems);
            Assert.AreEqual(1, e.OldItems?.Count);
            Assert.AreEqual(3, e.OldStartingIndex);
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(10, rangeObservableCollection.Count);
        rangeObservableCollection.RemoveRange(3, 3);
        Assert.AreEqual(7, rangeObservableCollection.Count);
        Assert.AreEqual(1, rangeObservableCollection[0]);
        Assert.AreEqual(2, rangeObservableCollection[1]);
        Assert.AreEqual(3, rangeObservableCollection[2]);
        Assert.AreEqual(7, rangeObservableCollection[3]);
        Assert.AreEqual(8, rangeObservableCollection[4]);
        Assert.AreEqual(9, rangeObservableCollection[5]);
        Assert.AreEqual(10, rangeObservableCollection[6]);
        Assert.AreEqual(3, collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedIndividualReplaceAll()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10), true);
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            if (collectionChanged == 1)
            {
                Assert.AreEqual(NotifyCollectionChangedAction.Reset, e.Action);
                Assert.IsNull(e.OldItems);
                Assert.IsNull(e.NewItems);
            }
            else
            {
                Assert.AreEqual(NotifyCollectionChangedAction.Add, e.Action);
                Assert.IsNotNull(e.NewItems);
                Assert.AreEqual(1, e.NewItems?.Count);
            }
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(10, rangeObservableCollection.Count);
        rangeObservableCollection.ReplaceAll(Enumerable.Range(11, 10));
        Assert.AreEqual(10, rangeObservableCollection.Count);
        Assert.AreEqual(11, rangeObservableCollection[0]);
        Assert.AreEqual(12, rangeObservableCollection[1]);
        Assert.AreEqual(13, rangeObservableCollection[2]);
        Assert.AreEqual(14, rangeObservableCollection[3]);
        Assert.AreEqual(15, rangeObservableCollection[4]);
        Assert.AreEqual(16, rangeObservableCollection[5]);
        Assert.AreEqual(17, rangeObservableCollection[6]);
        Assert.AreEqual(18, rangeObservableCollection[7]);
        Assert.AreEqual(19, rangeObservableCollection[8]);
        Assert.AreEqual(20, rangeObservableCollection[9]);
        Assert.AreEqual(11, collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedIndividualReplaceRange()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10), true);
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (++collectionChanged < 4)
            {
                Assert.AreEqual(NotifyCollectionChangedAction.Remove, e.Action);
                Assert.IsNotNull(e.OldItems);
                Assert.AreEqual(1, e.OldItems?.Count);
                Assert.AreEqual(3, e.OldStartingIndex);
            }
            else
            {
                Assert.AreEqual(NotifyCollectionChangedAction.Add, e.Action);
                Assert.IsNotNull(e.NewItems);
                Assert.AreEqual(1, e.NewItems?.Count);
                Assert.AreEqual(3 + collectionChanged - 4, e.NewStartingIndex);
            }
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(10, rangeObservableCollection.Count);
        rangeObservableCollection.ReplaceRange(3, 3, Enumerable.Range(11, 3));
        Assert.AreEqual(10, rangeObservableCollection.Count);
        Assert.AreEqual(1, rangeObservableCollection[0]);
        Assert.AreEqual(2, rangeObservableCollection[1]);
        Assert.AreEqual(3, rangeObservableCollection[2]);
        Assert.AreEqual(11, rangeObservableCollection[3]);
        Assert.AreEqual(12, rangeObservableCollection[4]);
        Assert.AreEqual(13, rangeObservableCollection[5]);
        Assert.AreEqual(7, rangeObservableCollection[6]);
        Assert.AreEqual(8, rangeObservableCollection[7]);
        Assert.AreEqual(9, rangeObservableCollection[8]);
        Assert.AreEqual(10, rangeObservableCollection[9]);
        Assert.AreEqual(6, collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedMoveRange()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10));
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            Assert.AreEqual(NotifyCollectionChangedAction.Move, e.Action);
            Assert.IsNotNull(e.OldItems);
            Assert.AreEqual(3, e.OldItems?.Count);
            Assert.AreEqual(3, e.OldStartingIndex);
            Assert.IsNotNull(e.NewItems);
            Assert.AreEqual(3, e.NewItems?.Count);
            Assert.AreEqual(6, e.NewStartingIndex);
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(10, rangeObservableCollection.Count);
        rangeObservableCollection.MoveRange(3, 6, 3);
        Assert.AreEqual(10, rangeObservableCollection.Count);
        Assert.AreEqual(1, rangeObservableCollection[0]);
        Assert.AreEqual(2, rangeObservableCollection[1]);
        Assert.AreEqual(3, rangeObservableCollection[2]);
        Assert.AreEqual(7, rangeObservableCollection[3]);
        Assert.AreEqual(8, rangeObservableCollection[4]);
        Assert.AreEqual(9, rangeObservableCollection[5]);
        Assert.AreEqual(4, rangeObservableCollection[6]);
        Assert.AreEqual(5, rangeObservableCollection[7]);
        Assert.AreEqual(6, rangeObservableCollection[8]);
        Assert.AreEqual(10, rangeObservableCollection[9]);
        Assert.AreEqual(1, collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedRemoveRangeWithIndexAndCount()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10));
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, e.Action);
            Assert.IsNotNull(e.OldItems);
            Assert.AreEqual(3, e.OldItems?.Count);
            Assert.AreEqual(3, e.OldStartingIndex);
            Assert.IsNull(e.NewItems);
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(10, rangeObservableCollection.Count);
        rangeObservableCollection.RemoveRange(3, 3);
        Assert.AreEqual(7, rangeObservableCollection.Count);
        Assert.AreEqual(1, rangeObservableCollection[0]);
        Assert.AreEqual(2, rangeObservableCollection[1]);
        Assert.AreEqual(3, rangeObservableCollection[2]);
        Assert.AreEqual(7, rangeObservableCollection[3]);
        Assert.AreEqual(8, rangeObservableCollection[4]);
        Assert.AreEqual(9, rangeObservableCollection[5]);
        Assert.AreEqual(10, rangeObservableCollection[6]);
        Assert.AreEqual(1, collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedRemoveRangeWithItems()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10));
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, e.Action);
            Assert.IsNotNull(e.OldItems);
            Assert.AreEqual(1, e.OldItems?.Count);
            Assert.AreEqual(3, e.OldStartingIndex);
            Assert.IsNull(e.NewItems);
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(10, rangeObservableCollection.Count);
        rangeObservableCollection.RemoveRange(new[] { 4, 5, 6 });
        Assert.AreEqual(7, rangeObservableCollection.Count);
        Assert.AreEqual(1, rangeObservableCollection[0]);
        Assert.AreEqual(2, rangeObservableCollection[1]);
        Assert.AreEqual(3, rangeObservableCollection[2]);
        Assert.AreEqual(7, rangeObservableCollection[3]);
        Assert.AreEqual(8, rangeObservableCollection[4]);
        Assert.AreEqual(9, rangeObservableCollection[5]);
        Assert.AreEqual(10, rangeObservableCollection[6]);
        Assert.AreEqual(3, collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedReplaceAll()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10));
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            Assert.AreEqual(NotifyCollectionChangedAction.Replace, e.Action);
            Assert.IsNotNull(e.OldItems);
            Assert.AreEqual(10, e.OldItems?.Count);
            Assert.AreEqual(0, e.OldStartingIndex);
            Assert.IsNotNull(e.NewItems);
            Assert.AreEqual(9, e.NewItems?.Count);
            Assert.AreEqual(0, e.NewStartingIndex);
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(10, rangeObservableCollection.Count);
        rangeObservableCollection.ReplaceAll(Enumerable.Range(11, 9));
        Assert.AreEqual(9, rangeObservableCollection.Count);
        Assert.AreEqual(11, rangeObservableCollection[0]);
        Assert.AreEqual(12, rangeObservableCollection[1]);
        Assert.AreEqual(13, rangeObservableCollection[2]);
        Assert.AreEqual(14, rangeObservableCollection[3]);
        Assert.AreEqual(15, rangeObservableCollection[4]);
        Assert.AreEqual(16, rangeObservableCollection[5]);
        Assert.AreEqual(17, rangeObservableCollection[6]);
        Assert.AreEqual(18, rangeObservableCollection[7]);
        Assert.AreEqual(19, rangeObservableCollection[8]);
        Assert.AreEqual(1, collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedReplaceRange()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10));
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            Assert.AreEqual(NotifyCollectionChangedAction.Replace, e.Action);
            Assert.IsNotNull(e.OldItems);
            Assert.AreEqual(4, e.OldItems?.Count);
            Assert.AreEqual(3, e.OldStartingIndex);
            Assert.IsNotNull(e.NewItems);
            Assert.AreEqual(3, e.NewItems?.Count);
            Assert.AreEqual(3, e.NewStartingIndex);
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(10, rangeObservableCollection.Count);
        rangeObservableCollection.ReplaceRange(3, 4, Enumerable.Range(11, 3).ToList());
        Assert.AreEqual(9, rangeObservableCollection.Count);
        Assert.AreEqual(1, rangeObservableCollection[0]);
        Assert.AreEqual(2, rangeObservableCollection[1]);
        Assert.AreEqual(3, rangeObservableCollection[2]);
        Assert.AreEqual(11, rangeObservableCollection[3]);
        Assert.AreEqual(12, rangeObservableCollection[4]);
        Assert.AreEqual(13, rangeObservableCollection[5]);
        Assert.AreEqual(8, rangeObservableCollection[6]);
        Assert.AreEqual(9, rangeObservableCollection[7]);
        Assert.AreEqual(10, rangeObservableCollection[8]);
        Assert.AreEqual(1, collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedReplaceRangeWithEmptyList()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10));
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, e.Action);
            Assert.IsNotNull(e.OldItems);
            Assert.AreEqual(4, e.OldItems?.Count);
            Assert.AreEqual(3, e.OldStartingIndex);
            Assert.IsNull(e.NewItems);
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(10, rangeObservableCollection.Count);
        rangeObservableCollection.ReplaceRange(3, 4, Enumerable.Range(11, 0).ToList());
        Assert.AreEqual(6, rangeObservableCollection.Count);
        Assert.AreEqual(1, rangeObservableCollection[0]);
        Assert.AreEqual(2, rangeObservableCollection[1]);
        Assert.AreEqual(3, rangeObservableCollection[2]);
        Assert.AreEqual(8, rangeObservableCollection[3]);
        Assert.AreEqual(9, rangeObservableCollection[4]);
        Assert.AreEqual(10, rangeObservableCollection[5]);
        Assert.AreEqual(1, collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void ObservedReset()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10));
        var collectionChanged = 0;
        void collectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++collectionChanged;
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, e.Action);
            Assert.IsNull(e.OldItems);
            Assert.IsNull(e.NewItems);
        }
        rangeObservableCollection.CollectionChanged += collectionChangedHandler;
        Assert.AreEqual(10, rangeObservableCollection.Count);
        rangeObservableCollection.Reset(Enumerable.Range(11, 9));
        Assert.AreEqual(9, rangeObservableCollection.Count);
        Assert.AreEqual(11, rangeObservableCollection[0]);
        Assert.AreEqual(12, rangeObservableCollection[1]);
        Assert.AreEqual(13, rangeObservableCollection[2]);
        Assert.AreEqual(14, rangeObservableCollection[3]);
        Assert.AreEqual(15, rangeObservableCollection[4]);
        Assert.AreEqual(16, rangeObservableCollection[5]);
        Assert.AreEqual(17, rangeObservableCollection[6]);
        Assert.AreEqual(18, rangeObservableCollection[7]);
        Assert.AreEqual(19, rangeObservableCollection[8]);
        Assert.AreEqual(1, collectionChanged);
        rangeObservableCollection.CollectionChanged -= collectionChangedHandler;
    }

    [TestMethod]
    public void RemoveAll()
    {
        var rangeObservableCollection = new RangeObservableCollection<int>(Enumerable.Range(1, 10));
        Assert.AreEqual(10, rangeObservableCollection.Count);
        rangeObservableCollection.RemoveAll(i => i % 3 == 0);
        Assert.AreEqual(7, rangeObservableCollection.Count);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void RemoveRangeWithNullItems() =>
        new RangeObservableCollection<int>(Enumerable.Range(1, 10)).RemoveRange(null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ReplaceAllWithNullItems() =>
        new RangeObservableCollection<int>(Enumerable.Range(1, 10)).ReplaceAll(null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ResetWithNullCollection() =>
        new RangeObservableCollection<int>(Enumerable.Range(1, 10)).Reset(null!);
}
