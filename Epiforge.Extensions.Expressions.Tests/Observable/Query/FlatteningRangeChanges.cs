namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// A flattening following a change of several elements at once to a sequence its selector returned, both to a sequence one team holds and to one two teams share
/// </summary>
[TestClass]
public class FlatteningRangeChanges
{
    static string Change(ObservableRangeCollection<TestPerson> people, string change)
    {
        switch (change)
        {
            case "add a range at the end":
                people.AddRange([new TestPerson("N0"), new TestPerson("N1")]);
                break;
            case "insert a range in the middle":
                people.InsertRange(1, [new TestPerson("N0"), new TestPerson("N1")]);
                break;
            case "remove a range":
                people.RemoveRange(1, 2);
                break;
            case "replace a range with as many":
                people.ReplaceRange(1, 2, [new TestPerson("N0"), new TestPerson("N1")]);
                break;
            case "replace a range with more":
                people.ReplaceRange(1, 2, [new TestPerson("N0"), new TestPerson("N1"), new TestPerson("N2")]);
                break;
            case "replace a range with fewer":
                people.ReplaceRange(0, 3, [new TestPerson("N0")]);
                break;
            case "replace one by index":
                people[1] = new TestPerson("N0");
                break;
            case "move a range forward":
                people.MoveRange(0, 1, 2);
                break;
            case "move a range back":
                people.MoveRange(1, 0, 2);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }
        return string.Join(",", people.Select(person => person.Name));
    }

    [TestMethod]
    [DataRow("add a range at the end", false)]
    [DataRow("add a range at the end", true)]
    [DataRow("insert a range in the middle", false)]
    [DataRow("insert a range in the middle", true)]
    [DataRow("remove a range", false)]
    [DataRow("remove a range", true)]
    [DataRow("replace a range with as many", false)]
    [DataRow("replace a range with as many", true)]
    [DataRow("replace a range with more", false)]
    [DataRow("replace a range with more", true)]
    [DataRow("replace a range with fewer", false)]
    [DataRow("replace a range with fewer", true)]
    [DataRow("replace one by index", false)]
    [DataRow("replace one by index", true)]
    [DataRow("move a range forward", false)]
    [DataRow("move a range forward", true)]
    [DataRow("move a range back", false)]
    [DataRow("move a range back", true)]
    public void AFlatteningFollowingAChangeOfSeveralElementsAgreesWithADirectFlattening(string change, bool shared)
    {
        var people = new ObservableRangeCollection<TestPerson>(Enumerable.Range(0, 5).Select(index => new TestPerson($"P{index}")));
        var others = new ObservableRangeCollection<TestPerson>(Enumerable.Range(0, 3).Select(index => new TestPerson($"Q{index}")));
        var source = new ObservableRangeCollection<TestTeam>(shared ? [new TestTeam(people), new TestTeam(others), new TestTeam(people)] : [new TestTeam(others), new TestTeam(people), new TestTeam(others)]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        using var flattened = sourceQuery.ObserveSelectMany(team => team.People!);
        var mirror = new List<TestPerson>(flattened);
        flattened.CollectionChanged += (sender, e) =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    for (var i = 0; i < e.NewItems!.Count; ++i)
                        mirror.Insert(e.NewStartingIndex + i, (TestPerson)e.NewItems[i]!);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    mirror.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    mirror.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                    for (var i = 0; i < e.NewItems!.Count; ++i)
                        mirror.Insert(e.OldStartingIndex + i, (TestPerson)e.NewItems[i]!);
                    break;
                case NotifyCollectionChangedAction.Move:
                    var moved = mirror.GetRange(e.OldStartingIndex, e.OldItems!.Count);
                    mirror.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                    mirror.InsertRange(e.NewStartingIndex, moved);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    mirror.Clear();
                    mirror.AddRange(flattened);
                    break;
            }
        };
        var changed = Change(people, change);
        var expected = source.SelectMany(team => team.People!).Select(person => person.Name).ToList();
        var context = $"after {change}, leaving {changed}";
        CollectionAssert.AreEqual(expected, flattened.Select(person => person.Name).ToList(), $"{context}: the flattening holds {string.Join(",", flattened.Select(person => person.Name))}");
        CollectionAssert.AreEqual(expected, mirror.Select(person => person.Name).ToList(), $"{context}: what it announced adds up to {string.Join(",", mirror.Select(person => person.Name))}");
        Assert.AreEqual(expected.Count, flattened.Count, $"{context}: the count is {flattened.Count}");
        var indexed = Enumerable.Range(0, flattened.Count).Select(index => flattened[index].Name).ToList();
        CollectionAssert.AreEqual(expected, indexed, $"{context}: reading by index gives {string.Join(",", indexed)}");
    }
}
