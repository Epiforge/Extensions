namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Pins the order in which a grouping holds its elements, which is documented as the order they were added and which nothing else asserts
/// </summary>
/// <remarks>
/// <c>CollectionGroupBy</c> and <c>CollectionGroupByRandomizedOperations</c> both sort a grouping's members before comparing them, so they establish which elements a grouping holds and say nothing about the order it holds them in. A change to how a grouping finds an element in order to remove it could therefore reorder every grouping in the library without failing a test
/// </remarks>
[TestClass]
public class CollectionGroupByOrdering
{
    static string Names(IObservableCollectionQuery<IObservableGrouping<int, TestPerson>> groupBy, int key) =>
        string.Join(",", groupBy.Single(grouping => grouping.Key == key).Select(person => person.Name));

    [TestMethod]
    public void AnElementAddedLaterGoesToTheEndOfItsGroupingWhereverItEntersTheSource()
    {
        var ben = new TestPerson("Ben");
        var erin = new TestPerson("Erin");
        var amy = new TestPerson("Amy");
        var bob = new TestPerson("Bob");
        var source = new ObservableRangeCollection<TestPerson>([ben, erin, amy]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var groupByQuery = sourceQuery.ObserveGroupBy(person => person.Name!.Length))
            {
                Assert.AreEqual("Ben,Amy", Names(groupByQuery, 3), "a grouping did not begin in the order its elements occur in the source");
                source.Insert(0, bob);
                Assert.AreEqual("Ben,Amy,Bob", Names(groupByQuery, 3), "an element inserted at the front of the source did not go to the end of its grouping");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void AnElementWhoseKeyChangesGoesToTheEndOfItsNewGrouping()
    {
        var ben = new TestPerson("Ben");
        var amy = new TestPerson("Amy");
        var erin = new TestPerson("Erin");
        var source = new ObservableRangeCollection<TestPerson>([ben, erin, amy]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var groupByQuery = sourceQuery.ObserveGroupBy(person => person.Name!.Length))
            {
                Assert.AreEqual("Ben,Amy", Names(groupByQuery, 3));
                erin.Name = "Ann";
                Assert.AreEqual("Ben,Amy,Ann", Names(groupByQuery, 3), "an element whose key changed did not go to the end of the grouping it joined");
                Assert.IsFalse(groupByQuery.Any(grouping => grouping.Key == 4), "a grouping was retained for a key which no longer has any elements");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    /// <summary>
    /// Requires that where an element occurs in a grouping more than once, taking one occurrence away takes the first and leaves the rest where they were
    /// </summary>
    /// <remarks>
    /// This is the case any change to how a grouping locates an element has to preserve, and the one most likely to be got wrong: an index kept per element rather than per occurrence would remove whichever occurrence it happened to know about
    /// </remarks>
    /// <remarks>
    /// Note that the source takes the second occurrence away and the grouping takes the first, which is indistinguishable here because both are the same instance. A grouping is not obliged to take the same occurrence the source did, only to leave the right number of them in the right order
    /// </remarks>
    [TestMethod]
    public void RemovingOneOccurrenceOfARepeatedElementTakesTheFirstAndLeavesTheOrderOfTheRest()
    {
        var ben = new TestPerson("Ben");
        var amy = new TestPerson("Amy");
        var bob = new TestPerson("Bob");
        var source = new ObservableRangeCollection<TestPerson>([ben, amy, ben, bob]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var groupByQuery = sourceQuery.ObserveGroupBy(person => person.Name!.Length))
            {
                Assert.AreEqual("Ben,Amy,Ben,Bob", Names(groupByQuery, 3), "a grouping did not hold a repeated element once for each of its occurrences");
                source.RemoveAt(2);
                Assert.AreEqual("Amy,Ben,Bob", Names(groupByQuery, 3), "taking one occurrence of a repeated element away did not take the first, or did not leave the rest in order");
                source.RemoveAt(1);
                Assert.AreEqual("Ben,Bob", Names(groupByQuery, 3), "taking an element which occurs once away disturbed the elements around it");
                source.RemoveAt(0);
                Assert.AreEqual("Bob", Names(groupByQuery, 3), "taking the last occurrence of a repeated element away disturbed the elements around it");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
