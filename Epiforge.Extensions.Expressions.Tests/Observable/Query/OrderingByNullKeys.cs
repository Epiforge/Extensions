namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// An ordering by more than one key where elements tie on an earlier key by both having none, which must still be ordered by the later keys, as an ordering does where both have the same key
/// </summary>
[TestClass]
public class OrderingByNullKeys
{
    sealed class Ranked(string tag, string? primary, int secondary) :
        PropertyChangeNotifier
    {
        int secondary = secondary;

        public string? Primary { get; } = primary;

        public int Secondary
        {
            get => secondary;
            set => SetBackedProperty(ref secondary, in value);
        }

        public string Tag { get; } = tag;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ElementsWithoutAnEarlierKeyAreOrderedByTheLaterOnes(bool isDescending)
    {
        var first = new Ranked("first", null, 2);
        var second = new Ranked("second", null, 1);
        var third = new Ranked("third", "a", 0);
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyList(new ObservableRangeCollection<Ranked>([first, second, third]));
        using var ordering = sourceQuery.ObserveOrderBy((element => element.Primary!, isDescending), (element => element.Secondary, false));
        var whileBuilt = ordering.Select(element => element.Tag).ToArray();
        first.Secondary = 0;
        var afterChange = ordering.Select(element => element.Tag).ToArray();
        CollectionAssert.AreEqual(isDescending ? new[] { "third", "second", "first" } : new[] { "second", "first", "third" }, whileBuilt, "the elements without an earlier key were not ordered by the later one when the ordering was built");
        CollectionAssert.AreEqual(isDescending ? new[] { "third", "first", "second" } : new[] { "first", "second", "third" }, afterChange, "the elements without an earlier key were not ordered by the later one after it changed");
    }
}
