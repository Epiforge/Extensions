namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class QueryNotificationDeferral
{
    static List<string> Record(IObservableCollectionQuery<TestPerson> query, Action mutate)
    {
        var sequence = new List<string>();
        void changing(object? sender, PropertyChangingEventArgs e) => sequence.Add($"changing:{e.PropertyName}");
        void changed(object? sender, PropertyChangedEventArgs e) => sequence.Add($"changed:{e.PropertyName}");
        void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => sequence.Add($"collection:{e.Action}");
        ((INotifyPropertyChanging)query).PropertyChanging += changing;
        ((INotifyPropertyChanged)query).PropertyChanged += changed;
        query.CollectionChanged += collectionChanged;
        try
        {
            mutate();
        }
        finally
        {
            ((INotifyPropertyChanging)query).PropertyChanging -= changing;
            ((INotifyPropertyChanged)query).PropertyChanged -= changed;
            query.CollectionChanged -= collectionChanged;
        }
        return sequence;
    }

    [TestMethod]
    public void AMembershipFlipDefersThreeNotificationsInOrder()
    {
        var alice = new TestPerson("A");
        var source = new ObservableRangeCollection<TestPerson>(new[] { alice, new TestPerson("BBB") });
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length > 2);
        Assert.AreEqual(1, whereQuery.Count);
        var sequence = Record(whereQuery, () => alice.Name = "AAAA");
        Assert.IsTrue(new[] { "changing:Count", "changed:Count", "collection:Add" }.SequenceEqual(sequence), string.Join(", ", sequence));
        Assert.AreEqual(2, whereQuery.Count);
    }

    [TestMethod]
    public void AnElementAppearingTwiceIsAnnouncedTwiceInOrder()
    {
        var alice = new TestPerson("A");
        var source = new ObservableRangeCollection<TestPerson>(new[] { alice, new TestPerson("BBB"), alice });
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length > 2);
        Assert.AreEqual(1, whereQuery.Count);
        var sequence = Record(whereQuery, () => alice.Name = "AAAA");
        Assert.IsTrue(new[] { "changing:Count", "changed:Count", "collection:Add", "changing:Count", "changed:Count", "collection:Add" }.SequenceEqual(sequence), string.Join(", ", sequence));
        Assert.AreEqual(3, whereQuery.Count);
    }

    [TestMethod]
    public void AnElementAppearingTwiceLeavesInTwoAnnouncements()
    {
        var alice = new TestPerson("AAAA");
        var source = new ObservableRangeCollection<TestPerson>(new[] { alice, new TestPerson("BBB"), alice });
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length > 2);
        Assert.AreEqual(3, whereQuery.Count);
        var sequence = Record(whereQuery, () => alice.Name = "A");
        Assert.IsTrue(new[] { "changing:Count", "changed:Count", "collection:Remove", "changing:Count", "changed:Count", "collection:Remove" }.SequenceEqual(sequence), string.Join(", ", sequence));
        Assert.AreEqual(1, whereQuery.Count);
    }

    [TestMethod]
    public void AChangeWhichAltersNoMembershipDefersNothing()
    {
        var alice = new TestPerson("AAAA");
        var source = new ObservableRangeCollection<TestPerson>(new[] { alice, new TestPerson("BBB") });
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length > 2);
        Assert.AreEqual(2, whereQuery.Count);
        var sequence = Record(whereQuery, () => alice.Name = "ZZZZ");
        Assert.AreEqual(0, sequence.Count, string.Join(", ", sequence));
        Assert.AreEqual(2, whereQuery.Count);
    }

    [TestMethod]
    public void EachNotificationCarriesTheCountAsOfTheChangeItDescribes()
    {
        var alice = new TestPerson("A");
        var source = new ObservableRangeCollection<TestPerson>(new[] { alice, new TestPerson("BBB"), alice });
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length > 2);
        var countsSeen = new List<int>();
        void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => countsSeen.Add(whereQuery.Count);
        whereQuery.CollectionChanged += collectionChanged;
        alice.Name = "AAAA";
        whereQuery.CollectionChanged -= collectionChanged;
        Assert.IsTrue(new[] { 2, 3 }.SequenceEqual(countsSeen), string.Join(", ", countsSeen));
    }
}
