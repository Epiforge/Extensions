namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

/// <summary>
/// A range operation honors the reentrancy rule of <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>: a lone handler may change the collection while it is being told of a change, and with more than one handler a change made during an announcement is refused
/// </summary>
[TestClass]
public class ObservableRangeCollectionReentrancy
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RemovingAllWhileALoneHandlerShortensTheCollectionFinishes(bool individually)
    {
        var collection = new ObservableRangeCollection<int>(Enumerable.Range(1, 10), individually);
        var shortened = false;
        collection.CollectionChanged += (sender, e) =>
        {
            if (!shortened && e.Action is NotifyCollectionChangedAction.Remove)
            {
                shortened = true;
                collection.RemoveAt(collection.Count - 1);
            }
        };
        var removed = collection.GetAndRemoveAll(element => element % 2 == 0);
        CollectionAssert.AreEqual(new[] { 2, 4, 6, 8 }, removed.ToArray());
        CollectionAssert.AreEqual(new[] { 1, 3, 5, 7, 9 }, collection.ToArray());
    }

    [TestMethod]
    [DataRow("add", false)]
    [DataRow("add", true)]
    [DataRow("add range", false)]
    [DataRow("add range", true)]
    [DataRow("insert range", false)]
    [DataRow("insert range", true)]
    [DataRow("move range of one", false)]
    [DataRow("move range of one", true)]
    [DataRow("move range of two", false)]
    [DataRow("move range of two", true)]
    [DataRow("remove range at an index", false)]
    [DataRow("remove range at an index", true)]
    [DataRow("remove range of items", false)]
    [DataRow("remove range of items", true)]
    [DataRow("remove all", false)]
    [DataRow("remove all", true)]
    [DataRow("get and remove all", false)]
    [DataRow("get and remove all", true)]
    [DataRow("replace range", false)]
    [DataRow("replace range", true)]
    [DataRow("replace all", false)]
    [DataRow("replace all", true)]
    [DataRow("reset", false)]
    [DataRow("reset", true)]
    [DataRow("reset removing all", false)]
    [DataRow("reset removing all", true)]
    public void AChangeDuringAnAnnouncementToMoreThanOneHandlerIsRefused(string operation, bool individually)
    {
        var collection = new ObservableRangeCollection<int>(Enumerable.Range(1, 5), individually);
        Exception? refusal = null;
        var attempted = false;
        collection.CollectionChanged += (sender, e) => { };
        collection.CollectionChanged += (sender, e) =>
        {
            if (attempted)
                return;
            attempted = true;
            try
            {
                switch (operation)
                {
                    case "add":
                        collection.Add(7);
                        break;
                    case "add range":
                        collection.AddRange([7, 8]);
                        break;
                    case "insert range":
                        collection.InsertRange(0, [7, 8]);
                        break;
                    case "move range of one":
                        collection.MoveRange(0, 2, 1);
                        break;
                    case "move range of two":
                        collection.MoveRange(0, 2, 2);
                        break;
                    case "remove range at an index":
                        collection.RemoveRange(0, 2);
                        break;
                    case "remove range of items":
                        collection.RemoveRange([1, 2]);
                        break;
                    case "remove all":
                        collection.RemoveAll(element => element < 3);
                        break;
                    case "get and remove all":
                        collection.GetAndRemoveAll(element => element < 3);
                        break;
                    case "replace range":
                        collection.ReplaceRange(0, 2, [7, 8]);
                        break;
                    case "replace all":
                        collection.ReplaceAll([7]);
                        break;
                    case "reset":
                        collection.Reset([7]);
                        break;
                    default:
                        collection.ResetRemovingAll(element => element < 3);
                        break;
                }
            }
            catch (Exception ex)
            {
                refusal = ex;
            }
        };
        collection.Add(6);
        Assert.IsTrue(attempted);
        Assert.IsInstanceOfType<InvalidOperationException>(refusal, $"{operation} was not refused during an announcement to two handlers");
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, collection.ToArray(), $"{operation} changed the collection although it was refused");
    }
}
