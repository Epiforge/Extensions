namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// A null element whose evaluation faults, which must stop being reported once the element leaves the source, as the fault of any other element does
/// </summary>
[TestClass]
public class NullElementFaults
{
    [TestMethod]
    [DataRow("filter")]
    [DataRow("projection")]
    [DataRow("flattening")]
    [DataRow("grouping")]
    [DataRow("lookup")]
    [DataRow("ordering")]
    [DataRow("dictionary")]
    public void AFaultOnANullElementClearsOnceItLeaves(string view)
    {
        var source = new ObservableRangeCollection<string?>(["a", null]);
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyList(source);
        IDisposable query;
        Func<Exception?> fault;
        switch (view)
        {
            case "filter":
                var filter = sourceQuery.ObserveWhere(element => element!.Length > 0);
                query = filter;
                fault = () => filter.OperationFault;
                break;
            case "projection":
                var projection = sourceQuery.ObserveSelect(element => element!.Length);
                query = projection;
                fault = () => projection.OperationFault;
                break;
            case "flattening":
                var flattening = sourceQuery.ObserveSelectMany(element => element!.ToCharArray());
                query = flattening;
                fault = () => flattening.OperationFault;
                break;
            case "grouping":
                var grouping = sourceQuery.ObserveGroupBy(element => element!.Length);
                query = grouping;
                fault = () => grouping.OperationFault;
                break;
            case "lookup":
                var lookup = sourceQuery.ObserveToLookup(element => element!.Length);
                query = lookup;
                fault = () => lookup.OperationFault;
                break;
            case "ordering":
                var ordering = sourceQuery.ObserveOrderBy(element => element!.Length);
                query = ordering;
                fault = () => ordering.OperationFault;
                break;
            default:
                var dictionary = sourceQuery.ObserveToDictionary(element => element!.Length, element => element);
                query = dictionary;
                fault = () => dictionary.OperationFault;
                break;
        }
        var whileHeld = fault();
        source.RemoveAt(1);
        var afterLeaving = fault();
        query.Dispose();
        Assert.IsNotNull(whileHeld, "the fault on the null element was not reported");
        Assert.IsNull(afterLeaving, "the fault on the null element was still reported after it left the source");
    }
}
