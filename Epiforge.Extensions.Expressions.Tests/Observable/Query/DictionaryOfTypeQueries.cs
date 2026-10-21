namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Holds what a dictionary type filter reports of its values, its faults and its changes, and what it subscribes to, so that how it is built can change without what it reports changing
/// </summary>
[TestClass]
public class DictionaryOfTypeQueries
{
    sealed class Distinguished(SubscriptionLog log) :
        Recorded(log);

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
    public void ADictionaryTypeFilterFollowsEveryKindOfChangeWithItsAnnouncements()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableDictionary<string, object?>(new Dictionary<string, object?> { ["a"] = 1, ["b"] = "two", ["c"] = null, ["d"] = 4 });
        var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        var ofType = sourceQuery.ObserveOfType<int>();
        var announcements = new List<string>();
        ListenTo(ofType, announcements);

        void assertState(string step, string contents, string fault, params string[] announced)
        {
            Assert.AreEqual(contents, Sorted(ofType), $"{step}: enumerated");
            Assert.AreEqual(contents, Describe(ofType.Keys.OrderBy(key => key, StringComparer.Ordinal).Select(key => new KeyValuePair<string, int>(key, ofType[key]))), $"{step}: indexed by keys");
            Assert.AreEqual(contents, Describe(ofType.Keys.OrderBy(key => key, StringComparer.Ordinal).Zip(ofType.Keys.OrderBy(key => key, StringComparer.Ordinal).Select(key => ofType.TryGetValue(key, out var value) ? value : -1), (key, value) => new KeyValuePair<string, int>(key, value))), $"{step}: tried by keys");
            var copy = new KeyValuePair<string, int>[ofType.Count];
            ((ICollection<KeyValuePair<string, int>>)ofType).CopyTo(copy, 0);
            Assert.AreEqual(contents, Describe(copy.OrderBy(keyValuePair => keyValuePair.Key, StringComparer.Ordinal)), $"{step}: copied");
            CollectionAssert.AreEquivalent(ofType.Select(keyValuePair => keyValuePair.Value).ToArray(), ofType.Values.ToArray(), $"{step}: values");
            Assert.AreEqual(fault, Describe(ofType.OperationFault), step);
            CollectionAssert.AreEqual(announced, announcements, $"{step}: {string.Join(" | ", announcements)}");
            announcements.Clear();
        }

