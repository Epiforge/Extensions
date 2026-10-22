namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Holds every query to what its source queries already keep: a subscriber reading a query while it is notified finds what the notifications delivered so far describe, the count announced as changing before it changes and as changed once it has, and nothing announced for a count which holds
/// </summary>
[TestClass]
public class NotificationStateAgreement
{
    sealed class CollectionWatcher<T>
    {
        internal CollectionWatcher(IObservableCollectionQuery<T> query)
        {
            this.query = query;
            shadow = [.. query];
            ((INotifyPropertyChanging)query).PropertyChanging += QueryPropertyChanging;
            ((INotifyPropertyChanged)query).PropertyChanged += QueryPropertyChanged;
            ((INotifyCollectionChanged)query).CollectionChanged += QueryCollectionChanged;
        }

        int? countAtChanged;
        int? countAtChanging;
        readonly IObservableCollectionQuery<T> query;
        List<T> shadow;
        string step = "building";
        internal readonly List<string> Violations = [];

        static string Describe(IEnumerable<T> elements) =>
            $"[{string.Join(",", elements)}]";

        void QueryPropertyChanging(object? sender, PropertyChangingEventArgs e)
        {
            if (e.PropertyName != nameof(IObservableCollectionQuery<T>.Count))
                return;
            if (countAtChanging is not null)
                Violations.Add($"{step}: Count changing raised again before a collection notification");
            var count = query.Count;
            if (count != shadow.Count)
                Violations.Add($"{step}: Count changing read {count} where the notifications delivered describe {shadow.Count}");
            countAtChanging = count;
        }

