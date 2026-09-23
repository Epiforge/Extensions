namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Covers the order in which a query's change reaches the queries derived from it and the handlers subscribed to it: every derived query has taken the change before any handler hears of it, and a query built while the change is being delivered does not take it again
/// </summary>
/// <remarks>
/// A handler subscribed to a query before a query was derived from it used to hear of a change before the derived query had taken it, and so read the derived query stale. The second test guards what a list of subscribers walked while it may grow must not lose: a query built from a handler during delivery starts from the state after the change, so delivering the same change to it again would apply it twice
/// </remarks>
[TestClass]
public class DerivedQueryNotificationOrder
{
    static ObservableRangeCollection<TestPerson> People() =>
        new(new[] { new TestPerson("A"), new TestPerson("BB"), new TestPerson("CCC"), new TestPerson("DDDD") });

    [TestMethod]
    public void AHandlerSubscribedBeforeADerivedQueryWasBuiltSeesItAlreadyUpdated()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var countsSeen = new List<int>();
        IObservableCollectionQuery<TestPerson>? whereQuery = null;
        void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => countsSeen.Add(whereQuery!.Count);
        sourceQuery.CollectionChanged += collectionChanged;
        using (whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length > 2))
        {
            Assert.AreEqual(2, whereQuery.Count);
            source.Add(new TestPerson("EEEEE"));
            sourceQuery.CollectionChanged -= collectionChanged;
        }
        Assert.IsTrue(new[] { 3 }.SequenceEqual(countsSeen), string.Join(", ", countsSeen));
    }

    [TestMethod]
    public void AQueryBuiltWhileAChangeIsDeliveredDoesNotTakeItAgain()
    {
        var source = People();
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length > 2);
        IObservableCollectionQuery<TestPerson>? builtDuringDelivery = null;
        void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => builtDuringDelivery ??= sourceQuery.ObserveWhere(person => person.Name!.Length > 3);
        whereQuery.CollectionChanged += collectionChanged;
        source.Add(new TestPerson("EEEEE"));
        whereQuery.CollectionChanged -= collectionChanged;
        Assert.IsNotNull(builtDuringDelivery);
        using (builtDuringDelivery)
            Assert.AreEqual("DDDD,EEEEE", string.Join(",", builtDuringDelivery.Select(person => person.Name)));
    }
}
