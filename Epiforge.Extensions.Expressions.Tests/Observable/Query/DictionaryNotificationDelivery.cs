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
    public void ASelectQuerySubscriberWhoLeavesAndReturnsReceivesAgain()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var selectQuery = sourceQuery.ObserveSelect((key, person) => key, (key, person) => person.Name!);
        var received = 0;
        void count(object? sender, NotifyDictionaryChangedEventArgs<int, string> e) => ++received;
        ((INotifyDictionaryChanged<int, string>)selectQuery).DictionaryChanged += count;
        source[0].Name = "AAAAA";
        Assert.AreEqual(1, received);
        ((INotifyDictionaryChanged<int, string>)selectQuery).DictionaryChanged -= count;
        source[1].Name = "BBBBB";
        Assert.AreEqual(1, received);
        ((INotifyDictionaryChanged<int, string>)selectQuery).DictionaryChanged += count;
        source[2].Name = "CCCCC";
        Assert.AreEqual(2, received);
        ((INotifyDictionaryChanged<int, string>)selectQuery).DictionaryChanged -= count;
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
    public void ChainedSelectQueriesStillSeeChangesWithNobodySubscribedToEither()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var selectQuery = sourceQuery.ObserveSelect((key, person) => key, (key, person) => person.Name!.Length);
        using var whereQuery = selectQuery.ObserveWhere((key, length) => length > 2);
        Assert.AreEqual(2, whereQuery.Count);
        source[0].Name = "AAAAA";
        Assert.AreEqual(3, whereQuery.Count);
        source[3].Name = "D";
        Assert.AreEqual(2, whereQuery.Count);
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
    public void TheBoxedDictionaryChangedEventOfASelectQueryIsStillDelivered()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var selectQuery = sourceQuery.ObserveSelect((key, person) => key, (key, person) => person.Name!);
        var received = 0;
        void count(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) => ++received;
        ((INotifyDictionaryChanged)selectQuery).DictionaryChanged += count;
        source[0].Name = "AAAAA";
        Assert.AreEqual(1, received);
        source[3].Name = "D";
        Assert.AreEqual(2, received);
        ((INotifyDictionaryChanged)selectQuery).DictionaryChanged -= count;
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
    public void TheCollectionChangedEventOfASelectQueryIsStillDelivered()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var selectQuery = sourceQuery.ObserveSelect((key, person) => key, (key, person) => person.Name!);
        var received = 0;
        void count(object? sender, NotifyCollectionChangedEventArgs e) => ++received;
        ((INotifyCollectionChanged)selectQuery).CollectionChanged += count;
        source[0].Name = "AAAAA";
        Assert.AreEqual(1, received);
        source[3].Name = "D";
        Assert.AreEqual(2, received);
        ((INotifyCollectionChanged)selectQuery).CollectionChanged -= count;
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

    [TestMethod]
    public void TheEnumerationKeysAndValuesOfAnUnobservedQueryAreNeverStale()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var whereQuery = sourceQuery.ObserveWhere((key, person) => person.Name!.Length > 2);
        Assert.IsTrue(new[] { 2, 3 }.SequenceEqual(whereQuery.Keys.OrderBy(key => key)));
        Assert.IsTrue(new[] { "CCC", "DDDD" }.SequenceEqual(whereQuery.Values.Select(person => person.Name).OrderBy(name => name)));
        Assert.AreEqual(2, whereQuery.Count());
        source[0].Name = "AAAAA";
        Assert.IsTrue(new[] { 0, 2, 3 }.SequenceEqual(whereQuery.Keys.OrderBy(key => key)));
        Assert.IsTrue(new[] { "AAAAA", "CCC", "DDDD" }.SequenceEqual(whereQuery.Values.Select(person => person.Name).OrderBy(name => name)));
        Assert.AreEqual(3, whereQuery.Count());
        source[3].Name = "D";
        Assert.IsTrue(new[] { 0, 2 }.SequenceEqual(whereQuery.Keys.OrderBy(key => key)));
        Assert.IsTrue(new[] { "AAAAA", "CCC" }.SequenceEqual(whereQuery.Values.Select(person => person.Name).OrderBy(name => name)));
        Assert.AreEqual(2, whereQuery.Count());
    }

    [TestMethod]
    public void TheEnumerationKeysAndValuesOfAnUnobservedSelectQueryAreNeverStale()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var selectQuery = sourceQuery.ObserveSelect((key, person) => key, (key, person) => person.Name!);
        Assert.IsTrue(new[] { 0, 1, 2, 3 }.SequenceEqual(selectQuery.Keys.OrderBy(key => key)));
        Assert.IsTrue(new[] { "A", "BB", "CCC", "DDDD" }.SequenceEqual(selectQuery.Values.OrderBy(name => name)));
        Assert.AreEqual(4, selectQuery.Count());
        source[0].Name = "AAAAA";
        Assert.IsTrue(new[] { "AAAAA", "BB", "CCC", "DDDD" }.SequenceEqual(selectQuery.Values.OrderBy(name => name)));
        Assert.IsTrue(new[] { 0, 1, 2, 3 }.SequenceEqual(selectQuery.Keys.OrderBy(key => key)));
        source[3].Name = "D";
        Assert.IsTrue(new[] { "AAAAA", "BB", "CCC", "D" }.SequenceEqual(selectQuery.Values.OrderBy(name => name)));
        Assert.AreEqual(4, selectQuery.Count());
    }

    [TestMethod]
    public void TheProjectionOfAnUnobservedSelectQueryStillTracksTheSource()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        using var selectQuery = sourceQuery.ObserveSelect((key, person) => key, (key, person) => person.Name!);
        Assert.AreEqual(4, selectQuery.Count);
        Assert.AreEqual("A", selectQuery[0]);
        source[0].Name = "AAAAA";
        Assert.AreEqual("AAAAA", selectQuery[0]);
        source[3].Name = "D";
        Assert.AreEqual("D", selectQuery[3]);
    }
}