        assertState("initial", "a=1,d=4", "none");
        source.Add("e", 5);
        assertState("added e five", "a=1,d=4,e=5", "none", "collection Add new[[e, 5]] old[]", "boxed Add new[e=5] old[]", "Add new[e=5] old[]");
        source.Add("f", "six");
        assertState("added f six", "a=1,d=4,e=5", "none");
        source.AddRange([new("g", 7), new("h", "eight"), new("i", 9)]);
        assertState("added g, h and i", "a=1,d=4,e=5,g=7,i=9", "none", "collection Add new[[g, 7]] old[]", "boxed Add new[g=7] old[]", "Add new[g=7] old[]", "collection Add new[[i, 9]] old[]", "boxed Add new[i=9] old[]", "Add new[i=9] old[]");
        source.Remove("b");
        assertState("removed b", "a=1,d=4,e=5,g=7,i=9", "none");
        source["a"] = 10;
        assertState("replaced a with ten", "a=10,d=4,e=5,g=7,i=9", "none", "collection Remove new[] old[[a, 1]]", "boxed Remove new[] old[a=1]", "Remove new[] old[a=1]", "collection Add new[[a, 10]] old[]", "boxed Add new[a=10] old[]", "Add new[a=10] old[]");
        source["c"] = 11;
        assertState("replaced c with eleven", "a=10,c=11,d=4,e=5,g=7,i=9", "none", "collection Add new[[c, 11]] old[]", "boxed Add new[c=11] old[]", "Add new[c=11] old[]");
        source["d"] = "four";
        assertState("replaced d with text", "a=10,c=11,e=5,g=7,i=9", "none", "collection Remove new[] old[[d, 4]]", "boxed Remove new[] old[d=4]", "Remove new[] old[d=4]");
        source["f"] = "six again";
        assertState("replaced f with other text", "a=10,c=11,e=5,g=7,i=9", "none");
        source["h"] = 8;
        assertState("replaced h with eight", "a=10,c=11,e=5,g=7,h=8,i=9", "none", "collection Add new[[h, 8]] old[]", "boxed Add new[h=8] old[]", "Add new[h=8] old[]");
        source.ReplaceRange([new("a", 20), new("d", 21)]);
        assertState("replaced a and d in one range", "a=20,c=11,d=21,e=5,g=7,h=8,i=9", "none", "collection Remove new[] old[[a, 10]]", "boxed Remove new[] old[a=10]", "Remove new[] old[a=10]", "collection Add new[[a, 20]] old[]", "boxed Add new[a=20] old[]", "Add new[a=20] old[]", "collection Add new[[d, 21]] old[]", "boxed Add new[d=21] old[]", "Add new[d=21] old[]");
        source.RemoveRange(["e", "g", "f"]);
        assertState("removed e, g and f in one range", "a=20,c=11,d=21,h=8,i=9", "none", "collection Remove new[] old[[e, 5]]", "boxed Remove new[] old[e=5]", "Remove new[] old[e=5]", "collection Remove new[] old[[g, 7]]", "boxed Remove new[] old[g=7]", "Remove new[] old[g=7]");
        source.Reset(new Dictionary<string, object?> { ["x"] = 1, ["y"] = "z", ["w"] = null });
        assertState("reset", "x=1", "none", "collection Reset new[] old[]", "boxed Reset new[] old[]", "Reset new[] old[]");
        source.Clear();
        assertState("cleared", "", "none", "collection Remove new[] old[[x, 1]]", "boxed Remove new[] old[x=1]", "Remove new[] old[x=1]");
        source.Add("p", "q");
        assertState("added text to nothing", "", "none");
        source.Reset(new Dictionary<string, object?> { ["r"] = "s" });
        assertState("reset without a value", "", "none", "collection Reset new[] old[]", "boxed Reset new[] old[]", "Reset new[] old[]");
        ofType.Dispose();
        sourceQuery.Dispose();
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ADictionaryTypeFilterSubscribesToNothingItTests()
    {
        var log = new SubscriptionLog();
        var plain = new Recorded(log) { Rank = 1 };
        var distinguished = new Distinguished(log) { Rank = 2 };
        var collectionObserver = new CollectionObserver();
        var source = new ObservableDictionary<string, Recorded>(new Dictionary<string, Recorded> { ["plain"] = plain, ["distinguished"] = distinguished });
        var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        var everyone = sourceQuery.ObserveOfType<Recorded>();
        var onlyDistinguished = sourceQuery.ObserveOfType<Distinguished>();
        var announcements = new List<string>();
        ListenTo(everyone, announcements);
        ListenTo(onlyDistinguished, announcements);
        CollectionAssert.AreEquivalent(new[] { "plain", "distinguished" }, everyone.Keys.ToArray());
        CollectionAssert.AreEqual(new[] { "distinguished" }, onlyDistinguished.Keys.ToArray());
        Assert.AreSame(distinguished, onlyDistinguished["distinguished"]);
        Assert.AreEqual(0, log.Attachments().Count, $"[{string.Join(", ", log.Attachments())}]");
        plain.Rank = 3;
        distinguished.Rank = 4;
        Assert.AreEqual(0, announcements.Count, string.Join(" | ", announcements));
        everyone.Dispose();
        onlyDistinguished.Dispose();
        sourceQuery.Dispose();
        Assert.AreEqual(0, log.Attachments().Count);
    }

