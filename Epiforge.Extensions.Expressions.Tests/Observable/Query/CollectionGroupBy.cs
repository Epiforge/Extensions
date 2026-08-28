namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionGroupBy
{
    [TestMethod]
    public void GroupingsForSurvivingKeysSurviveAReset()
    {
        var source = new ObservableRangeCollection<TestPerson>(new TestPerson[] { new("Ben"), new("Erin") });
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var groupByQuery = sourceQuery.ObserveGroupBy(person => person.Name!.Length))
            {
                var threeLength = groupByQuery.Single(grouping => grouping.Key == 3);
                var announcements = 0;
                void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
                    ++announcements;
                threeLength.CollectionChanged += collectionChanged;
                source.Reset(new TestPerson[] { new("Bob"), new("Amy"), new("Charles") });
                Assert.AreSame(threeLength, groupByQuery.Single(grouping => grouping.Key == 3), "the grouping the caller was holding was replaced by the reset");
                CollectionAssert.AreEquivalent(new string?[] { "Bob", "Amy" }, threeLength.Select(person => person.Name).ToList(), "the grouping the caller was holding does not contain the elements which joined it");
                Assert.AreNotEqual(0, announcements, "the grouping the caller was holding announced nothing when its contents changed");
                Assert.IsFalse(groupByQuery.Any(grouping => grouping.Key == 4), "a grouping was retained for a key which no longer has any elements");
                threeLength.CollectionChanged -= collectionChanged;
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SourceManipulation()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var groupByQuery = sourceQuery.ObserveGroupBy(person => person.Name!.Length))
            {
                Assert.IsNull(groupByQuery.OperationFault);
                void checkMergedNames(string against) =>
                    Assert.AreEqual(against, string.Join(";", groupByQuery.OrderBy(group => group.Key).Select(group => $"{group!.Key}:{string.Join(",", group.OrderBy(person => person.Name).Select(person => person.Name))}")));
                checkMergedNames("3:Ben;4:Erin,John;5:Bryan,Cliff,Craig,Emily,James,Steve;6:George,Hunter;7:Bridget,Charles,Nanette");
                source[0].Name = "Adam";
                checkMergedNames("3:Ben;4:Adam,Erin;5:Bryan,Cliff,Craig,Emily,James,Steve;6:George,Hunter;7:Bridget,Charles,Nanette");
                source[0].Name = "J";
                checkMergedNames("1:J;3:Ben;4:Erin;5:Bryan,Cliff,Craig,Emily,James,Steve;6:George,Hunter;7:Bridget,Charles,Nanette");
                source[0].Name = "John";
                checkMergedNames("3:Ben;4:Erin,John;5:Bryan,Cliff,Craig,Emily,James,Steve;6:George,Hunter;7:Bridget,Charles,Nanette");
                source.Add(new TestPerson("Daniel"));
                checkMergedNames("3:Ben;4:Erin,John;5:Bryan,Cliff,Craig,Emily,James,Steve;6:Daniel,George,Hunter;7:Bridget,Charles,Nanette");
                source.RemoveAt(source.Count - 1);
                checkMergedNames("3:Ben;4:Erin,John;5:Bryan,Cliff,Craig,Emily,James,Steve;6:George,Hunter;7:Bridget,Charles,Nanette");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