        void QueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IObservableCollectionQuery<T>.Count))
                return;
            if (countAtChanging is null)
                Violations.Add($"{step}: Count changed raised without Count changing");
            if (countAtChanged is not null)
                Violations.Add($"{step}: Count changed raised again before a collection notification");
            countAtChanged = query.Count;
        }

        void QueryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            var countBefore = shadow.Count;
            if (!Replay(e))
                shadow = [.. query];
            var contents = query.ToList();
            if (!contents.SequenceEqual(shadow))
                Violations.Add($"{step}: {e.Action} delivered while the query held {Describe(contents)} where its notifications describe {Describe(shadow)}");
            if (shadow.Count != countBefore)
            {
                if (countAtChanged is null)
                    Violations.Add($"{step}: {e.Action} took the count from {countBefore} to {shadow.Count} without Count changed before it");
                else if (countAtChanged != shadow.Count)
                    Violations.Add($"{step}: Count changed read {countAtChanged} before a {e.Action} which left {shadow.Count}");
            }
            else if (countAtChanging is not null || countAtChanged is not null)
                Violations.Add($"{step}: Count notified before a {e.Action} which left it at {shadow.Count}");
            countAtChanging = null;
            countAtChanged = null;
        }

        bool Replay(NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when e.NewStartingIndex >= 0 && e.NewStartingIndex <= shadow.Count:
                    shadow.InsertRange(e.NewStartingIndex, e.NewItems!.Cast<T>());
                    return true;
                case NotifyCollectionChangedAction.Remove when e.OldStartingIndex >= 0 && e.OldStartingIndex + e.OldItems!.Count <= shadow.Count:
                    if (!shadow.Skip(e.OldStartingIndex).Take(e.OldItems.Count).SequenceEqual(e.OldItems.Cast<T>()))
                        Violations.Add($"{step}: Remove at {e.OldStartingIndex} carried {Describe(e.OldItems.Cast<T>())} where its notifications put {Describe(shadow.Skip(e.OldStartingIndex).Take(e.OldItems.Count))}");
                    shadow.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                    return true;
                case NotifyCollectionChangedAction.Replace when e.OldStartingIndex >= 0 && e.OldStartingIndex + e.OldItems!.Count <= shadow.Count:
                    if (!shadow.Skip(e.OldStartingIndex).Take(e.OldItems.Count).SequenceEqual(e.OldItems.Cast<T>()))
                        Violations.Add($"{step}: Replace at {e.OldStartingIndex} carried {Describe(e.OldItems.Cast<T>())} where its notifications put {Describe(shadow.Skip(e.OldStartingIndex).Take(e.OldItems.Count))}");
                    shadow.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                    shadow.InsertRange(e.OldStartingIndex, e.NewItems!.Cast<T>());
                    return true;
                case NotifyCollectionChangedAction.Move when e.OldStartingIndex >= 0 && e.OldStartingIndex + e.OldItems!.Count <= shadow.Count:
                    var moved = shadow.GetRange(e.OldStartingIndex, e.OldItems.Count);
                    shadow.RemoveRange(e.OldStartingIndex, moved.Count);
                    if (e.NewStartingIndex < 0 || e.NewStartingIndex > shadow.Count)
                        break;
                    shadow.InsertRange(e.NewStartingIndex, moved);
                    return true;
                case NotifyCollectionChangedAction.Reset:
                    return false;
            }
            Violations.Add($"{step}: {e.Action} could not be replayed (new at {e.NewStartingIndex}, old at {e.OldStartingIndex}, {shadow.Count} described)");
            return false;
        }

        internal void Settle(string nextStep)
        {
            if (countAtChanging is not null || countAtChanged is not null)
                Violations.Add($"{step}: Count notified with no collection notification after it");
            countAtChanging = null;
            countAtChanged = null;
            var contents = query.ToList();
            if (!contents.SequenceEqual(shadow))
                Violations.Add($"{step}: the query settled at {Describe(contents)} where its notifications describe {Describe(shadow)}");
            shadow = contents;
            step = nextStep;
        }
    }

    sealed class DictionaryWatcher<TKey, TValue>
        where TKey : notnull
    {
        internal DictionaryWatcher(IObservableDictionaryQuery<TKey, TValue> query)
        {
            this.query = query;
            shadow = new(query);
            ((INotifyPropertyChanging)query).PropertyChanging += QueryPropertyChanging;
            ((INotifyPropertyChanged)query).PropertyChanged += QueryPropertyChanged;
            ((INotifyDictionaryChanged<TKey, TValue>)query).DictionaryChanged += QueryDictionaryChanged;
        }

        int? countAtChanged;
        int? countAtChanging;
        readonly IObservableDictionaryQuery<TKey, TValue> query;
        Dictionary<TKey, TValue> shadow;
        string step = "building";
        internal readonly List<string> Violations = [];

        static string Describe(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs) =>
            $"{{{string.Join(",", keyValuePairs.Select(keyValuePair => $"{keyValuePair.Key}={keyValuePair.Value}").OrderBy(description => description, StringComparer.Ordinal))}}}";

        bool Agrees() =>
            query.Count == shadow.Count && query.All(keyValuePair => shadow.TryGetValue(keyValuePair.Key, out var described) && EqualityComparer<TValue>.Default.Equals(described, keyValuePair.Value));

        void QueryPropertyChanging(object? sender, PropertyChangingEventArgs e)
        {
            if (e.PropertyName != nameof(IObservableDictionaryQuery<TKey, TValue>.Count))
                return;
            if (countAtChanging is not null)
                Violations.Add($"{step}: Count changing raised again before a dictionary notification");
            var count = query.Count;
            if (count != shadow.Count)
                Violations.Add($"{step}: Count changing read {count} where the notifications delivered describe {shadow.Count}");
            countAtChanging = count;
        }

        void QueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IObservableDictionaryQuery<TKey, TValue>.Count))
                return;
            if (countAtChanging is null)
                Violations.Add($"{step}: Count changed raised without Count changing");
            if (countAtChanged is not null)
                Violations.Add($"{step}: Count changed raised again before a dictionary notification");
            countAtChanged = query.Count;
        }

        void QueryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
        {
            var countBefore = shadow.Count;
            if (e.Action == NotifyDictionaryChangedAction.Reset)
                shadow = new(query);
            else
            {
                foreach (var (key, value) in e.OldItems)
                    if (!shadow.Remove(key, out var described) || !EqualityComparer<TValue>.Default.Equals(described, value))
                        Violations.Add($"{step}: {e.Action} took {key}={value} out where its notifications put {(shadow.ContainsKey(key) || described is not null ? $"{key}={described}" : "nothing")}");
                foreach (var (key, value) in e.NewItems)
                    if (!shadow.TryAdd(key, value))
                    {
                        Violations.Add($"{step}: {e.Action} put {key}={value} in where its notifications already put {key}={shadow[key]}");
                        shadow[key] = value;
                    }
            }
            if (!Agrees())
                Violations.Add($"{step}: {e.Action} delivered while the query held {Describe(query)} where its notifications describe {Describe(shadow)}");
            if (shadow.Count != countBefore)
            {
                if (countAtChanged is null)
                    Violations.Add($"{step}: {e.Action} took the count from {countBefore} to {shadow.Count} without Count changed before it");
                else if (countAtChanged != shadow.Count)
                    Violations.Add($"{step}: Count changed read {countAtChanged} before a {e.Action} which left {shadow.Count}");
            }
            else if (countAtChanging is not null || countAtChanged is not null)
                Violations.Add($"{step}: Count notified before a {e.Action} which left it at {shadow.Count}");
            countAtChanging = null;
            countAtChanged = null;
        }

        internal void Settle(string nextStep)
        {
            if (countAtChanging is not null || countAtChanged is not null)
                Violations.Add($"{step}: Count notified with no dictionary notification after it");
            countAtChanging = null;
            countAtChanged = null;
            if (!Agrees())
                Violations.Add($"{step}: the query settled at {Describe(query)} where its notifications describe {Describe(shadow)}");
            shadow = new(query);
            step = nextStep;
        }
    }

    static void Change(ObservableRangeCollection<int> source, Action<string> settle)
    {
        settle("added four");
        source.Add(4);
        settle("inserted five second");
        source.Insert(1, 5);
        settle("removed the first");
        source.RemoveAt(0);
        settle("replaced the first with zero");
        source[0] = 0;
        settle("moved the first to the end");
        source.Move(0, source.Count - 1);
        settle("added six and seven");
        source.AddRange([6, 7]);
        settle("removed two from the second");
        source.RemoveRange(1, 2);
        settle("moved two from the first after two more");
        source.MoveRange(0, 2, 2);
        settle("reset");
        source.Reset([3, 1, 3]);
        settle("cleared");
        source.Clear();
        settle("added one");
        source.Add(1);
        settle("done");
    }

    static void Change(ObservableDictionary<string, int> source, Action<string> settle)
    {
        settle("added d");
        source.Add("d", 4);
        settle("removed a");
        source.Remove("a");
        settle("replaced b with five");
        source["b"] = 5;
        settle("replaced c with zero");
        source["c"] = 0;
        settle("added e and f");
        source.AddRange([new("e", 6), new("f", 7)]);
        settle("removed e and b");
        source.RemoveRange(["e", "b"]);
        settle("replaced d and f");
        source.ReplaceRange([new("d", 8), new("f", 1)]);
        settle("reset");
        source.Reset(new Dictionary<string, int> { ["x"] = 3, ["y"] = 1 });
        settle("cleared");
        source.Clear();
        settle("added z");
        source.Add("z", 9);
        settle("done");
    }

    [TestMethod]
    [DataRow("source")]
    [DataRow("filter")]
    [DataRow("projection")]
    [DataRow("cast")]
    [DataRow("type filter")]
    [DataRow("flattening")]
    [DataRow("concatenation")]
    [DataRow("ordering")]
    [DataRow("appending")]
    [DataRow("prepending")]
    [DataRow("skipping")]
    [DataRow("taking")]
    [DataRow("distinct")]
    [DataRow("grouping")]
    [DataRow("individual changes")]
    [DataRow("sync root")]
    [DataRow("synchronization callback")]
    public void ACollectionQueryAgreesWithItsNotifications(string kind)
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<int>([1, 2, 3, 2]);
        var other = new ObservableRangeCollection<int>([7]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var otherQuery = collectionObserver.ObserveReadOnlyList(other);

        IReadOnlyList<string> watch<T>(IObservableCollectionQuery<T> query)
        {
            var watcher = new CollectionWatcher<T>(query);
            Change(source, watcher.Settle);
            if (!ReferenceEquals(query, sourceQuery))
                query.Dispose();
            return watcher.Violations;
        }

        var violations = kind switch
        {
            "source" => watch(sourceQuery),
            "filter" => watch(sourceQuery.ObserveWhere(element => element > 2)),
            "projection" => watch(sourceQuery.ObserveSelect(element => element * 10)),
            "cast" => watch(sourceQuery.ObserveCast<object>()),
            "type filter" => watch(sourceQuery.ObserveOfType<int>()),
            "flattening" => watch(sourceQuery.ObserveSelectMany(element => new[] { element, element })),
            "concatenation" => watch(sourceQuery.ObserveConcat(otherQuery)),
            "ordering" => watch(sourceQuery.ObserveOrderBy(element => element)),
            "appending" => watch(sourceQuery.ObserveAppend(0)),
            "prepending" => watch(sourceQuery.ObservePrepend(0)),
            "skipping" => watch(sourceQuery.ObserveSkip(1)),
            "taking" => watch(sourceQuery.ObserveTake(2)),
            "distinct" => watch(sourceQuery.ObserveDistinct()),
            "grouping" => watch(sourceQuery.ObserveGroupBy(element => element % 2)),
            "individual changes" => watch(sourceQuery.ObserveIndividualChanges()),
            "sync root" => watch(sourceQuery.ObserveUsingSyncRoot(new object())),
            "synchronization callback" => watch(sourceQuery.ObserveUsingSynchronizationCallback(new object(), (collection, context, accessMethod, writeAccess) => accessMethod())),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        otherQuery.Dispose();
        sourceQuery.Dispose();
        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    [DataRow("source")]
    [DataRow("filter")]
    [DataRow("projection")]
    [DataRow("cast")]
    [DataRow("type filter")]
    [DataRow("concurrently")]
    public void ADictionaryQueryAgreesWithItsNotifications(string kind)
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2, ["c"] = 3 });
        var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source);

        IReadOnlyList<string> watch<TValue>(IObservableDictionaryQuery<string, TValue> query)
        {
            var watcher = new DictionaryWatcher<string, TValue>(query);
            Change(source, watcher.Settle);
            if (!ReferenceEquals(query, sourceQuery))
                query.Dispose();
            return watcher.Violations;
        }

        var violations = kind switch
        {
            "source" => watch(sourceQuery),
            "filter" => watch(sourceQuery.ObserveWhere((key, value) => value > 2)),
            "projection" => watch(sourceQuery.ObserveSelect((key, value) => key, (key, value) => value * 10)),
            "cast" => watch(sourceQuery.ObserveCast<object>()),
            "type filter" => watch(sourceQuery.ObserveOfType<int>()),
            "concurrently" => watch(sourceQuery.ObserveConcurrently()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        sourceQuery.Dispose();
        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void ADictionaryOfACollectionAgreesWithItsNotifications()
    {
        var collectionObserver = new CollectionObserver();
        var source = new ObservableRangeCollection<int>([1, 2, 3, 2]);
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var query = sourceQuery.ObserveToDictionary(element => element, element => element * 10);
        var watcher = new DictionaryWatcher<int, int>(query);
        Change(source, watcher.Settle);
        query.Dispose();
        sourceQuery.Dispose();
        Assert.AreEqual(0, watcher.Violations.Count, string.Join(Environment.NewLine, watcher.Violations));
    }
}
