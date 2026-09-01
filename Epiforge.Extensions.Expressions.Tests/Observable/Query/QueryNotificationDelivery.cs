namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class QueryNotificationDelivery
{
    static ObservableRangeCollection<TestPerson> People() =>
        new(new[] { new TestPerson("A"), new TestPerson("BB"), new TestPerson("CCC"), new TestPerson("DDDD") });

    [TestMethod]
    public void ASecondSubscriberStillReceivesWhenTheFirstUnsubscribes()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length > 2);
        var first = 0;
        var second = 0;
        void countFirst(object? sender, NotifyCollectionChangedEventArgs e) => ++first;
        void countSecond(object? sender, NotifyCollectionChangedEventArgs e) => ++second;
        whereQuery.CollectionChanged += countFirst;
        whereQuery.CollectionChanged += countSecond;
        source[0].Name = "AAAAA";
        Assert.AreEqual(1, first);
        Assert.AreEqual(1, second);
        whereQuery.CollectionChanged -= countFirst;
        source[1].Name = "BBBBB";
        Assert.AreEqual(1, first);
        Assert.AreEqual(2, second);
        whereQuery.CollectionChanged -= countSecond;
    }

    [TestMethod]
    public void ASubscriberAfterAChangeReceivesEveryLaterChange()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length > 2);
        Assert.AreEqual(2, whereQuery.Count);
        source[0].Name = "AAAAA";
        Assert.AreEqual(3, whereQuery.Count);
        var received = 0;
        void count(object? sender, NotifyCollectionChangedEventArgs e) => ++received;
        whereQuery.CollectionChanged += count;
        source[1].Name = "BBBBB";
        Assert.AreEqual(1, received);
        Assert.AreEqual(4, whereQuery.Count);
        whereQuery.CollectionChanged -= count;
    }

    [TestMethod]
    public void ASubscriberWhoLeavesAndReturnsReceivesAgain()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length > 2);
        var received = 0;
        void count(object? sender, NotifyCollectionChangedEventArgs e) => ++received;
        whereQuery.CollectionChanged += count;
        source[0].Name = "AAAAA";
        Assert.AreEqual(1, received);
        whereQuery.CollectionChanged -= count;
        source[1].Name = "BBBBB";
        Assert.AreEqual(1, received);
        whereQuery.CollectionChanged += count;
        source[2].Name = "C";
        Assert.AreEqual(2, received);
        whereQuery.CollectionChanged -= count;
    }

    [TestMethod]
    public void ChainedQueriesStillSeeChangesWithNobodySubscribedToEither()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length > 2);
        using var countQuery = whereQuery.ObserveCount();
        Assert.AreEqual(2, countQuery.Evaluation.Result);
        source[0].Name = "AAAAA";
        Assert.AreEqual(3, countQuery.Evaluation.Result);
        source[3].Name = "D";
        Assert.AreEqual(2, countQuery.Evaluation.Result);
    }

    [TestMethod]
    public void TheCountOfAnUnobservedQueryStillTracksTheSource()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length > 2);
        Assert.AreEqual(2, whereQuery.Count);
        source[0].Name = "AAAAA";
        Assert.AreEqual(3, whereQuery.Count);
        Assert.AreEqual("AAAAA", whereQuery[0].Name);
        source[0].Name = "A";
        Assert.AreEqual(2, whereQuery.Count);
    }

    [TestMethod]
    public void TheCountPropertyOfAnUnobservedQueryIsStillAnnounced()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length > 2);
        var counts = new List<int>();
        void propertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IReadOnlyCollection<TestPerson>.Count))
                counts.Add(whereQuery.Count);
        }
        ((INotifyPropertyChanged)whereQuery).PropertyChanged += propertyChanged;
        source[0].Name = "AAAAA";
        source[3].Name = "D";
        ((INotifyPropertyChanged)whereQuery).PropertyChanged -= propertyChanged;
        Assert.IsTrue(new[] { 3, 2 }.SequenceEqual(counts), string.Join(", ", counts));
    }
}
