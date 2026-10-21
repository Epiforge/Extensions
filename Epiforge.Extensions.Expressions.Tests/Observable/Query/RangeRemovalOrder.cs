namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class RangeRemovalOrder
{
    [TestMethod]
    [DataRow("filter keeping all")]
    [DataRow("filter keeping some")]
    [DataRow("projection")]
    public void ARangeRemovalAnnouncesTheRemovedElementsInTheirOrder(string query)
    {
        var source = new ObservableRangeCollection<int>([1, 2, 3, 4, 5, 6]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using var observed = query switch
            {
                "filter keeping all" => sourceQuery.ObserveWhere(number => number > 0),
                "filter keeping some" => sourceQuery.ObserveWhere(number => number % 2 == 0),
                _ => sourceQuery.ObserveSelect(number => number * 10)
            };
            int[] expected = query switch
            {
                "filter keeping all" => [2, 3, 4, 5],
                "filter keeping some" => [2, 4],
                _ => [20, 30, 40, 50]
            };
            IList? announced = null;
            void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
                announced = e.OldItems;
            observed.CollectionChanged += collectionChanged;
            source.RemoveRange(1, 4);
            observed.CollectionChanged -= collectionChanged;
            Assert.IsNotNull(announced);
            CollectionAssert.AreEqual(expected, announced.Cast<int>().ToArray());
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
