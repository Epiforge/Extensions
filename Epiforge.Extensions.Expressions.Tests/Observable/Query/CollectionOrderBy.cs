namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionOrderBy
{
    [TestMethod]
    public void AddIntoEmptyAnnouncesAdd()
    {
        var ben = new TestPerson("Ben");
        var source = new ObservableRangeCollection<TestPerson>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy(person => person.Name!))
            {
                var announcements = RecordAnnouncements(orderByQuery);
                source.Add(ben);
                AssertSingleAdd(announcements, [ben]);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void AddRangeIntoEmptyAnnouncesOrderedAdd()
    {
        var ben = new TestPerson("Ben");
        var erin = new TestPerson("Erin");
        var john = new TestPerson("John");
        var source = new ObservableRangeCollection<TestPerson>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy(person => person.Name!))
            {
                var announcements = RecordAnnouncements(orderByQuery);
                source.AddRange([john, ben, erin]);
                AssertSingleAdd(announcements, [ben, erin, john]);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void AddRangeIntoEmptyWithFaultingKeyAnnouncesAddOfPlacedOnly()
    {
        var ben = new TestPerson("Ben");
        var erin = new TestPerson("Erin");
        var source = new ObservableRangeCollection<TestPerson>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy(person => person.Name!.Length))
            {
                var announcements = RecordAnnouncements(orderByQuery);
                source.AddRange([erin, new TestPerson(), ben]);
                AssertSingleAdd(announcements, [ben, erin]);
                Assert.IsNotNull(orderByQuery.OperationFault);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    static void AssertSingleAdd(List<NotifyCollectionChangedEventArgs> announcements, TestPerson[] expectedItems)
    {
        Assert.AreEqual(1, announcements.Count, "the ordering did not make exactly one announcement");
        var announcement = announcements[0];
        Assert.AreEqual(NotifyCollectionChangedAction.Add, announcement.Action, "the ordering did not announce an arrival as an addition");
        CollectionAssert.AreEqual(expectedItems, announcement.NewItems, "the ordering did not announce the elements it placed, in order");
        Assert.AreEqual(0, announcement.NewStartingIndex, "the ordering did not announce the addition at its start");
        announcements.Clear();
    }

    static void AssertSingleRemove(List<NotifyCollectionChangedEventArgs> announcements, TestPerson[] expectedItems)
    {
        Assert.AreEqual(1, announcements.Count, "the ordering did not make exactly one announcement");
        var announcement = announcements[0];
        Assert.AreEqual(NotifyCollectionChangedAction.Remove, announcement.Action, "the ordering did not announce a departure as a removal");
        CollectionAssert.AreEqual(expectedItems, announcement.OldItems, "the ordering did not announce the elements it removed, in order");
        Assert.AreEqual(0, announcement.OldStartingIndex, "the ordering did not announce the removal at its start");
        announcements.Clear();
    }

    [TestMethod]
    public void ElementReturningToEmptyFilteredOrderingAnnouncesRemoveThenAdd()
    {
        var ben = new TestPerson("Ben");
        var source = new ObservableRangeCollection<TestPerson>([ben, new TestPerson("Erin")]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length == 3))
            using (var orderByQuery = whereQuery.ObserveOrderBy(person => person.Name!))
            {
                var announcements = RecordAnnouncements(orderByQuery);
                ben.Name = "Benjamin";
                AssertSingleRemove(announcements, [ben]);
                ben.Name = "Ben";
                AssertSingleAdd(announcements, [ben]);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void EqualKeysPreserveSourceOrder()
    {
        var people = Enumerable.Range(0, 40).Select(index => new TestPerson($"P{index:D3}")).ToList();
        var source = new ObservableRangeCollection<TestPerson>(people);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy(person => person.Name!.Length))
            {
                CollectionAssert.AreEqual(people, orderByQuery.ToList());
                source.Reset(people);
                CollectionAssert.AreEqual(people, orderByQuery.ToList());
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void NoSelectors()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy())
            {
                void checkMergedNames(string against) =>
                    Assert.AreEqual(against, string.Join(string.Empty, orderByQuery!.Select(person => person.Name)));
                checkMergedNames("JohnEmilyCharlesErinCliffHunterBenCraigBridgetNanetteGeorgeBryanJamesSteve");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    static List<NotifyCollectionChangedEventArgs> RecordAnnouncements(IObservableCollectionQuery<TestPerson> query)
    {
        var announcements = new List<NotifyCollectionChangedEventArgs>();
        query.CollectionChanged += (sender, e) => announcements.Add(e);
        return announcements;
    }

    [TestMethod]
    public void RemoveAllAnnouncesOrderedRemove()
    {
        var ben = new TestPerson("Ben");
        var erin = new TestPerson("Erin");
        var john = new TestPerson("John");
        var source = new ObservableRangeCollection<TestPerson>([john, ben, erin]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy(person => person.Name!))
            {
                var announcements = RecordAnnouncements(orderByQuery);
                source.RemoveRange(0, 3);
                AssertSingleRemove(announcements, [ben, erin, john]);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void RemoveAllWithFaultingKeyAnnouncesRemoveOfPlacedOnly()
    {
        var ben = new TestPerson("Ben");
        var erin = new TestPerson("Erin");
        var source = new ObservableRangeCollection<TestPerson>([erin, new TestPerson(), ben]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy(person => person.Name!.Length))
            {
                var announcements = RecordAnnouncements(orderByQuery);
                source.RemoveRange(0, 3);
                AssertSingleRemove(announcements, [ben, erin]);
                Assert.IsNull(orderByQuery.OperationFault);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void RemoveAllWithheldAnnouncesNothing()
    {
        var source = new ObservableRangeCollection<TestPerson>([new TestPerson()]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy(person => person.Name!.Length))
            {
                var announcements = RecordAnnouncements(orderByQuery);
                source.RemoveAt(0);
                Assert.AreEqual(0, announcements.Count, "the ordering announced a change when none of its elements changed");
                Assert.IsNull(orderByQuery.OperationFault);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void RemoveLastAnnouncesRemove()
    {
        var ben = new TestPerson("Ben");
        var source = new ObservableRangeCollection<TestPerson>([ben]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy(person => person.Name!))
            {
                var announcements = RecordAnnouncements(orderByQuery);
                source.RemoveAt(0);
                AssertSingleRemove(announcements, [ben]);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SelectorsDirections()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy((person => person.Name!.Length, true), (person => person.Name!, false)))
            {
                void checkMergedNames(string against) =>
                    Assert.AreEqual(against, string.Join(string.Empty, orderByQuery!.Select(person => person.Name)));
                checkMergedNames("BridgetCharlesNanetteGeorgeHunterBryanCliffCraigEmilyJamesSteveErinJohnBen");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SourceManipulation()
    {
        var source = TestPerson.CreatePeopleCollection();
        source.Add(source[0]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy(person => person.Name!))
            {
                void checkMergedNames(string against) =>
                    Assert.AreEqual(against, string.Join(string.Empty, orderByQuery!.Select(person => person.Name)));
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJohnJohnNanetteSteve");
                source.Add(source[0]);
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJohnJohnJohnNanetteSteve");
                source[0].Name = "Buddy";
                checkMergedNames("BenBridgetBryanBuddyBuddyBuddyCharlesCliffCraigEmilyErinGeorgeHunterJamesNanetteSteve");
                source[0].Name = "John";
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJohnJohnJohnNanetteSteve");
                source.RemoveRange(source.Count - 2, 2);
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJohnNanetteSteve");
                source.Add(new TestPerson("Javon"));
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJavonJohnNanetteSteve");
                source[0].Name = null;
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJavonNanetteSteve");
                source.Insert(0, new TestPerson());
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJavonNanetteSteve");
                source.RemoveRange(0, 2);
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJavonNanetteSteve");
                source.Add(new TestPerson());
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJavonNanetteSteve");
                source.Add(new TestPerson("Daniel"));
                checkMergedNames("BenBridgetBryanCharlesCliffCraigDanielEmilyErinGeorgeHunterJamesJavonNanetteSteve");
                source.Reset(new TestPerson[] { new TestPerson("Kelly") });
                checkMergedNames("Kelly");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SourceManipulationMultipleSelectors()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy((person => person.Name!.Length, false), (person => person.Name!, false)))
            {
                void checkMergedNames(string against) =>
                    Assert.AreEqual(against, string.Join(string.Empty, orderByQuery!.Select(person => person.Name)));
                checkMergedNames("BenErinJohnBryanCliffCraigEmilyJamesSteveGeorgeHunterBridgetCharlesNanette");
                source[0].Name = "J";
                checkMergedNames("JBenErinBryanCliffCraigEmilyJamesSteveGeorgeHunterBridgetCharlesNanette");
                source[0].Name = "Johnathon";
                checkMergedNames("BenErinBryanCliffCraigEmilyJamesSteveGeorgeHunterBridgetCharlesNanetteJohnathon");
                Assert.IsNull(orderByQuery.OperationFault);
                source[0].Name = null;
                Assert.IsNotNull(orderByQuery.OperationFault);
                source[0].Name = "John";
                checkMergedNames("BenErinJohnBryanCliffCraigEmilyJamesSteveGeorgeHunterBridgetCharlesNanette");
                Assert.IsNull(orderByQuery.OperationFault);
                source.Add(new TestPerson("Daniel"));
                checkMergedNames("BenErinJohnBryanCliffCraigEmilyJamesSteveDanielGeorgeHunterBridgetCharlesNanette");
                source.RemoveAt(source.Count - 1);
                checkMergedNames("BenErinJohnBryanCliffCraigEmilyJamesSteveGeorgeHunterBridgetCharlesNanette");
                source.Reset(new TestPerson[] { new TestPerson("Kelly") });
                checkMergedNames("Kelly");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SourceResetAnnouncesReset()
    {
        var ben = new TestPerson("Ben");
        var erin = new TestPerson("Erin");
        var source = new ObservableRangeCollection<TestPerson>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy(person => person.Name!))
            {
                var announcements = RecordAnnouncements(orderByQuery);
                source.Reset([erin, ben]);
                Assert.AreEqual(1, announcements.Count, "the ordering did not make exactly one announcement for a reset into it while empty");
                Assert.AreEqual(NotifyCollectionChangedAction.Reset, announcements[0].Action, "the ordering did not announce a reset of its source as a reset when it was empty");
                CollectionAssert.AreEqual(new[] { ben, erin }, orderByQuery.ToList());
                announcements.Clear();
                source.Reset([]);
                Assert.AreEqual(1, announcements.Count, "the ordering did not make exactly one announcement for a reset emptying it");
                Assert.AreEqual(NotifyCollectionChangedAction.Reset, announcements[0].Action, "the ordering did not announce a reset of its source emptying it as a reset");
                Assert.AreEqual(0, orderByQuery.Count);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
