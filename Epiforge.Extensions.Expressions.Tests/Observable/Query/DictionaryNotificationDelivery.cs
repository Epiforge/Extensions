namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryNotificationDelivery
{
    static ObservableDictionary<int, TestPerson> People() =>
        new(new Dictionary<int, TestPerson>
        {
            { 0, new TestPerson("A") },
            { 1, new TestPerson("BB") },
            { 2, new TestPerson("CCC") },
            { 3, new TestPerson("DDDD") }
        });

    [TestMethod]
    public void ASecondSubscriberStillReceivesWhenTheFirstUnsubscribes()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var whereQuery = sourceQuery.ObserveWhere((key, person) => person.Name!.Length > 2);
        var first = 0;
        var second = 0;
        void countFirst(object? sender, NotifyDictionaryChangedEventArgs<int, TestPerson> e) => ++first;
        void countSecond(object? sender, NotifyDictionaryChangedEventArgs<int, TestPerson> e) => ++second;
        ((INotifyDictionaryChanged<int, TestPerson>)whereQuery).DictionaryChanged += countFirst;
        ((INotifyDictionaryChanged<int, TestPerson>)whereQuery).DictionaryChanged += countSecond;
        source[0].Name = "AAAAA";
        Assert.AreEqual(1, first);
        Assert.AreEqual(1, second);
        ((INotifyDictionaryChanged<int, TestPerson>)whereQuery).DictionaryChanged -= countFirst;
        source[1].Name = "BBBBB";
        Assert.AreEqual(1, first);
        Assert.AreEqual(2, second);
        ((INotifyDictionaryChanged<int, TestPerson>)whereQuery).DictionaryChanged -= countSecond;
    }

    [TestMethod]
    public void ASubscriberAfterAChangeReceivesEveryLaterChange()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var whereQuery = sourceQuery.ObserveWhere((key, person) => person.Name!.Length > 2);
        Assert.AreEqual(2, whereQuery.Count);
        source[0].Name = "AAAAA";
        Assert.AreEqual(3, whereQuery.Count);
        var received = 0;
        void count(object? sender, NotifyDictionaryChangedEventArgs<int, TestPerson> e) => ++received;
        ((INotifyDictionaryChanged<int, TestPerson>)whereQuery).DictionaryChanged += count;
        source[1].Name = "BBBBB";
        Assert.AreEqual(1, received);
        Assert.AreEqual(4, whereQuery.Count);
        ((INotifyDictionaryChanged<int, TestPerson>)whereQuery).DictionaryChanged -= count;
    }

    [TestMethod]
    public void ASubscriberWhoLeavesAndReturnsReceivesAgain()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var whereQuery = sourceQuery.ObserveWhere((key, person) => person.Name!.Length > 2);
        var received = 0;
        void count(object? sender, NotifyDictionaryChangedEventArgs<int, TestPerson> e) => ++received;
        ((INotifyDictionaryChanged<int, TestPerson>)whereQuery).DictionaryChanged += count;
        source[0].Name = "AAAAA";
        Assert.AreEqual(1, received);
        ((INotifyDictionaryChanged<int, TestPerson>)whereQuery).DictionaryChanged -= count;
        source[1].Name = "BBBBB";
        Assert.AreEqual(1, received);
        ((INotifyDictionaryChanged<int, TestPerson>)whereQuery).DictionaryChanged += count;
        source[2].Name = "C";
        Assert.AreEqual(2, received);
        ((INotifyDictionaryChanged<int, TestPerson>)whereQuery).DictionaryChanged -= count;
    }

    [TestMethod]
    public void ChainedQueriesStillSeeChangesWithNobodySubscribedToEither()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var whereQuery = sourceQuery.ObserveWhere((key, person) => person.Name!.Length > 2);
        using var countQuery = whereQuery.ObserveCount();
        Assert.AreEqual(2, countQuery.Evaluation.Result);
        source[0].Name = "AAAAA";
        Assert.AreEqual(3, countQuery.Evaluation.Result);
        source[3].Name = "D";
        Assert.AreEqual(2, countQuery.Evaluation.Result);
    }

    [TestMethod]
    public void TheBoxedDictionaryChangedEventIsStillDelivered()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var whereQuery = sourceQuery.ObserveWhere((key, person) => person.Name!.Length > 2);
        var received = 0;
        void count(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) => ++received;
        ((INotifyDictionaryChanged)whereQuery).DictionaryChanged += count;
        source[0].Name = "AAAAA";
        Assert.AreEqual(1, received);
        source[3].Name = "D";
        Assert.AreEqual(2, received);
        ((INotifyDictionaryChanged)whereQuery).DictionaryChanged -= count;
    }

    [TestMethod]
    public void TheCollectionChangedEventIsStillDelivered()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var whereQuery = sourceQuery.ObserveWhere((key, person) => person.Name!.Length > 2);
        var received = 0;
        void count(object? sender, NotifyCollectionChangedEventArgs e) => ++received;
        ((INotifyCollectionChanged)whereQuery).CollectionChanged += count;
        source[0].Name = "AAAAA";
        Assert.AreEqual(1, received);
        source[3].Name = "D";
        Assert.AreEqual(2, received);
        ((INotifyCollectionChanged)whereQuery).CollectionChanged -= count;
    }

    [TestMethod]
    public void TheCountOfAnUnobservedQueryStillTracksTheSource()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var whereQuery = sourceQuery.ObserveWhere((key, person) => person.Name!.Length > 2);
        Assert.AreEqual(2, whereQuery.Count);
        source[0].Name = "AAAAA";
        Assert.AreEqual(3, whereQuery.Count);
        Assert.AreEqual("AAAAA", whereQuery[0].Name);
        source[0].Name = "A";
        Assert.AreEqual(2, whereQuery.Count);
        Assert.IsFalse(whereQuery.ContainsKey(0));
    }

    [TestMethod]
    public void TheCountPropertyOfAnUnobservedQueryIsStillAnnounced()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var whereQuery = sourceQuery.ObserveWhere((key, person) => person.Name!.Length > 2);
        var counts = new List<int>();
        void propertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IReadOnlyCollection<KeyValuePair<int, TestPerson>>.Count))
                counts.Add(whereQuery.Count);
        }
        ((INotifyPropertyChanged)whereQuery).PropertyChanged += propertyChanged;
        source[0].Name = "AAAAA";
        source[3].Name = "D";
        ((INotifyPropertyChanged)whereQuery).PropertyChanged -= propertyChanged;
        Assert.IsTrue(new[] { 3, 2 }.SequenceEqual(counts), string.Join(", ", counts));
    }
}
