namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

/// <summary>
/// Holds the observable dictionaries to what they promise a subscriber reading one while it is notified: what the notifications delivered so far describe, the count announced as changing before it changes and as changed once it has, before the notifications describing the change, and nothing announced for a count which holds
/// </summary>
[TestClass]
public class DictionaryNotificationStateAgreement
{
    sealed class Watcher<TDictionary>
        where TDictionary : IReadOnlyDictionary<string, int>, INotifyDictionaryChanged<string, int>, INotifyPropertyChanged, INotifyPropertyChanging
    {
        internal Watcher(TDictionary dictionary)
        {
            this.dictionary = dictionary;
            shadow = new(dictionary);
            dictionary.PropertyChanging += DictionaryPropertyChanging;
            dictionary.PropertyChanged += DictionaryPropertyChanged;
            dictionary.DictionaryChanged += DictionaryDictionaryChanged;
        }

        int? countAtChanged;
        int? countAtChanging;
        readonly TDictionary dictionary;
        Dictionary<string, int> shadow;
        string step = "building";
        internal readonly List<string> Violations = [];

        static string Describe(IEnumerable<KeyValuePair<string, int>> keyValuePairs) =>
            $"{{{string.Join(",", keyValuePairs.Select(keyValuePair => $"{keyValuePair.Key}={keyValuePair.Value}").OrderBy(description => description, StringComparer.Ordinal))}}}";

        bool Agrees() =>
            dictionary.Count == shadow.Count && dictionary.All(keyValuePair => shadow.TryGetValue(keyValuePair.Key, out var described) && described == keyValuePair.Value);

        void DictionaryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<string, int> e)
        {
            var countBefore = shadow.Count;
            if (e.Action == NotifyDictionaryChangedAction.Reset)
                shadow = new(dictionary);
            else
            {
                foreach (var (key, value) in e.OldItems)
                    if (!shadow.Remove(key, out var described) || described != value)
                        Violations.Add($"{step}: {e.Action} took {key}={value} out where its notifications put {(shadow.ContainsKey(key) ? $"{key}={described}" : "nothing")}");
                foreach (var (key, value) in e.NewItems)
                    if (!shadow.TryAdd(key, value))
                    {
                        Violations.Add($"{step}: {e.Action} put {key}={value} in where its notifications already put {key}={shadow[key]}");
                        shadow[key] = value;
                    }
            }
            if (!Agrees())
                Violations.Add($"{step}: {e.Action} delivered while the dictionary held {Describe(dictionary)} where its notifications describe {Describe(shadow)}");
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

        void DictionaryPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IReadOnlyDictionary<string, int>.Count))
                return;
            if (countAtChanging is null)
                Violations.Add($"{step}: Count changed raised without Count changing");
            if (countAtChanged is not null)
                Violations.Add($"{step}: Count changed raised again before a dictionary notification");
            countAtChanged = dictionary.Count;
        }

        void DictionaryPropertyChanging(object? sender, PropertyChangingEventArgs e)
        {
            if (e.PropertyName != nameof(IReadOnlyDictionary<string, int>.Count))
                return;
            if (countAtChanging is not null)
                Violations.Add($"{step}: Count changing raised again before a dictionary notification");
            var count = dictionary.Count;
            if (count != shadow.Count)
                Violations.Add($"{step}: Count changing read {count} where the notifications delivered describe {shadow.Count}");
            countAtChanging = count;
        }

        internal void Settle(string nextStep)
        {
            if (countAtChanging is not null || countAtChanged is not null)
                Violations.Add($"{step}: Count notified with no dictionary notification after it");
            countAtChanging = null;
            countAtChanged = null;
            if (!Agrees())
                Violations.Add($"{step}: the dictionary settled at {Describe(dictionary)} where its notifications describe {Describe(shadow)}");
            shadow = new(dictionary);
            step = nextStep;
        }
    }

    static IReadOnlyList<string> Watch<TDictionary>(TDictionary dictionary)
        where TDictionary : IReadOnlyDictionary<string, int>, IObservableRangeDictionary<string, int>, IDictionary<string, int>, INotifyDictionaryChanged<string, int>, INotifyPropertyChanged, INotifyPropertyChanging
    {
        var watcher = new Watcher<TDictionary>(dictionary);
        watcher.Settle("added c");
        ((IDictionary<string, int>)dictionary).Add("c", 3);
        watcher.Settle("removed a");
        ((IDictionary<string, int>)dictionary).Remove("a");
        watcher.Settle("replaced b");
        ((IDictionary<string, int>)dictionary)["b"] = 5;
        watcher.Settle("added e and f");
        dictionary.AddRange([new("e", 6), new("f", 7)]);
        watcher.Settle("removed e and b");
        dictionary.RemoveRange(["e", "b"]);
        watcher.Settle("replaced c and f in place");
        dictionary.ReplaceRange([new("c", 8), new("f", 1)]);
        watcher.Settle("replaced c with g and h");
        dictionary.ReplaceRange(["c"], [new("g", 2), new("h", 3)]);
        watcher.Settle("replaced f and g with i");
        dictionary.ReplaceRange(["f", "g"], [new("i", 4)]);
        watcher.Settle("replaced h with j");
        dictionary.ReplaceRange(["h"], [new("j", 5)]);
        watcher.Settle("removed every value of five");
        dictionary.RemoveAll((key, value) => value == 5);
        watcher.Settle("reset to as many");
        dictionary.Reset(new Dictionary<string, int> { ["x"] = 1 });
        watcher.Settle("reset to more");
        dictionary.Reset(new Dictionary<string, int> { ["x"] = 1, ["y"] = 2, ["z"] = 3 });
        watcher.Settle("reset to fewer");
        dictionary.Reset(new Dictionary<string, int> { ["w"] = 4 });
        watcher.Settle("reset to nothing");
        dictionary.Reset();
        watcher.Settle("reset from nothing");
        dictionary.Reset(new Dictionary<string, int> { ["v"] = 5, ["u"] = 6 });
        watcher.Settle("cleared");
        ((IDictionary<string, int>)dictionary).Clear();
        watcher.Settle("done");
        return watcher.Violations;
    }

    [TestMethod]
    [DataRow("dictionary")]
    [DataRow("sorted dictionary")]
    public void AnObservableDictionaryAgreesWithItsNotifications(string kind)
    {
        var initial = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var violations = kind switch
        {
            "dictionary" => Watch(new ObservableDictionary<string, int>(initial)),
            "sorted dictionary" => Watch(new ObservableSortedDictionary<string, int>(initial)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));
    }
}
