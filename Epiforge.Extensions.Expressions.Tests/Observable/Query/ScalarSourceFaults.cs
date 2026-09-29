namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// A scalar query over a query whose fault appears or clears without its contents changing, which must report the fault while it stands and drop it once it clears, as counting and aggregating already do
/// </summary>
[TestClass]
public class ScalarSourceFaults
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
    [DataRow("first")]
    [DataRow("first or default")]
    [DataRow("last")]
    [DataRow("last or default")]
    [DataRow("element at")]
    [DataRow("element at or default")]
    [DataRow("single")]
    [DataRow("single or default")]
    [DataRow("element at a position computed from the count")]
    public void AnElementOfAFilterReportsTheFilterFaultingOnAnElementItLeavesOut(string scalar)
    {
        var member = new Keyed("member", 2);
        var outsider = new Keyed("outsider", 3);
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyList(new ObservableRangeCollection<Keyed>([member, outsider]));
        using var filter = sourceQuery.ObserveWhere(element => 10 / (element.Key - 1) > 6);
        using var query = scalar switch
        {
            "first" => filter.ObserveFirst(),
            "first or default" => filter.ObserveFirstOrDefault(),
            "last" => filter.ObserveLast(),
            "last or default" => filter.ObserveLastOrDefault(),
            "element at" => filter.ObserveElementAt(0),
            "element at or default" => filter.ObserveElementAtOrDefault(0),
            "single" => filter.ObserveSingle(),
            "single or default" => filter.ObserveSingleOrDefault(),
            _ => filter.ObserveElementAt(count => count - 1)
        };
        outsider.Key = 1;
        var whileFaulted = query.Evaluation;
        outsider.Key = 3;
        var afterRecovery = query.Evaluation;
        Assert.IsNotNull(whileFaulted.Fault, "the filter's fault on an element it leaves out was not reported");
        Assert.IsNull(afterRecovery.Fault, "the filter's fault was still reported after it cleared");
        Assert.AreSame(member, afterRecovery.Result, "the element was not reported once the fault cleared");
    }

    [TestMethod]
    public void ARankInAFilterReportsTheFilterFaultingOnAnElementItLeavesOut()
    {
        var member = new Keyed("member", 2);
        var outsider = new Keyed("outsider", 3);
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyList(new ObservableRangeCollection<Keyed>([member, outsider]));
        using var filter = sourceQuery.ObserveWhere(element => 10 / (element.Key - 1) > 6);
        using var query = filter.ObserveRank(member, element => element.Key, false);
        outsider.Key = 1;
        var whileFaulted = query.Evaluation;
        outsider.Key = 3;
        var afterRecovery = query.Evaluation;
        Assert.IsNotNull(whileFaulted.Fault, "the filter's fault on an element it leaves out was not reported");
        Assert.IsNull(afterRecovery.Fault, "the filter's fault was still reported after it cleared");
        Assert.AreEqual((0, 1), afterRecovery.Result, "the rank was not reported once the fault cleared");
    }

    [TestMethod]
    public void AnyOfADictionaryFilterReportsTheFilterFaultingOnAKeyItLeavesOut()
    {
        var member = new Keyed("member", 2);
        var outsider = new Keyed("outsider", 3);
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyDictionary(new ObservableDictionary<string, Keyed>(new Dictionary<string, Keyed> { ["member"] = member, ["outsider"] = outsider }));
        using var filter = sourceQuery.ObserveWhere((key, value) => 10 / (value.Key - 1) > 6);
        using var query = filter.ObserveAny();
        outsider.Key = 1;
        var whileFaulted = query.Evaluation;
        outsider.Key = 3;
        var afterRecovery = query.Evaluation;
        Assert.IsNotNull(whileFaulted.Fault, "the filter's fault on a key it leaves out was not reported");
        Assert.IsNull(afterRecovery.Fault, "the filter's fault was still reported after it cleared");
        Assert.IsTrue(afterRecovery.Result, "the member was not reported once the fault cleared");
    }

    [TestMethod]
    [DataRow("value for")]
    [DataRow("value for or default")]
    public void AValueOfADictionaryFilterReportsTheFilterFaultingOnAKeyItLeavesOut(string scalar)
    {
        var member = new Keyed("member", 2);
        var outsider = new Keyed("outsider", 3);
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyDictionary(new ObservableDictionary<string, Keyed>(new Dictionary<string, Keyed> { ["member"] = member, ["outsider"] = outsider }));
        using var filter = sourceQuery.ObserveWhere((key, value) => 10 / (value.Key - 1) > 6);
        using var query = scalar == "value for" ? filter.ObserveValueFor("member") : filter.ObserveValueForOrDefault("member");
        outsider.Key = 1;
        var whileFaulted = query.Evaluation;
        outsider.Key = 3;
        var afterRecovery = query.Evaluation;
        Assert.IsNotNull(whileFaulted.Fault, "the filter's fault on a key it leaves out was not reported");
        Assert.IsNull(afterRecovery.Fault, "the filter's fault was still reported after it cleared");
        Assert.AreSame(member, afterRecovery.Result, "the value was not reported once the fault cleared");
    }

    [TestMethod]
    [DataRow("sum")]
    [DataRow("average")]
    public void AnAggregateOfAProjectionReportsTheProjectionFaulting(string aggregate)
    {
        var first = new Keyed("first", 2);
        var second = new Keyed("second", 3);
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyList(new ObservableRangeCollection<Keyed>([first, second]));
        using var projection = sourceQuery.ObserveSelect(element => 10 / (element.Key - 1));
        using var query = aggregate == "sum" ? projection.ObserveSum() : projection.ObserveAverage();
        second.Key = 1;
        var whileFaulted = query.Evaluation;
        second.Key = 3;
        var afterRecovery = query.Evaluation;
        Assert.IsNotNull(whileFaulted.Fault, "the projection's fault was not reported");
        Assert.IsNull(afterRecovery.Fault, "the projection's fault was still reported after it cleared");
        Assert.AreEqual(aggregate == "sum" ? 15 : 7, afterRecovery.Result, "the aggregate was not recomputed once the fault cleared");
    }

    [TestMethod]
    [DataRow("count")]
    [DataRow("sum")]
    [DataRow("average")]
    public void AnAggregateOfADictionaryReportsItsLambdaFaulting(string aggregate)
    {
        var first = new Keyed("first", 2);
        var second = new Keyed("second", 3);
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyDictionary(new ObservableDictionary<string, Keyed>(new Dictionary<string, Keyed> { ["first"] = first, ["second"] = second }));
        using var query = aggregate switch
        {
            "count" => sourceQuery.ObserveCount((key, value) => 10 / (value.Key - 1) > 6),
            "sum" => sourceQuery.ObserveSum((key, value) => 10 / (value.Key - 1)),
            _ => sourceQuery.ObserveAverage((key, value) => 10 / (value.Key - 1))
        };
        second.Key = 1;
        var whileFaulted = query.Evaluation;
        second.Key = 3;
        var afterRecovery = query.Evaluation;
        Assert.IsNotNull(whileFaulted.Fault, "the lambda's fault was not reported");
        Assert.IsNull(afterRecovery.Fault, "the lambda's fault was still reported after it cleared");
        Assert.AreEqual(aggregate switch { "count" => 1, "sum" => 15, _ => 7 }, afterRecovery.Result, "the aggregate was not recomputed once the fault cleared");
    }
}
