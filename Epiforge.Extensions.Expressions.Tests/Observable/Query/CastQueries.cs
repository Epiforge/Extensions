namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Holds what a cast query reports of its elements, its faults and its changes, and what it subscribes to, so that how it is built can change without what it reports changing
/// </summary>
[TestClass]
public class CastQueries
{
    static int[] Enumerate(IEnumerable<int> query)
    {
        var read = new List<int>();
        foreach (var element in query)
            read.Add(element);
        return [.. read];
    }

    static int[] ReadAcross(IEnumerable<int> query, Action change)
    {
        var read = new List<int>();
        using (var enumerator = query.GetEnumerator())
        {
            Assert.IsTrue(enumerator.MoveNext());
            read.Add(enumerator.Current);
            change();
            while (enumerator.MoveNext())
                read.Add(enumerator.Current);
        }
        return [.. read];
    }

    static int[] ReadRest(IEnumerator<int> enumerator)
    {
        var read = new List<int>();
        while (enumerator.MoveNext())
            read.Add(enumerator.Current);
        return [.. read];
    }

    static string Describe(Exception? fault) =>
        fault switch
        {
            null => "none",
            EvaluationFaultException evaluationFault => $"{evaluationFault.InnerException?.GetType().Name} at {evaluationFault.Element ?? "null"}",
            AggregateException aggregate => $"[{string.Join("; ", aggregate.InnerExceptions.Select(Describe))}]",
            _ => fault.GetType().Name
        };

    static string Describe(NotifyCollectionChangedEventArgs e) =>
        $"{e.Action} new@{e.NewStartingIndex}[{(e.NewItems is null ? string.Empty : string.Join(",", e.NewItems.Cast<object?>().Select(item => item ?? "null")))}] old@{e.OldStartingIndex}[{(e.OldItems is null ? string.Empty : string.Join(",", e.OldItems.Cast<object?>().Select(item => item ?? "null")))}]";

