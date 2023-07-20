namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionSelectMany
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new RangeObservableCollection<TestTeam>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var selectManyQuery = sourceQuery.ObserveSelectMany(team => team.People!))
            {
                void checkMergedNames(string against) =>
                    Assert.AreEqual(against, string.Join(string.Empty, selectManyQuery!.Select(person => person.Name)));
                var management = new TestTeam();
                management.People!.Add(new TestPerson("Charles"));
                source.Add(management);
                checkMergedNames("Charles");
                management.People!.Add(new TestPerson("Michael"));
                checkMergedNames("CharlesMichael");
                management.People!.RemoveAt(1);
                checkMergedNames("Charles");
                var development = new TestTeam();
                source.Add(development);
                checkMergedNames("Charles");
                development.People!.AddRange(new TestPerson[]
                {
                    new TestPerson("John"),
                    new TestPerson("Emily"),
                    new TestPerson("Edward"),
                    new TestPerson("Andrew")
                });
                checkMergedNames("CharlesJohnEmilyEdwardAndrew");
                development.People.RemoveRange(2, 2);
                checkMergedNames("CharlesJohnEmily");
                var qa = new TestTeam();
                qa.People!.AddRange(new TestPerson[]
                {
                    new TestPerson("Aaron"),
                    new TestPerson("Cliff")
                });
                source.Add(qa);
                checkMergedNames("CharlesJohnEmilyAaronCliff");
                qa.People[0].Name = "Erin";
                checkMergedNames("CharlesJohnEmilyErinCliff");
                var bryan = new TestPerson("Brian");
                var it = new TestTeam();
                it.People!.AddRange(new TestPerson[] { bryan, bryan });
                source.Add(it);
                checkMergedNames("CharlesJohnEmilyErinCliffBrianBrian");
                bryan.Name = "Bryan";
                checkMergedNames("CharlesJohnEmilyErinCliffBryanBryan");
                it.People.Clear();
                checkMergedNames("CharlesJohnEmilyErinCliff");
                it.People = null;
                checkMergedNames("CharlesJohnEmilyErinCliff");
                it.People = new RangeObservableCollection<TestPerson>()
                {
                    new TestPerson("Paul")
                };
                checkMergedNames("CharlesJohnEmilyErinCliffPaul");
                it.People[0] = new TestPerson("Alex");
                checkMergedNames("CharlesJohnEmilyErinCliffAlex");
                development.People.Move(1, 0);
                checkMergedNames("CharlesEmilyJohnErinCliffAlex");
                development.People.ReplaceRange(0, 2, development.People.GetRange(0, 1));
                checkMergedNames("CharlesEmilyErinCliffAlex");
                it.People.Clear();
                it.People.Reset(new TestPerson[] { new TestPerson("Daniel") });
                checkMergedNames("CharlesEmilyErinCliffDaniel");
                source.Add(management);
                checkMergedNames("CharlesEmilyErinCliffDanielCharles");
                management.People.Insert(0, new TestPerson("George"));
                checkMergedNames("GeorgeCharlesEmilyErinCliffDanielGeorgeCharles");
                var currentManagers = management.People;
                var otherManagers = new RangeObservableCollection<TestPerson>()
                {
                    new TestPerson("Josh"),
                    new TestPerson("Jessica")
                };
                management.People = otherManagers;
                checkMergedNames("JoshJessicaEmilyErinCliffDanielJoshJessica");
                management.People = currentManagers;
                source.RemoveAt(source.Count - 1);
                checkMergedNames("GeorgeCharlesEmilyErinCliffDaniel");
                source.Insert(0, management);
                checkMergedNames("GeorgeCharlesGeorgeCharlesEmilyErinCliffDaniel");
                source.Move(2, 1);
                checkMergedNames("GeorgeCharlesEmilyGeorgeCharlesErinCliffDaniel");
                source.Move(1, 2);
                checkMergedNames("GeorgeCharlesGeorgeCharlesEmilyErinCliffDaniel");
                source.RemoveAt(1);
                checkMergedNames("GeorgeCharlesEmilyErinCliffDaniel");
                source.RemoveAt(0);
                checkMergedNames("EmilyErinCliffDaniel");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
