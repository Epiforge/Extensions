namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// A key selector which throws for an element, while a view is built or afterward, which the readme promises leaves the element out and reports the fault on the view until the selector stops throwing
/// </summary>
[TestClass]
public class KeySelectorFaults
{
    sealed class Keyed(string tag, int key) :
        PropertyChangeNotifier
    {
        int key = key;

        public int Key
        {
            get => key;
            set => SetBackedProperty(ref key, in value);
        }

        public string Tag { get; } = tag;
    }

    [TestMethod]
    [DataRow("grouping")]
    [DataRow("lookup")]
    [DataRow("ordering")]
    [DataRow("subsequent ordering")]
    [DataRow("dictionary")]
    public void AnElementWhoseKeyThrowsIsLeftOutAndTheFaultReportedUntilItStops(string view)
    {
        var first = new Keyed("first", 1);
        var second = new Keyed("second", 0);
        var third = new Keyed("third", 2);
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyList(new ObservableRangeCollection<Keyed>([first, second, third]));
        IDisposable query;
        Func<IReadOnlyList<string>> contents;
        Func<Exception?> fault;
        switch (view)
        {
            case "grouping":
                var grouping = sourceQuery.ObserveGroupBy(element => 10 / element.Key);
                query = grouping;
                fault = () => grouping.OperationFault;
                contents = () => grouping.OrderBy(group => group.Key).SelectMany(group => group).Select(element => element.Tag).ToList();
                break;
            case "lookup":
                var lookup = sourceQuery.ObserveToLookup(element => 10 / element.Key);
                query = lookup;
                fault = () => lookup.OperationFault;
                contents = () => lookup.OrderBy<KeyValuePair<int, IObservableGrouping<int, Keyed>>, int>(entry => entry.Key).SelectMany(entry => entry.Value).Select(element => element.Tag).ToList();
                break;
            case "ordering":
                var ordering = sourceQuery.ObserveOrderBy(element => 10 / element.Key);
                query = ordering;
                fault = () => ordering.OperationFault;
                contents = () => ordering.Select(element => element.Tag).ToList();
                break;
            case "subsequent ordering":
                var subsequentOrdering = sourceQuery.ObserveOrderBy((element => 0, false), (element => 10 / element.Key, false));
                query = subsequentOrdering;
                fault = () => subsequentOrdering.OperationFault;
                contents = () => subsequentOrdering.Select(element => element.Tag).ToList();
                break;
            default:
                var dictionary = sourceQuery.ObserveToDictionary(element => 10 / element.Key, element => element.Tag);
                query = dictionary;
                fault = () => dictionary.OperationFault;
                contents = () => dictionary.OrderBy(entry => entry.Key).Select(entry => entry.Value).ToList();
                break;
        }
        var whileBuilt = contents();
        var faultWhileBuilt = fault();
        second.Key = 5;
        var afterRecovery = contents();
        var faultAfterRecovery = fault();
        third.Key = 0;
        var afterThrowing = contents();
        var faultAfterThrowing = fault();
        third.Key = 2;
        var afterSecondRecovery = contents();
        var faultAfterSecondRecovery = fault();
        query.Dispose();
        CollectionAssert.AreEqual(new[] { "third", "first" }, whileBuilt.ToArray(), "the view built with a throwing key did not leave out exactly that element");
        Assert.IsNotNull(faultWhileBuilt, "the view built with a throwing key did not report the fault");
        CollectionAssert.AreEqual(new[] { "second", "third", "first" }, afterRecovery.ToArray(), "the element whose key stopped throwing did not return to the view");
        Assert.IsNull(faultAfterRecovery, "the fault was still reported after the key stopped throwing");
        CollectionAssert.AreEqual(new[] { "second", "first" }, afterThrowing.ToArray(), "the element whose key began throwing was not left out");
        Assert.IsNotNull(faultAfterThrowing, "the key which began throwing was not reported");
        CollectionAssert.AreEqual(new[] { "second", "third", "first" }, afterSecondRecovery.ToArray(), "the element whose key stopped throwing again did not return to the view");
        Assert.IsNull(faultAfterSecondRecovery, "the fault was still reported after the key stopped throwing again");
    }
}
