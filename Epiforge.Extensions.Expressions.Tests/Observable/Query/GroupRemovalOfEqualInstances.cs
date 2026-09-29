namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// An element leaving a group of a grouping or a lookup when the group also holds a distinct element equal to it, which must leave the element that stayed where it was
/// </summary>
[TestClass]
public class GroupRemovalOfEqualInstances
{
    sealed class Interchangeable(string tag, int key) :
        PropertyChangeNotifier
    {
        int key = key;

        public int Key
        {
            get => key;
            set => SetBackedProperty(ref key, in value);
        }

        public string Tag { get; } = tag;

        public override bool Equals(object? obj) =>
            obj is Interchangeable;

        public override int GetHashCode() =>
            0;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnElementChangingGroupLeavesAnEqualElementWhichStayed(bool throughALookup)
    {
        var first = new Interchangeable("first", 0);
        var second = new Interchangeable("second", 0);
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyList(new ObservableRangeCollection<Interchangeable>([first, second]));
        IDisposable query;
        Func<int, IReadOnlyList<Interchangeable>> group;
        if (throughALookup)
        {
            var lookup = sourceQuery.ObserveToLookup(element => element.Key);
            query = lookup;
            group = key => ((IReadOnlyObservableRangeDictionary<int, IObservableGrouping<int, Interchangeable>>)lookup)[key].ToList();
        }
        else
        {
            var grouping = sourceQuery.ObserveGroupBy(element => element.Key);
            query = grouping;
            group = key => grouping.FirstOrDefault(candidate => candidate.Key == key)?.ToList() ?? [];
        }
        second.Key = 1;
        var stayed = group(0).Select(element => element.Tag).ToList();
        var moved = group(1).Select(element => element.Tag).ToList();
        query.Dispose();
        CollectionAssert.AreEqual(new[] { "first" }, stayed, "the group the element left does not hold exactly the element which stayed");
        CollectionAssert.AreEqual(new[] { "second" }, moved, "the group the element joined does not hold exactly the element which moved");
    }
}