    [TestMethod]
    public void ACastFollowsEveryKindOfChangeWithItsFaultsAndAnnouncements()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<object?>([1, "two", null, 4]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var cast = sourceQuery.ObserveCast<int>();
        var announcements = new List<string>();
        ((INotifyCollectionChanged)cast).CollectionChanged += (sender, e) => announcements.Add(Describe(e));

        void assertState(string step, int[] contents, string fault, params string[] announced)
        {
            CollectionAssert.AreEqual(contents, cast.ToList(), $"{step}: enumerated [{string.Join(",", cast)}]");
            Assert.AreEqual(contents.Length, cast.Count, step);
            CollectionAssert.AreEqual(contents, Enumerable.Range(0, cast.Count).Select(i => cast[i]).ToArray(), $"{step}: indexed");
            var copy = new int[cast.Count];
            ((ICollection<int>)cast).CopyTo(copy, 0);
            CollectionAssert.AreEqual(contents, copy, $"{step}: copied");
            Assert.AreEqual(fault, Describe(cast.OperationFault), step);
            CollectionAssert.AreEqual(announced, announcements, $"{step}: {string.Join(" | ", announcements)}");
            announcements.Clear();
        }

        assertState("initial", [1, 0, 0, 4], "[InvalidCastException at two; NullReferenceException at null]");
        source.Add(5);
        assertState("added five", [1, 0, 0, 4, 5], "[InvalidCastException at two; NullReferenceException at null]", "Add new@4[5] old@-1[]");
        source.Add("six");
        assertState("added six", [1, 0, 0, 4, 5, 0], "[InvalidCastException at two; NullReferenceException at null; InvalidCastException at six]", "Add new@5[0] old@-1[]");
        source.AddRange([7, 8]);
        assertState("added seven and eight", [1, 0, 0, 4, 5, 0, 7, 8], "[InvalidCastException at two; NullReferenceException at null; InvalidCastException at six]", "Add new@6[7,8] old@-1[]");
        source.RemoveAt(1);
        assertState("removed two", [1, 0, 4, 5, 0, 7, 8], "[NullReferenceException at null; InvalidCastException at six]", "Remove new@-1[] old@1[0]");
        source[0] = 10;
        assertState("replaced one", [10, 0, 4, 5, 0, 7, 8], "[NullReferenceException at null; InvalidCastException at six]", "Replace new@0[10] old@0[1]");
        source.Move(0, 2);
        assertState("moved ten", [0, 4, 10, 5, 0, 7, 8], "[NullReferenceException at null; InvalidCastException at six]", "Move new@2[10] old@0[10]");
        source.RemoveRange(0, 2);
        assertState("removed null and four", [10, 5, 0, 7, 8], "InvalidCastException at six", "Remove new@-1[] old@0[0,4]");
        source.MoveRange(0, 2, 2);
        assertState("moved ten and five", [0, 7, 10, 5, 8], "InvalidCastException at six", "Move new@2[10,5] old@0[10,5]");
        source.Reset([1, null]);
        assertState("reset", [1, 0], "NullReferenceException at null", "Reset new@-1[] old@-1[]");
        source.Clear();
        assertState("cleared", [], "none", "Reset new@-1[] old@-1[]");
        cast.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void ACastSubscribesToNothingItCasts()
    {
        var log = new SubscriptionLog();
        var element = new Recorded(log) { Rank = 1 };
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<Recorded>([element]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var cast = sourceQuery.ObserveCast<object>();
        var announcements = 0;
        ((INotifyCollectionChanged)cast).CollectionChanged += (sender, e) => ++announcements;
        Assert.AreEqual(0, log.Attachments().Count, $"[{string.Join(", ", log.Attachments())}]");
        element.Rank = 2;
        Assert.AreEqual(0, announcements);
        cast.Dispose();
        sourceQuery.Dispose();
        Assert.AreEqual(0, log.Attachments().Count);
    }

    [TestMethod]
    public void ACastAnnouncesNothingWhenAnElementsContentsChange()
    {
        var inner = new ObservableCollection<int> { 1 };
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<ObservableCollection<int>>([inner]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var cast = sourceQuery.ObserveCast<IEnumerable<int>>();
        var announcements = new List<string>();
        ((INotifyCollectionChanged)cast).CollectionChanged += (sender, e) => announcements.Add(Describe(e));
        inner.Add(2);
        Assert.AreEqual(0, announcements.Count, string.Join(" | ", announcements));
        Assert.AreSame(inner, cast[0]);
        cast.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void ACastReportsItsSourcesFaultBeforeItsOwn()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<object?>([0, 5, "x"]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var filter = sourceQuery.ObserveWhere(o => !(o is int) || 10 / (int)o! > 1);
        var cast = filter.ObserveCast<int>();
        CollectionAssert.AreEqual(new[] { 5, 0 }, cast.ToArray());
        Assert.AreEqual("[DivideByZeroException at 0; InvalidCastException at x]", Describe(cast.OperationFault));
        source.Add("y");
        Assert.AreEqual("[DivideByZeroException at 0; InvalidCastException at x; InvalidCastException at y]", Describe(cast.OperationFault));
        source.RemoveAt(0);
        Assert.AreEqual("[InvalidCastException at x; InvalidCastException at y]", Describe(cast.OperationFault));
        source.Add(0);
        Assert.AreEqual("[DivideByZeroException at 0; InvalidCastException at x; InvalidCastException at y]", Describe(cast.OperationFault));
        cast.Dispose();
        filter.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void ACastReportsEachOccurrenceOfAnElementWhichCannotBeCast()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<object?>(["x", 1, "x"]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var cast = sourceQuery.ObserveCast<int>();
        Assert.AreEqual("[InvalidCastException at x; InvalidCastException at x]", Describe(cast.OperationFault));
        source.RemoveAt(0);
        Assert.AreEqual("InvalidCastException at x", Describe(cast.OperationFault));
        source.RemoveAt(1);
        Assert.AreEqual("none", Describe(cast.OperationFault));
        cast.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void ACastOfValueTypesUnboxesAsTheLanguageDoes()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<int>([1, 2]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var widened = sourceQuery.ObserveCast<long>();
        var boxed = sourceQuery.ObserveCast<object>();
        var nullable = sourceQuery.ObserveCast<int?>();
        CollectionAssert.AreEqual(new long[] { 0, 0 }, widened.ToArray());
        Assert.AreEqual("[InvalidCastException at 1; InvalidCastException at 2]", Describe(widened.OperationFault));
        CollectionAssert.AreEqual(new object[] { 1, 2 }, boxed.ToArray());
        Assert.IsNull(boxed.OperationFault);
        CollectionAssert.AreEqual(new int?[] { 1, 2 }, nullable.ToArray());
        Assert.IsNull(nullable.OperationFault);
        widened.Dispose();
        boxed.Dispose();
        nullable.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void AnEnumerationUnderWayReadsTheResultsAsTheyWereWhenItBeganThroughEveryKindOfChange()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<object?>([1, 2, 3, 4]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var cast = sourceQuery.ObserveCast<int>();

        void assertAcross(string step, Action change, int[] before, int[] after)
        {
            CollectionAssert.AreEqual(before, ReadAcross(cast, change), $"{step}: read across");
            CollectionAssert.AreEqual(after, Enumerate(cast), $"{step}: enumerated after");
            CollectionAssert.AreEqual(after, Enumerable.Range(0, cast.Count).Select(i => cast[i]).ToArray(), $"{step}: indexed after");
        }

        assertAcross("added", () => source.Add(5), [1, 2, 3, 4], [1, 2, 3, 4, 5]);
        assertAcross("removed", () => source.RemoveAt(0), [1, 2, 3, 4, 5], [2, 3, 4, 5]);
        assertAcross("replaced", () => source[0] = 6, [2, 3, 4, 5], [6, 3, 4, 5]);
        assertAcross("moved", () => source.Move(0, 3), [6, 3, 4, 5], [3, 4, 5, 6]);
        assertAcross("added one which cannot be cast", () => source.Add("seven"), [3, 4, 5, 6], [3, 4, 5, 6, 0]);
        assertAcross("reset", () => source.Reset([7, 8]), [3, 4, 5, 6, 0], [7, 8]);
        assertAcross("cleared", source.Clear, [7, 8], []);
        cast.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void EnumerationsKeepWhatTheyBeganWithWhetherOrNotTheyAreDisposed()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<object?>([1, 2]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var cast = sourceQuery.ObserveCast<int>();
        CollectionAssert.AreEqual(new[] { 1, 2 }, Enumerate(cast));
        source.Add(3);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, Enumerate(cast), "after an enumeration which has ended");
        var undisposed = cast.GetEnumerator();
        Assert.IsTrue(undisposed.MoveNext());
        Assert.AreEqual(1, undisposed.Current);
        source.Add(4);
        var overlapping = cast.GetEnumerator();
        Assert.IsTrue(overlapping.MoveNext());
        Assert.AreEqual(1, overlapping.Current);
        source.RemoveAt(0);
        source[0] = 5;
        CollectionAssert.AreEqual(new[] { 5, 3, 4 }, Enumerate(cast), "while two enumerations are under way");
        CollectionAssert.AreEqual(new[] { 2, 3, 4 }, ReadRest(overlapping), "the overlapping enumeration");
        overlapping.Dispose();
        source.Add(6);
        CollectionAssert.AreEqual(new[] { 5, 3, 4, 6 }, Enumerate(cast), "after the overlapping enumeration was disposed");
        CollectionAssert.AreEqual(new[] { 2, 3 }, ReadRest(undisposed), "the enumeration never disposed");
        source.Add(7);
        CollectionAssert.AreEqual(new[] { 5, 3, 4, 6, 7 }, Enumerate(cast), "after the enumeration never disposed was read to its end");
        cast.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void AnAggregateOverACastFollowsEveryKindOfChange()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<object?>([1, 2, 3]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var cast = sourceQuery.ObserveCast<int>();
        var sum = cast.ObserveAggregate(() => 0, (accumulate, element) => accumulate + element, accumulate => accumulate);

        void assertSum(string step, int expected)
        {
            Assert.IsNull(sum.Evaluation.Fault, step);
            Assert.AreEqual(expected, sum.Evaluation.Result, step);
            Assert.AreEqual(expected, Enumerate(cast).Sum(), $"{step}: enumerated");
        }

        assertSum("initial", 6);
        source.Add(4);
        assertSum("added", 10);
        source.RemoveAt(0);
        assertSum("removed", 9);
        source[0] = 10;
        assertSum("replaced", 17);
        source.Move(0, 2);
        assertSum("moved", 17);
        source.Reset([5, 6]);
        assertSum("reset", 11);
        source.Clear();
        assertSum("cleared", 0);
        sum.Dispose();
        cast.Dispose();
        sourceQuery.Dispose();
    }
}
