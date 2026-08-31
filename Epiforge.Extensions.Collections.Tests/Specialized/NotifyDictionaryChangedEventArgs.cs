namespace Epiforge.Extensions.Collections.Tests.Specialized;

[TestClass]
public class NotifyDictionaryChangedEventArgs
{
    static KeyValuePair<string, int> Pair(string key, int value) =>
        new(key, value);

    [TestMethod]
    public void AddOfManyItems()
    {
        var items = new[] { Pair("a", 1), Pair("b", 2) };
        var e = new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Add, items);
        Assert.AreEqual(NotifyDictionaryChangedAction.Add, e.Action);
        CollectionAssert.AreEqual(items, e.NewItems.ToArray());
        Assert.AreEqual(0, e.OldItems.Count);
    }

    [TestMethod]
    public void AddOfOneItem()
    {
        var e = new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Add, Pair("key", 3));
        Assert.AreEqual(NotifyDictionaryChangedAction.Add, e.Action);
        Assert.AreEqual(Pair("key", 3), e.NewItems.Single());
        Assert.AreEqual(0, e.OldItems.Count);
    }

    [TestMethod]
    public void AddOfOneKeyAndValue()
    {
        var e = new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Add, "key", 3);
        Assert.AreEqual(NotifyDictionaryChangedAction.Add, e.Action);
        Assert.AreEqual(Pair("key", 3), e.NewItems.Single());
        Assert.AreEqual(0, e.OldItems.Count);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void NonAddRemoveWithItems() =>
        new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Reset, new[] { Pair("key", 3) });

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void NonReplaceWithNewAndOldItems() =>
        new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Add, new[] { Pair("a", 1) }, new[] { Pair("b", 2) });

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void NonResetWithNoItems() =>
        new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Add);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullChangedItems() =>
        new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Add, (IEnumerable<KeyValuePair<string, int>>)null!);

    [TestMethod]
    public void RemoveOfManyItems()
    {
        var items = new[] { Pair("a", 1), Pair("b", 2) };
        var e = new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Remove, items);
        Assert.AreEqual(NotifyDictionaryChangedAction.Remove, e.Action);
        CollectionAssert.AreEqual(items, e.OldItems.ToArray());
        Assert.AreEqual(0, e.NewItems.Count);
    }

    [TestMethod]
    public void RemoveOfOneKeyAndValue()
    {
        var e = new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Remove, "key", 3);
        Assert.AreEqual(NotifyDictionaryChangedAction.Remove, e.Action);
        Assert.AreEqual(Pair("key", 3), e.OldItems.Single());
        Assert.AreEqual(0, e.NewItems.Count);
    }

    [TestMethod]
    public void ReplaceOfManyItems()
    {
        var newItems = new[] { Pair("a", 10), Pair("b", 20) };
        var oldItems = new[] { Pair("a", 1), Pair("b", 2) };
        var e = new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Replace, newItems, oldItems);
        Assert.AreEqual(NotifyDictionaryChangedAction.Replace, e.Action);
        CollectionAssert.AreEqual(newItems, e.NewItems.ToArray());
        CollectionAssert.AreEqual(oldItems, e.OldItems.ToArray());
    }

    [TestMethod]
    public void ReplaceOfOneKeyAndValues()
    {
        var e = new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Replace, "key", 5, 3);
        Assert.AreEqual(NotifyDictionaryChangedAction.Replace, e.Action);
        Assert.AreEqual(Pair("key", 5), e.NewItems.Single());
        Assert.AreEqual(Pair("key", 3), e.OldItems.Single());
    }

    [TestMethod]
    public void ReplaceOfOneNewAndOldItem()
    {
        var e = new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Replace, Pair("key", 5), Pair("key", 3));
        Assert.AreEqual(NotifyDictionaryChangedAction.Replace, e.Action);
        Assert.AreEqual(Pair("key", 5), e.NewItems.Single());
        Assert.AreEqual(Pair("key", 3), e.OldItems.Single());
    }

    [TestMethod]
    public void ResetHasNoItems()
    {
        var e = new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Reset);
        Assert.AreEqual(NotifyDictionaryChangedAction.Reset, e.Action);
        Assert.AreEqual(0, e.NewItems.Count);
        Assert.AreEqual(0, e.OldItems.Count);
    }

    [TestMethod]
    public void ToStringDescribesEachAction()
    {
        var older = Pair("key", 3);
        var newer = Pair("key", 5);
        Assert.AreEqual($"added {newer}", new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Add, newer).ToString());
        Assert.AreEqual($"removed {older}", new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Remove, older).ToString());
        Assert.AreEqual($"replaced {older} with {newer}", new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Replace, newer, older).ToString());
        Assert.AreEqual("reset", new NotifyDictionaryChangedEventArgs<string, int>(NotifyDictionaryChangedAction.Reset).ToString());
    }
}
