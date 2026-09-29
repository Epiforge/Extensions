namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// A fault in a query which a view is built over, which the readme promises is reported on the view, since the query holding the throwing lambda is not always one the caller keeps
/// </summary>
[TestClass]
public class FaultsThroughChains
{
    sealed class ImmediateSynchronizationContext :
        SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) =>
            d(state);

        public override void Send(SendOrPostCallback d, object? state) =>
            d(state);
    }

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

    static Exception? FaultOf(object query) =>
        query switch
        {
            IObservableCollectionQuery<Keyed> collection => collection.OperationFault,
            IObservableCollectionQuery<int> collection => collection.OperationFault,
            IObservableCollectionQuery<object> collection => collection.OperationFault,
            IObservableCollectionQuery<IObservableGrouping<int, Keyed>> collection => collection.OperationFault,
            IObservableDictionaryQuery<string, Keyed> dictionary => dictionary.OperationFault,
            IObservableDictionaryQuery<string, int> dictionary => dictionary.OperationFault,
            IObservableDictionaryQuery<string, object> dictionary => dictionary.OperationFault,
            IObservableDictionaryQuery<int, IObservableGrouping<int, Keyed>> dictionary => dictionary.OperationFault,
            IObservableScalarQuery<int> scalar => scalar.Evaluation.Fault,
            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };

    [TestMethod]
    [DataRow("filter")]
    [DataRow("projection")]
    [DataRow("flattening")]
    [DataRow("ordering")]
    [DataRow("grouping")]
    [DataRow("lookup")]
    [DataRow("dictionary")]
    [DataRow("cast")]
    [DataRow("type filter")]
    [DataRow("distinct")]
    [DataRow("skip")]
    [DataRow("take")]
    [DataRow("slice")]
    [DataRow("append")]
    [DataRow("prepend")]
    [DataRow("concatenation, first")]
    [DataRow("concatenation, second")]
    [DataRow("individual changes")]
    [DataRow("sync root")]
    [DataRow("synchronization callback")]
    [DataRow("synchronization context")]
    [DataRow("maximum of a projection")]
    public void AViewOfACollectionQueryReportsThatQuerysFault(string view)
    {
        var first = new Keyed("first", 2);
        var second = new Keyed("second", 3);
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyList(new ObservableRangeCollection<Keyed>([first, second]));
        using var other = observer.ObserveReadOnlyList(new ObservableRangeCollection<Keyed>([new Keyed("other", 4)]));
        using var filter = sourceQuery.ObserveWhere(element => 10 / (element.Key - 1) > 0);
        var query = (IDisposable)(view switch
        {
            "filter" => filter.ObserveWhere(element => element.Key >= 0),
            "projection" => filter.ObserveSelect(element => element.Key),
            "flattening" => filter.ObserveSelectMany(element => new[] { element.Key }),
            "ordering" => filter.ObserveOrderBy(element => element.Key),
            "grouping" => filter.ObserveGroupBy(element => element.Key % 2),
            "lookup" => filter.ObserveToLookup(element => element.Key % 2),
            "dictionary" => filter.ObserveToDictionary(element => element.Tag),
            "cast" => filter.ObserveCast<object>(),
            "type filter" => filter.ObserveOfType<Keyed>(),
            "distinct" => filter.ObserveDistinct(),
            "skip" => filter.ObserveSkip(0),
            "take" => filter.ObserveTake(10),
            "slice" => filter.ObserveSlice(0..10),
            "append" => filter.ObserveAppend(new Keyed("appended", 9)),
            "prepend" => filter.ObservePrepend(new Keyed("prepended", 9)),
            "concatenation, first" => filter.ObserveConcat(other),
            "concatenation, second" => other.ObserveConcat(filter),
            "individual changes" => filter.ObserveIndividualChanges(),
            "sync root" => filter.ObserveUsingSyncRoot(new object()),
            "synchronization callback" => filter.ObserveUsingSynchronizationCallback(new object(), (collection, context, accessMethod, writeAccess) => accessMethod()),
            "synchronization context" => filter.ObserveUsingSynchronizationContext(new ImmediateSynchronizationContext()),
            "maximum of a projection" => filter.ObserveSelect(element => element.Key).ObserveMax(),
            _ => throw new ArgumentOutOfRangeException(nameof(view))
        });
        var faultWhileClean = FaultOf(query);
        second.Key = 1;
        var faultWhileThrowing = FaultOf(query);
        second.Key = 3;
        var faultAfterRecovery = FaultOf(query);
        query.Dispose();
        Assert.IsNull(faultWhileClean, "the view reported a fault before anything threw");
        Assert.IsNotNull(faultWhileThrowing, "the view did not report the fault of the query it is built over");
        Assert.IsNull(faultAfterRecovery, "the view still reported the fault after it cleared");
    }

    [TestMethod]
    [DataRow("filter")]
    [DataRow("projection")]
    [DataRow("collection")]
    [DataRow("cast")]
    [DataRow("type filter")]
    [DataRow("concurrently")]
    [DataRow("synchronization context")]
    public void AViewOfADictionaryQueryReportsThatQuerysFault(string view)
    {
        var first = new Keyed("first", 2);
        var second = new Keyed("second", 3);
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyDictionary(new ObservableDictionary<string, Keyed>(new Dictionary<string, Keyed> { ["first"] = first, ["second"] = second }));
        using var filter = sourceQuery.ObserveWhere((key, value) => 10 / (value.Key - 1) > 0);
        var query = (IDisposable)(view switch
        {
            "filter" => filter.ObserveWhere((key, value) => value.Key >= 0),
            "projection" => filter.ObserveSelect((key, value) => key, (key, value) => value.Key),
            "collection" => filter.ObserveToCollection((key, value) => value.Key),
            "cast" => filter.ObserveCast<object>(),
            "type filter" => filter.ObserveOfType<Keyed>(),
            "concurrently" => filter.ObserveConcurrently(),
            "synchronization context" => filter.ObserveUsingSynchronizationContext(new ImmediateSynchronizationContext()),
            _ => throw new ArgumentOutOfRangeException(nameof(view))
        });
        var faultWhileClean = FaultOf(query);
        second.Key = 1;
        var faultWhileThrowing = FaultOf(query);
        second.Key = 3;
        var faultAfterRecovery = FaultOf(query);
        query.Dispose();
        Assert.IsNull(faultWhileClean, "the view reported a fault before anything threw");
        Assert.IsNotNull(faultWhileThrowing, "the view did not report the fault of the query it is built over");
        Assert.IsNull(faultAfterRecovery, "the view still reported the fault after it cleared");
    }
}
