namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class SliceNotificationPrecision
{
    static void Change(ObservableRangeCollection<int> source, string change)
    {
        var words = change.Split(' ');
        static int[] Elements(string list) =>
            list.Split(',').Select(int.Parse).ToArray();
        switch (words)
        {
            case ["add", var element]:
                source.Add(int.Parse(element));
                break;
            case ["insert", var elements, "at", var index]:
                source.InsertRange(int.Parse(index), Elements(elements));
                break;
            case ["remove", "at", var index]:
                source.RemoveAt(int.Parse(index));
                break;
            case ["remove", var count, "at", var index]:
                source.RemoveRange(int.Parse(index), int.Parse(count));
                break;
            case ["set", var index, "to", var element]:
                source[int.Parse(index)] = int.Parse(element);
                break;
            case ["replace", var count, "at", var index, "with", var elements]:
                source.ReplaceRange(int.Parse(index), int.Parse(count), Elements(elements));
                break;
            case ["move", "from", var oldIndex, "to", var newIndex]:
                source.Move(int.Parse(oldIndex), int.Parse(newIndex));
                break;
            case ["move", var count, "from", var oldIndex, "to", var newIndex]:
                source.MoveRange(int.Parse(oldIndex), int.Parse(newIndex), int.Parse(count));
                break;
            case ["reset"]:
                source.Reset(Enumerable.Range(50, 10));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }
    }

    static string Describe(NotifyCollectionChangedEventArgs e)
    {
        static string List(IList? items) =>
            $"[{string.Join(", ", items?.Cast<int>() ?? [])}]";
        return e.Action switch
        {
            NotifyCollectionChangedAction.Add => $"Add {List(e.NewItems)} at {e.NewStartingIndex}",
            NotifyCollectionChangedAction.Remove => $"Remove {List(e.OldItems)} at {e.OldStartingIndex}",
            NotifyCollectionChangedAction.Replace => $"Replace {List(e.OldItems)} with {List(e.NewItems)} at {e.NewStartingIndex}",
            NotifyCollectionChangedAction.Move => $"Move {List(e.OldItems)} from {e.OldStartingIndex} to {e.NewStartingIndex}",
            _ => "Reset"
        };
    }

    static IObservableCollectionQuery<int> Observe(IObservableCollectionQuery<int> sourceQuery, string slice) =>
        slice switch
        {
            "take 3" => sourceQuery.ObserveTake(3),
            "skip 7" => sourceQuery.ObserveSkip(7),
            "^4..^1" => sourceQuery.ObserveSlice(^4..^1),
            _ => throw new ArgumentOutOfRangeException(nameof(slice))
        };

    [TestMethod]
    [DataRow("take 3", "set 1 to 100", "Replace [1] with [100] at 1", "0, 100, 2")]
    [DataRow("take 3", "set 5 to 100", "", "0, 1, 2")]
    [DataRow("take 3", "replace 2 at 2 with 100,101", "Replace [2] with [100] at 2", "0, 1, 100")]
    [DataRow("take 3", "insert 100 at 0", "Remove [2] at 2; Add [100] at 0", "100, 0, 1")]
    [DataRow("take 3", "insert 100 at 1", "Add [100] at 1; Remove [2] at 3", "0, 100, 1")]
    [DataRow("take 3", "insert 100 at 3", "", "0, 1, 2")]
    [DataRow("take 3", "insert 100,101 at 2", "Replace [2] with [100] at 2", "0, 1, 100")]
    [DataRow("take 3", "insert 100,101,102,103 at 0", "Replace [0, 1, 2] with [100, 101, 102] at 0", "100, 101, 102")]
    [DataRow("take 3", "remove at 0", "Remove [0] at 0; Add [3] at 2", "1, 2, 3")]
    [DataRow("take 3", "remove at 5", "", "0, 1, 2")]
    [DataRow("take 3", "remove 2 at 2", "Replace [2] with [4] at 2", "0, 1, 4")]
    [DataRow("take 3", "remove 5 at 0", "Replace [0, 1, 2] with [5, 6, 7] at 0", "5, 6, 7")]
    [DataRow("take 3", "replace 1 at 1 with 100,101", "Replace [1] with [100] at 1; Replace [2] with [101] at 2", "0, 100, 101")]
    [DataRow("take 3", "move from 0 to 1", "Move [0] from 0 to 1", "1, 0, 2")]
    [DataRow("take 3", "move 2 from 0 to 1", "Move [0, 1] from 0 to 1", "2, 0, 1")]
    [DataRow("take 3", "move from 5 to 8", "", "0, 1, 2")]
    [DataRow("take 3", "move from 1 to 5", "Reset", "0, 2, 3")]
    [DataRow("take 3", "reset", "Reset", "50, 51, 52")]
    [DataRow("skip 7", "insert 100 at 0", "Add [6] at 0", "6, 7, 8, 9")]
    [DataRow("skip 7", "remove at 9", "Remove [9] at 2", "7, 8")]
    [DataRow("skip 7", "add 100", "Add [100] at 3", "7, 8, 9, 100")]
    [DataRow("skip 7", "set 2 to 100", "", "7, 8, 9")]
    [DataRow("skip 7", "remove at 0", "Remove [7] at 0", "8, 9")]
    [DataRow("^4..^1", "insert 100 at 0", "", "6, 7, 8")]
    [DataRow("^4..^1", "add 100", "Remove [6] at 0; Add [9] at 2", "7, 8, 9")]
    [DataRow("^4..^1", "set 7 to 100", "Replace [7] with [100] at 1", "6, 100, 8")]
    [DataRow("^4..^1", "remove at 7", "Remove [7] at 1; Add [5] at 0", "5, 6, 8")]
    public void SliceAnnouncesOnlyWhatChangedWithinIt(string slice, string change, string expectedNotifications, string expectedElements)
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var sliceQuery = Observe(sourceQuery, slice))
            {
                var notifications = new List<string>();
                void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
                    notifications.Add(Describe(e));
                sliceQuery.CollectionChanged += collectionChanged;
                Change(source, change);
                sliceQuery.CollectionChanged -= collectionChanged;
                Assert.AreEqual(expectedNotifications, string.Join("; ", notifications));
                Assert.AreEqual(expectedElements, string.Join(", ", sliceQuery));
                Assert.AreEqual(expectedElements.Split(", ").Length, sliceQuery.Count);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
