namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

/// <summary>
/// Holds the observable range collection to what it promises a subscriber reading it while it is notified: what the notifications delivered so far describe, and the count announced as changed before the notification which changes it, reading what that notification leaves, and not at all for a notification which leaves it as it was
/// </summary>
[TestClass]
public class RangeCollectionNotificationStateAgreement
{
    sealed class Watcher
    {
        internal Watcher(ObservableRangeCollection<int> collection)
        {
            this.collection = collection;
            shadow = [.. collection];
            ((INotifyPropertyChanged)collection).PropertyChanged += CollectionPropertyChanged;
            collection.CollectionChanged += CollectionCollectionChanged;
        }

        readonly ObservableRangeCollection<int> collection;
        int? countAtChanged;
        List<int> shadow;
        string step = "building";
        internal readonly List<string> Violations = [];

        static string Describe(IEnumerable<int> elements) =>
            $"[{string.Join(",", elements)}]";

        void CollectionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            var countBefore = shadow.Count;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    shadow = [.. collection];
                    break;
                case NotifyCollectionChangedAction.Move:
                    var moved = shadow.GetRange(e.OldStartingIndex, e.OldItems!.Count);
                    shadow.RemoveRange(e.OldStartingIndex, moved.Count);
                    shadow.InsertRange(e.NewStartingIndex, moved);
                    break;
                default:
                    if (e.OldItems is { } oldItems)
                    {
                        var described = shadow.Skip(e.OldStartingIndex).Take(oldItems.Count).ToList();
                        if (!described.SequenceEqual(oldItems.Cast<int>()))
                            Violations.Add($"{step}: {e.Action} took {Describe(oldItems.Cast<int>())} out at {e.OldStartingIndex} where its notifications put {Describe(described)}");
                        shadow.RemoveRange(e.OldStartingIndex, Math.Min(oldItems.Count, shadow.Count - e.OldStartingIndex));
                    }
                    if (e.NewItems is { } newItems)
                        shadow.InsertRange(e.NewStartingIndex, newItems.Cast<int>());
                    break;
            }
            if (!collection.SequenceEqual(shadow))
                Violations.Add($"{step}: {e.Action} delivered while the collection held {Describe(collection)} where its notifications describe {Describe(shadow)}");
            if (shadow.Count != countBefore)
            {
                if (countAtChanged is null)
                    Violations.Add($"{step}: {e.Action} took the count from {countBefore} to {shadow.Count} without Count changed before it");
                else if (countAtChanged != shadow.Count)
                    Violations.Add($"{step}: Count changed read {countAtChanged} before a {e.Action} which left {shadow.Count}");
            }
            else if (countAtChanged is not null)
                Violations.Add($"{step}: Count changed before a {e.Action} which left it at {shadow.Count}");
            countAtChanged = null;
        }

        void CollectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ObservableRangeCollection<int>.Count))
                return;
            if (countAtChanged is not null)
                Violations.Add($"{step}: Count changed raised again before a collection notification");
            countAtChanged = collection.Count;
        }

        internal void Settle(string nextStep)
        {
            if (countAtChanged is not null)
                Violations.Add($"{step}: Count changed with no collection notification after it");
            countAtChanged = null;
            if (!collection.SequenceEqual(shadow))
                Violations.Add($"{step}: the collection settled at {Describe(collection)} where its notifications describe {Describe(shadow)}");
            shadow = [.. collection];
            step = nextStep;
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnObservableRangeCollectionAgreesWithItsNotifications(bool individually)
    {
        var collection = new ObservableRangeCollection<int>(Enumerable.Range(1, 5), individually);
        var watcher = new Watcher(collection);
        watcher.Settle("added one");
        collection.Add(6);
        watcher.Settle("added two");
        collection.AddRange([7, 8]);
        watcher.Settle("inserted one");
        collection.Insert(1, 9);
        watcher.Settle("inserted two");
        collection.InsertRange(2, [10, 11]);
        watcher.Settle("removed one by position");
        collection.RemoveAt(0);
        watcher.Settle("removed two by position");
        collection.RemoveRange(1, 2);
        watcher.Settle("removed two by value");
        collection.RemoveRange([7, 8]);
        watcher.Settle("removed every even element");
        collection.RemoveAll(element => element % 2 == 0);
        watcher.Settle("set one");
        collection[0] = 12;
        watcher.Settle("moved one");
        collection.Move(0, 2);
        watcher.Settle("moved two");
        collection.MoveRange(0, 1, 2);
        watcher.Settle("replaced one with three");
        collection.ReplaceRange(1, 1, [13, 14, 15]);
        watcher.Settle("replaced three with one");
        collection.ReplaceRange(1, 3, [16]);
        watcher.Settle("replaced two with two");
        collection.ReplaceRange(0, 2, [17, 18]);
        watcher.Settle("replaced everything with more");
        collection.ReplaceAll([19, 20, 21, 22, 23, 24]);
        watcher.Settle("replaced everything with fewer");
        collection.ReplaceAll([25, 26]);
        watcher.Settle("replaced everything with as many");
        collection.ReplaceAll([27, 28]);
        watcher.Settle("reset");
        collection.Reset([29, 30, 31, 32]);
        watcher.Settle("reset removing every odd element");
        collection.ResetRemovingAll(element => element % 2 == 1);
        watcher.Settle("cleared");
        collection.Clear();
        watcher.Settle("done");
        Assert.AreEqual(0, watcher.Violations.Count, string.Join(Environment.NewLine, watcher.Violations));
    }
}
