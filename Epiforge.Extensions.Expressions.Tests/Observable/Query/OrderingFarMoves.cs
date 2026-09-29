namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// An ordering of a few hundred elements whose keys change to values anywhere in the order, so that elements move further than the few places the other ordering tests move them and are placed by the search which covers the whole order
/// </summary>
[TestClass]
public class OrderingFarMoves
{
    sealed class Ranked(int rank, int name) :
        PropertyChangeNotifier
    {
        int name = name;
        int rank = rank;

        public int Name
        {
            get => name;
            set => SetBackedProperty(ref name, in value);
        }

        public int Rank
        {
            get => rank;
            set => SetBackedProperty(ref rank, in value);
        }
    }

    static void AssertAgrees(IReadOnlyList<Ranked> source, IObservableCollectionQuery<Ranked> ordering, bool byName, int change)
    {
        var enumerated = ordering.ToList();
        Assert.AreEqual(source.Count, enumerated.Count, $"after change {change}: the ordering holds {enumerated.Count} elements where the source holds {source.Count}");
        CollectionAssert.AreEquivalent(source.ToList(), enumerated, $"after change {change}: the ordering holds different elements from the source");
        for (var i = 0; i < enumerated.Count; ++i)
        {
            Assert.AreSame(enumerated[i], ordering[i], $"after change {change}: the ordering's indexer disagrees with its enumeration at {i}");
            if (i == 0)
                continue;
            var previous = enumerated[i - 1];
            var current = enumerated[i];
            Assert.IsTrue(previous.Rank < current.Rank || previous.Rank == current.Rank && (!byName || previous.Name >= current.Name), $"after change {change}: the ordering holds rank {previous.Rank} name {previous.Name} before rank {current.Rank} name {current.Name}");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ElementsMovingAnywhereInTheOrderLandWhereTheyBelong(bool byName)
    {
        var random = new Random(byName ? 2 : 1);
        var source = new ObservableRangeCollection<Ranked>(Enumerable.Range(0, 400).Select(_ => new Ranked(random.Next(40), random.Next(5))));
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyList(source);
        using var ordering = byName ? sourceQuery.ObserveOrderBy((element => element.Rank, false), (element => element.Name, true)) : sourceQuery.ObserveOrderBy((element => element.Rank, false));
        AssertAgrees(source, ordering, byName, 0);
        for (var change = 1; change <= 4000; ++change)
        {
            var choice = random.Next(20);
            if (choice == 0)
                source.Insert(random.Next(source.Count + 1), random.Next(4) == 0 ? source[random.Next(source.Count)] : new Ranked(random.Next(40), random.Next(5)));
            else if (choice == 1 && source.Count > 1)
                source.RemoveAt(random.Next(source.Count));
            else if (choice == 2)
                source[random.Next(source.Count)] = new Ranked(random.Next(40), random.Next(5));
            else if (choice < 6)
                source[random.Next(source.Count)].Name = random.Next(5);
            else
                source[random.Next(source.Count)].Rank = random.Next(40);
            if (change % 50 == 0)
                AssertAgrees(source, ordering, byName, change);
        }
    }
}
