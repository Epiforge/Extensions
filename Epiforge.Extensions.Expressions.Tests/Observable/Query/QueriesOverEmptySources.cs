namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Holds what a query built over a source with nothing in it reports as elements arrive, fault, change, leave and are replaced wholesale, and what a source query leaves registered once it is disposed
/// </summary>
[TestClass]
public class QueriesOverEmptySources
{
    static void AssertFaultIsOf(Exception? fault, object element, string query)
    {
        Assert.IsInstanceOfType<EvaluationFaultException>(fault, $"{query}: {fault}");
        var evaluationFault = (EvaluationFaultException)fault;
        Assert.AreSame(element, evaluationFault.Element, query);
        Assert.IsInstanceOfType<DivideByZeroException>(evaluationFault.InnerException, query);
    }

    [TestMethod]
    [DataRow("ReadOnlyList")]
    [DataRow("GenericList")]
    [DataRow("List")]
    [DataRow("GenericEnumerable")]
    [DataRow("Enumerable")]
    public void ASourceQueryLeavesNothingRegisteredOnceDisposed(string kind)
    {
        var log = new SubscriptionLog();
        var items = new RecordedCollection(log);
        var collectionObserver = new CollectionObserver();
        IDisposable query = kind switch
        {
            "ReadOnlyList" => collectionObserver.ObserveReadOnlyList<Recorded>(items),
            "GenericList" => collectionObserver.ObserveList<Recorded>(items),
            "List" => collectionObserver.ObserveList((IList)items),
            "GenericEnumerable" => collectionObserver.ObserveEnumerable<Recorded>(items),
            _ => collectionObserver.ObserveEnumerable((IEnumerable)items)
        };
        Assert.AreEqual(1, log.Outstanding, $"[{string.Join(", ", log.Attachments())}]");
        query.Dispose();
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(1, log.Attachments().Count, $"[{string.Join(", ", log.Attachments())}]");
    }

    [TestMethod]
    public void QueriesBuiltOverAnEmptySourceFollowWhatArrivesFaultsChangesAndLeaves()
    {
        var log = new SubscriptionLog();
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<Recorded>();
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var filter = sourceQuery.ObserveWhere(r => 10 / r.Rank > 1);
        var projection = sourceQuery.ObserveSelect(r => 10 / r.Rank);
        var cast = sourceQuery.ObserveCast<object>();
        var flattening = sourceQuery.ObserveSelectMany(r => new[] { r.Rank, r.Rank * 10 });

        void assertContents(string step, Recorded[] filtered, int[] projected, Recorded[] casted, int[] flattened)
        {
            CollectionAssert.AreEqual(filtered, filter.ToArray(), $"{step}: filter [{string.Join(", ", filter.Select(r => r.Rank))}]");
            Assert.AreEqual(filtered.Length, filter.Count, step);
            CollectionAssert.AreEqual(projected, projection.ToArray(), $"{step}: projection [{string.Join(", ", projection)}]");
            Assert.AreEqual(projected.Length, projection.Count, step);
            CollectionAssert.AreEqual(casted, cast.ToArray(), step);
            Assert.AreEqual(casted.Length, cast.Count, step);
            CollectionAssert.AreEqual(flattened, flattening.ToArray(), $"{step}: flattening [{string.Join(", ", flattening)}]");
            Assert.AreEqual(flattened.Length, flattening.Count, step);
        }

        assertContents("empty", [], [], [], []);
        Assert.IsNull(filter.OperationFault);
        Assert.IsNull(projection.OperationFault);
        var two = new Recorded(log) { Rank = 2 };
        var zero = new Recorded(log) { Rank = 0 };
        var twenty = new Recorded(log) { Rank = 20 };
        source.Add(two);
        assertContents("added two", [two], [5], [two], [2, 20]);
        source.Add(zero);
        assertContents("added zero", [two], [5, 0], [two, zero], [2, 20, 0, 0]);
        AssertFaultIsOf(filter.OperationFault, zero, "filter");
        AssertFaultIsOf(projection.OperationFault, zero, "projection");
        source.Remove(zero);
        assertContents("removed zero", [two], [5], [two], [2, 20]);
        Assert.IsNull(filter.OperationFault);
        Assert.IsNull(projection.OperationFault);
        source.Clear();
        assertContents("cleared", [], [], [], []);
        source.Add(twenty);
        assertContents("added twenty", [], [0], [twenty], [20, 200]);
        twenty.Rank = 4;
        assertContents("twenty became four", [twenty], [2], [twenty], [4, 40]);
        source.Reset([two, zero]);
        assertContents("reset", [two], [5, 0], [two, zero], [2, 20, 0, 0]);
        AssertFaultIsOf(filter.OperationFault, zero, "filter after reset");
        AssertFaultIsOf(projection.OperationFault, zero, "projection after reset");
        filter.Dispose();
        projection.Dispose();
        cast.Dispose();
        flattening.Dispose();
        sourceQuery.Dispose();
        Assert.AreEqual(0, log.Outstanding);
    }
}
