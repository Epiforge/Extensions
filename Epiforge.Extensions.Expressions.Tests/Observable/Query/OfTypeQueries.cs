namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Holds what a type filter reports of its elements, its faults and its changes, and what it subscribes to, so that how it is built can change without what it reports changing
/// </summary>
[TestClass]
public class OfTypeQueries
{
    sealed class Distinguished(SubscriptionLog log) :
        Recorded(log);

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
    public void ATypeFilterFollowsEveryKindOfChangeWithItsAnnouncements()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<object?>([1, "two", null, 4]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var ofType = sourceQuery.ObserveOfType<int>();
        var announcements = new List<string>();
        ((INotifyCollectionChanged)ofType).CollectionChanged += (sender, e) => announcements.Add(Describe(e));

        void assertState(string step, int[] contents, params string[] announced)
        {
            CollectionAssert.AreEqual(contents, ofType.ToList(), $"{step}: enumerated [{string.Join(",", ofType)}]");
            Assert.AreEqual(contents.Length, ofType.Count, step);
            CollectionAssert.AreEqual(contents, Enumerable.Range(0, ofType.Count).Select(i => ofType[i]).ToArray(), $"{step}: indexed");
            var copy = new int[ofType.Count];
            ((ICollection<int>)ofType).CopyTo(copy, 0);
            CollectionAssert.AreEqual(contents, copy, $"{step}: copied");
            Assert.AreEqual("none", Describe(ofType.OperationFault), step);
            CollectionAssert.AreEqual(announced, announcements, $"{step}: {string.Join(" | ", announcements)}");
            announcements.Clear();
        }

        assertState("initial", [1, 4]);
        source.Add(5);
        assertState("added five", [1, 4, 5], "Add new@2[5] old@-1[]");
        source.Add("six");
        assertState("added six", [1, 4, 5]);
        source.AddRange([7, "eight", 9]);
        assertState("added seven, eight and nine", [1, 4, 5, 7, 9], "Add new@3[7,9] old@-1[]");
        source.RemoveAt(1);
        assertState("removed two", [1, 4, 5, 7, 9]);
        source[0] = 10;
        assertState("replaced one with ten", [10, 4, 5, 7, 9], "Replace new@0[10] old@0[1]");
        source[1] = 11;
        assertState("replaced null with eleven", [10, 11, 4, 5, 7, 9], "Add new@1[11] old@-1[]");
        source[2] = "four";
        assertState("replaced four with text", [10, 11, 5, 7, 9], "Remove new@-1[] old@2[4]");
        source.Move(0, 3);
        assertState("moved ten past five", [11, 5, 10, 7, 9], "Move new@2[10] old@0[10]");
        source.Move(1, 4);
        assertState("moved eleven among text", [11, 5, 10, 7, 9]);
        source.Move(source.IndexOf("six"), 0);
        assertState("moved six to the front", [11, 5, 10, 7, 9]);
        source.RemoveRange(0, 3);
        assertState("removed six, eleven and four as text", [10, 7, 9], "Remove new@-1[] old@0[11,5]");
        source.MoveRange(0, 2, 2);
        assertState("moved ten and four as text", [7, 10, 9], "Move new@1[10] old@0[10]");
        source.InsertRange(1, ["x", 12, 13, "y"]);
        assertState("inserted x, twelve, thirteen and y", [7, 12, 13, 10, 9], "Add new@1[12,13] old@-1[]");
        source.RemoveRange(1, 4);
        assertState("removed them", [7, 10, 9], "Remove new@-1[] old@1[12,13]");
        source.Reset([1, null, "z", 2]);
        assertState("reset", [1, 2], "Reset new@-1[] old@-1[]");
        source.Clear();
        assertState("cleared", [], "Reset new@-1[] old@-1[]");
        source.Add("p");
        assertState("added text to nothing", []);
        source[0] = "q";
        assertState("replaced text with text", []);
        source.Reset(["r", null]);
        assertState("reset without a match", [], "Reset new@-1[] old@-1[]");
        source.Insert(1, 20);
        assertState("inserted twenty", [20], "Add new@0[20] old@-1[]");
        ofType.Dispose();
        sourceQuery.Dispose();
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ATypeFilterSubscribesToNothingItTests()
    {
        var log = new SubscriptionLog();
        var plain = new Recorded(log) { Rank = 1 };
        var distinguished = new Distinguished(log) { Rank = 2 };
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<Recorded>([plain, distinguished]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var everyone = sourceQuery.ObserveOfType<Recorded>();
        var onlyDistinguished = sourceQuery.ObserveOfType<Distinguished>();
        var announcements = 0;
        ((INotifyCollectionChanged)everyone).CollectionChanged += (sender, e) => ++announcements;
        ((INotifyCollectionChanged)onlyDistinguished).CollectionChanged += (sender, e) => ++announcements;
        CollectionAssert.AreEqual(new Recorded[] { plain, distinguished }, everyone.ToArray());
        CollectionAssert.AreEqual(new Distinguished[] { distinguished }, onlyDistinguished.ToArray());
        Assert.AreEqual(0, log.Attachments().Count, $"[{string.Join(", ", log.Attachments())}]");
        plain.Rank = 3;
        distinguished.Rank = 4;
        Assert.AreEqual(0, announcements);
        everyone.Dispose();
        onlyDistinguished.Dispose();
        sourceQuery.Dispose();
        Assert.AreEqual(0, log.Attachments().Count);
    }

    [TestMethod]
    public void ATypeFilterAnnouncesNothingWhenAnElementsContentsChange()
    {
        var inner = new ObservableCollection<int> { 1 };
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<ObservableCollection<int>>([inner]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var ofType = sourceQuery.ObserveOfType<IEnumerable<int>>();
        var announcements = new List<string>();
        ((INotifyCollectionChanged)ofType).CollectionChanged += (sender, e) => announcements.Add(Describe(e));
        inner.Add(2);
        Assert.AreEqual(0, announcements.Count, string.Join(" | ", announcements));
        Assert.AreSame(inner, ofType[0]);
        ofType.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void ATypeFilterReportsItsSourcesFault()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<object?>([0, 5, "x"]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var filter = sourceQuery.ObserveWhere(o => !(o is int) || 10 / (int)o! > 1);
        var ofType = filter.ObserveOfType<int>();
        var faultAnnouncements = 0;
        ((INotifyPropertyChanged)ofType).PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(IObservableCollectionQuery<int>.OperationFault))
                ++faultAnnouncements;
        };
        CollectionAssert.AreEqual(new[] { 5 }, ofType.ToArray());
        Assert.AreEqual("DivideByZeroException at 0", Describe(ofType.OperationFault));
        source.Add(0);
        Assert.AreEqual("[DivideByZeroException at 0; DivideByZeroException at 0]", Describe(ofType.OperationFault));
        Assert.AreEqual(1, faultAnnouncements);
        source.RemoveAt(0);
        Assert.AreEqual("DivideByZeroException at 0", Describe(ofType.OperationFault));
        Assert.AreEqual(2, faultAnnouncements);
        source.RemoveAt(2);
        Assert.AreEqual("none", Describe(ofType.OperationFault));
        Assert.AreEqual(3, faultAnnouncements);
        ofType.Dispose();
        filter.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void ATypeFilterTestsTypesAsTheLanguageDoes()
    {
        var collectionObserver = new CollectionObserver();
        var integers = new ObservableRangeCollection<int>([1, 2]);
        var integersQuery = collectionObserver.ObserveReadOnlyList(integers);
        var widened = integersQuery.ObserveOfType<long>();
        var boxed = integersQuery.ObserveOfType<object>();
        var nullable = integersQuery.ObserveOfType<int?>();
        Assert.AreEqual(0, widened.Count);
        Assert.IsNull(widened.OperationFault);
        CollectionAssert.AreEqual(new object[] { 1, 2 }, boxed.ToArray());
        CollectionAssert.AreEqual(new int?[] { 1, 2 }, nullable.ToArray());
        var objects = new ObservableRangeCollection<object?>([null, "a", null]);
        var objectsQuery = collectionObserver.ObserveReadOnlyList(objects);
        var anything = objectsQuery.ObserveOfType<object>();
        var text = objectsQuery.ObserveOfType<string?>();
        CollectionAssert.AreEqual(new object[] { "a" }, anything.ToArray());
        CollectionAssert.AreEqual(new string[] { "a" }, text.ToArray());
        widened.Dispose();
        boxed.Dispose();
        nullable.Dispose();
        integersQuery.Dispose();
        anything.Dispose();
        text.Dispose();
        objectsQuery.Dispose();
    }

    [TestMethod]
    public void TypeFiltersForOneTypeOutliveEachOther()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<object>(["a", 1]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var first = sourceQuery.ObserveOfType<string>();
        var second = sourceQuery.ObserveOfType<string>();
        first.Dispose();
        source.Add("b");
        Assert.IsTrue(((IDisposalStatus)first).IsDisposed);
        Assert.IsFalse(((IDisposalStatus)second).IsDisposed);
        CollectionAssert.AreEqual(new[] { "a", "b" }, second.ToArray());
        second.Dispose();
        Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        sourceQuery.Dispose();
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
