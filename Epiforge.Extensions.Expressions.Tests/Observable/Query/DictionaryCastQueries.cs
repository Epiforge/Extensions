namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Holds what a dictionary cast reports of its values, its faults and its changes, and what it subscribes to, so that how it is built can change without what it reports changing
/// </summary>
[TestClass]
public class DictionaryCastQueries
{
    static string Describe(Exception? fault)
    {
        static IEnumerable<string> flatten(Exception exception) =>
            exception switch
            {
                AggregateException aggregate => aggregate.InnerExceptions.SelectMany(flatten),
                EvaluationFaultException evaluationFault => [$"{evaluationFault.InnerException?.GetType().Name} at {evaluationFault.Element ?? "null"}"],
                _ => [exception.GetType().Name]
            };
        if (fault is null)
            return "none";
        var described = flatten(fault).OrderBy(description => description, StringComparer.Ordinal).ToList();
        return described.Count == 1 ? described[0] : $"[{string.Join("; ", described)}]";
    }

    static string Describe<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs) =>
        string.Join(",", keyValuePairs.Select(keyValuePair => $"{keyValuePair.Key}={(object?)keyValuePair.Value ?? "null"}"));

    static string Describe<TKey, TValue>(NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        $"{e.Action} new[{Describe(e.NewItems)}] old[{Describe(e.OldItems)}]";

    static string Describe(NotifyCollectionChangedEventArgs e) =>
        $"collection {e.Action} new[{(e.NewItems is null ? string.Empty : string.Join(",", e.NewItems.Cast<object?>()))}] old[{(e.OldItems is null ? string.Empty : string.Join(",", e.OldItems.Cast<object?>()))}]";

    static void ListenTo<TValue>(IObservableDictionaryQuery<string, TValue> query, List<string> announcements)
    {
        ((INotifyCollectionChanged)query).CollectionChanged += (sender, e) => announcements.Add(Describe(e));
        ((INotifyDictionaryChanged)query).DictionaryChanged += (sender, e) => announcements.Add($"boxed {Describe(e)}");
        ((INotifyDictionaryChanged<string, TValue>)query).DictionaryChanged += (sender, e) => announcements.Add(Describe(e));
    }

    static string Sorted<TValue>(IObservableDictionaryQuery<string, TValue> query) =>
        Describe(query.OrderBy(keyValuePair => keyValuePair.Key, StringComparer.Ordinal));

    [TestMethod]
    public void ADictionaryCastFollowsEveryKindOfChangeWithItsFaultsAndAnnouncements()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableDictionary<string, object?>(new Dictionary<string, object?> { ["a"] = 1, ["b"] = "two", ["c"] = null, ["d"] = 4 });
        var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        var cast = sourceQuery.ObserveCast<int>();
        var announcements = new List<string>();
        ListenTo(cast, announcements);

        void assertState(string step, string contents, string fault, params string[] announced)
        {
            Assert.AreEqual(contents, Sorted(cast), $"{step}: enumerated");
            Assert.AreEqual(contents, Describe(cast.Keys.OrderBy(key => key, StringComparer.Ordinal).Select(key => new KeyValuePair<string, int>(key, cast[key]))), $"{step}: indexed by keys");
            Assert.AreEqual(contents, Describe(cast.Keys.OrderBy(key => key, StringComparer.Ordinal).Zip(cast.Keys.OrderBy(key => key, StringComparer.Ordinal).Select(key => cast.TryGetValue(key, out var value) ? value : -1), (key, value) => new KeyValuePair<string, int>(key, value))), $"{step}: tried by keys");
            var copy = new KeyValuePair<string, int>[cast.Count];
            ((ICollection<KeyValuePair<string, int>>)cast).CopyTo(copy, 0);
            Assert.AreEqual(contents, Describe(copy.OrderBy(keyValuePair => keyValuePair.Key, StringComparer.Ordinal)), $"{step}: copied");
            CollectionAssert.AreEquivalent(cast.Select(keyValuePair => keyValuePair.Value).ToArray(), cast.Values.ToArray(), $"{step}: values");
            Assert.AreEqual(fault, Describe(cast.OperationFault), step);
            CollectionAssert.AreEqual(announced, announcements, $"{step}: {string.Join(" | ", announcements)}");
            announcements.Clear();
        }

        assertState("initial", "a=1,d=4", "[InvalidCastException at b; NullReferenceException at c]");
        source.Add("e", 5);
        assertState("added e five", "a=1,d=4,e=5", "[InvalidCastException at b; NullReferenceException at c]", "collection Add new[[e, 5]] old[]", "boxed Add new[e=5] old[]", "Add new[e=5] old[]");
        source.Add("f", "six");
        assertState("added f six", "a=1,d=4,e=5", "[InvalidCastException at b; InvalidCastException at f; NullReferenceException at c]");
        source.AddRange([new("g", 7), new("h", "eight"), new("i", 9)]);
        assertState("added g, h and i", "a=1,d=4,e=5,g=7,i=9", "[InvalidCastException at b; InvalidCastException at f; InvalidCastException at h; NullReferenceException at c]", "collection Add new[[g, 7]] old[]", "boxed Add new[g=7] old[]", "Add new[g=7] old[]", "collection Add new[[i, 9]] old[]", "boxed Add new[i=9] old[]", "Add new[i=9] old[]");
        source.Remove("b");
        assertState("removed b", "a=1,d=4,e=5,g=7,i=9", "[InvalidCastException at f; InvalidCastException at h; NullReferenceException at c]");
        source["a"] = 10;
        assertState("replaced a with ten", "a=10,d=4,e=5,g=7,i=9", "[InvalidCastException at f; InvalidCastException at h; NullReferenceException at c]", "collection Remove new[] old[[a, 1]]", "boxed Remove new[] old[a=1]", "Remove new[] old[a=1]", "collection Add new[[a, 10]] old[]", "boxed Add new[a=10] old[]", "Add new[a=10] old[]");
        source["c"] = 11;
        assertState("replaced c with eleven", "a=10,c=11,d=4,e=5,g=7,i=9", "[InvalidCastException at f; InvalidCastException at h]", "collection Add new[[c, 11]] old[]", "boxed Add new[c=11] old[]", "Add new[c=11] old[]");
        source["d"] = "four";
        assertState("replaced d with text", "a=10,c=11,e=5,g=7,i=9", "[InvalidCastException at d; InvalidCastException at f; InvalidCastException at h]", "collection Remove new[] old[[d, 4]]", "boxed Remove new[] old[d=4]", "Remove new[] old[d=4]");
        source["f"] = "six again";
        assertState("replaced f with other text", "a=10,c=11,e=5,g=7,i=9", "[InvalidCastException at d; InvalidCastException at f; InvalidCastException at h]");
        source["h"] = 8;
        assertState("replaced h with eight", "a=10,c=11,e=5,g=7,h=8,i=9", "[InvalidCastException at d; InvalidCastException at f]", "collection Add new[[h, 8]] old[]", "boxed Add new[h=8] old[]", "Add new[h=8] old[]");
        source.ReplaceRange([new("a", 20), new("d", 21)]);
        assertState("replaced a and d in one range", "a=20,c=11,d=21,e=5,g=7,h=8,i=9", "InvalidCastException at f", "collection Remove new[] old[[a, 10]]", "boxed Remove new[] old[a=10]", "Remove new[] old[a=10]", "collection Add new[[a, 20]] old[]", "boxed Add new[a=20] old[]", "Add new[a=20] old[]", "collection Add new[[d, 21]] old[]", "boxed Add new[d=21] old[]", "Add new[d=21] old[]");
        source.RemoveRange(["e", "g", "f"]);
        assertState("removed e, g and f in one range", "a=20,c=11,d=21,h=8,i=9", "none", "collection Remove new[] old[[e, 5]]", "boxed Remove new[] old[e=5]", "Remove new[] old[e=5]", "collection Remove new[] old[[g, 7]]", "boxed Remove new[] old[g=7]", "Remove new[] old[g=7]");
        source.Reset(new Dictionary<string, object?> { ["x"] = 1, ["y"] = "z", ["w"] = null });
        assertState("reset", "x=1", "[InvalidCastException at y; NullReferenceException at w]", "collection Reset new[] old[]", "boxed Reset new[] old[]", "Reset new[] old[]");
        source.Clear();
        assertState("cleared", "", "none", "collection Remove new[] old[[x, 1]]", "boxed Remove new[] old[x=1]", "Remove new[] old[x=1]");
        source.Add("p", "q");
        assertState("added text to nothing", "", "InvalidCastException at p");
        source.Reset(new Dictionary<string, object?> { ["r"] = "s" });
        assertState("reset without a value", "", "InvalidCastException at r", "collection Reset new[] old[]", "boxed Reset new[] old[]", "Reset new[] old[]");
        cast.Dispose();
        sourceQuery.Dispose();
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ADictionaryCastSubscribesToNothingItCasts()
    {
        var log = new SubscriptionLog();
        var value = new Recorded(log) { Rank = 1 };
        var collectionObserver = new CollectionObserver();
        var source = new ObservableDictionary<string, Recorded>(new Dictionary<string, Recorded> { ["a"] = value });
        var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        var cast = sourceQuery.ObserveCast<object>();
        var announcements = new List<string>();
        ListenTo(cast, announcements);
        Assert.AreEqual(0, log.Attachments().Count, $"[{string.Join(", ", log.Attachments())}]");
        value.Rank = 2;
        Assert.AreEqual(0, announcements.Count, string.Join(" | ", announcements));
        cast.Dispose();
        sourceQuery.Dispose();
        Assert.AreEqual(0, log.Attachments().Count);
    }

    [TestMethod]
    public void ADictionaryCastAnnouncesNothingWhenAValuesContentsChange()
    {
        var inner = new ObservableCollection<int> { 1 };
        var collectionObserver = new CollectionObserver();
        var source = new ObservableDictionary<string, ObservableCollection<int>>(new Dictionary<string, ObservableCollection<int>> { ["a"] = inner });
        var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        var cast = sourceQuery.ObserveCast<IEnumerable<int>>();
        var announcements = new List<string>();
        ListenTo(cast, announcements);
        inner.Add(2);
        Assert.AreEqual(0, announcements.Count, string.Join(" | ", announcements));
        Assert.AreSame(inner, cast["a"]);
        cast.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void ADictionaryCastReportsItsSourcesFaultWithItsOwn()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableDictionary<string, object?>(new Dictionary<string, object?> { ["a"] = 0, ["b"] = 5, ["c"] = "x" });
        var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        var filter = sourceQuery.ObserveWhere((key, value) => !(value is int) || 10 / (int)value! > 1);
        var cast = filter.ObserveCast<int>();
        var faultAnnouncements = 0;
        ((INotifyPropertyChanged)cast).PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(IObservableDictionaryQuery<string, int>.OperationFault))
                ++faultAnnouncements;
        };
        Assert.AreEqual("b=5", Sorted(cast));
        Assert.AreEqual("[DivideByZeroException at a; InvalidCastException at c]", Describe(cast.OperationFault));
        Assert.AreEqual(0, faultAnnouncements);
        source.Add("d", 0);
        Assert.AreEqual("[DivideByZeroException at a; DivideByZeroException at d; InvalidCastException at c]", Describe(cast.OperationFault));
        Assert.AreEqual(1, faultAnnouncements);
        source.Add("e", "y");
        Assert.AreEqual("[DivideByZeroException at a; DivideByZeroException at d; InvalidCastException at c; InvalidCastException at e]", Describe(cast.OperationFault));
        Assert.AreEqual(2, faultAnnouncements);
        source.Remove("a");
        Assert.AreEqual("[DivideByZeroException at d; InvalidCastException at c; InvalidCastException at e]", Describe(cast.OperationFault));
        Assert.AreEqual(3, faultAnnouncements);
        source.Remove("d");
        Assert.AreEqual("[InvalidCastException at c; InvalidCastException at e]", Describe(cast.OperationFault));
        Assert.AreEqual(4, faultAnnouncements);
        source.Remove("c");
        Assert.AreEqual("InvalidCastException at e", Describe(cast.OperationFault));
        Assert.AreEqual(5, faultAnnouncements);
        source.Remove("e");
        Assert.AreEqual("none", Describe(cast.OperationFault));
        Assert.AreEqual(6, faultAnnouncements);
        cast.Dispose();
        filter.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void ADictionaryCastOfValueTypesUnboxesAsTheLanguageDoes()
    {
        var collectionObserver = new CollectionObserver();
        var integers = new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });
        var integersQuery = collectionObserver.ObserveReadOnlyDictionary(integers);
        var widened = integersQuery.ObserveCast<long>();
        var boxed = integersQuery.ObserveCast<object>();
        var nullable = integersQuery.ObserveCast<int?>();
        Assert.AreEqual(0, widened.Count);
        Assert.AreEqual("[InvalidCastException at a; InvalidCastException at b]", Describe(widened.OperationFault));
        Assert.AreEqual("a=1,b=2", Sorted(boxed));
        Assert.AreEqual("a=1,b=2", Sorted(nullable));
        var objects = new ObservableDictionary<string, object?>(new Dictionary<string, object?> { ["n"] = null, ["a"] = "a" });
        var objectsQuery = collectionObserver.ObserveReadOnlyDictionary(objects);
        var anything = objectsQuery.ObserveCast<object?>();
        var text = objectsQuery.ObserveCast<string?>();
        Assert.AreEqual("a=a,n=null", Sorted(anything));
        Assert.AreEqual("a=a,n=null", Sorted(text));
        Assert.IsNull(anything.OperationFault);
        Assert.IsNull(text.OperationFault);
        widened.Dispose();
        boxed.Dispose();
        nullable.Dispose();
        integersQuery.Dispose();
        anything.Dispose();
        text.Dispose();
        objectsQuery.Dispose();
    }

    [TestMethod]
    public void DictionaryCastsForOneTypeOutliveEachOther()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableDictionary<string, object>(new Dictionary<string, object> { ["a"] = "a", ["one"] = 1 });
        var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        var first = sourceQuery.ObserveCast<string>();
        var second = sourceQuery.ObserveCast<string>();
        first.Dispose();
        source.Add("b", "b");
        Assert.IsTrue(((IDisposalStatus)first).IsDisposed);
        Assert.IsFalse(((IDisposalStatus)second).IsDisposed);
        Assert.AreEqual("a=a,b=b", Sorted(second));
        second.Dispose();
        Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        sourceQuery.Dispose();
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