    [TestMethod]
    public void ADictionaryTypeFilterAnnouncesNothingWhenAValuesContentsChange()
    {
        var inner = new ObservableCollection<int> { 1 };
        var collectionObserver = new CollectionObserver();
        var source = new ObservableDictionary<string, ObservableCollection<int>>(new Dictionary<string, ObservableCollection<int>> { ["a"] = inner });
        var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        var ofType = sourceQuery.ObserveOfType<IEnumerable<int>>();
        var announcements = new List<string>();
        ListenTo(ofType, announcements);
        inner.Add(2);
        Assert.AreEqual(0, announcements.Count, string.Join(" | ", announcements));
        Assert.AreSame(inner, ofType["a"]);
        ofType.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void ADictionaryTypeFilterReportsItsSourcesFault()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableDictionary<string, object?>(new Dictionary<string, object?> { ["a"] = 0, ["b"] = 5, ["c"] = "x" });
        var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        var filter = sourceQuery.ObserveWhere((key, value) => !(value is int) || 10 / (int)value! > 1);
        var ofType = filter.ObserveOfType<int>();
        var faultAnnouncements = 0;
        ((INotifyPropertyChanged)ofType).PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(IObservableDictionaryQuery<string, int>.OperationFault))
                ++faultAnnouncements;
        };
        Assert.AreEqual("b=5", Sorted(ofType));
        Assert.AreEqual("DivideByZeroException at a", Describe(ofType.OperationFault));
        Assert.AreEqual(0, faultAnnouncements);
        source.Add("d", 0);
        Assert.AreEqual("[DivideByZeroException at a; DivideByZeroException at d]", Describe(ofType.OperationFault));
        Assert.AreEqual(1, faultAnnouncements);
        source.Add("e", "y");
        Assert.AreEqual("[DivideByZeroException at a; DivideByZeroException at d]", Describe(ofType.OperationFault));
        Assert.AreEqual(1, faultAnnouncements);
        source.Remove("a");
        Assert.AreEqual("DivideByZeroException at d", Describe(ofType.OperationFault));
        Assert.AreEqual(2, faultAnnouncements);
        source.Remove("d");
        Assert.AreEqual("none", Describe(ofType.OperationFault));
        Assert.AreEqual(3, faultAnnouncements);
        source.Remove("c");
        Assert.AreEqual("none", Describe(ofType.OperationFault));
        Assert.AreEqual(3, faultAnnouncements);
        source.Remove("e");
        Assert.AreEqual("none", Describe(ofType.OperationFault));
        Assert.AreEqual(3, faultAnnouncements);
        ofType.Dispose();
        filter.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void ADictionaryTypeFilterTestsTypesAsTheLanguageDoes()
    {
        var collectionObserver = new CollectionObserver();
        var integers = new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });
        var integersQuery = collectionObserver.ObserveReadOnlyDictionary(integers);
        var widened = integersQuery.ObserveOfType<long>();
        var boxed = integersQuery.ObserveOfType<object>();
        var nullable = integersQuery.ObserveOfType<int?>();
        Assert.AreEqual(0, widened.Count);
        Assert.AreEqual("none", Describe(widened.OperationFault));
        Assert.AreEqual("a=1,b=2", Sorted(boxed));
        Assert.AreEqual("a=1,b=2", Sorted(nullable));
        var objects = new ObservableDictionary<string, object?>(new Dictionary<string, object?> { ["n"] = null, ["a"] = "a" });
        var objectsQuery = collectionObserver.ObserveReadOnlyDictionary(objects);
        var anything = objectsQuery.ObserveOfType<object>();
        var text = objectsQuery.ObserveOfType<string?>();
        Assert.AreEqual("a=a", Sorted(anything));
        Assert.AreEqual("a=a", Sorted(text));
        widened.Dispose();
        boxed.Dispose();
        nullable.Dispose();
        integersQuery.Dispose();
        anything.Dispose();
        text.Dispose();
        objectsQuery.Dispose();
    }

    [TestMethod]
    public void DictionaryTypeFiltersForOneTypeOutliveEachOther()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableDictionary<string, object>(new Dictionary<string, object> { ["a"] = "a", ["one"] = 1 });
        var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);
        var first = sourceQuery.ObserveOfType<string>();
        var second = sourceQuery.ObserveOfType<string>();
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
